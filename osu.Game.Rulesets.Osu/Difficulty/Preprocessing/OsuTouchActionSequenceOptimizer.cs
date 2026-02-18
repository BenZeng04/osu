// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Skills;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    /// <summary>
    /// Finds a sequence of <see cref="OsuTouchAction"/>s (left hand, right hand, or drag) that approximately minimizes PP across the beatmap using beam search.
    /// </summary>
    public static class OsuTouchActionSequenceOptimizer
    {
        /// <summary>
        /// Controls the maximum number of sequences we consider at once. 
        /// As beam_width tends to infinity, the optimizer finds the true optimum.
        /// </summary>
        private const int beam_width = 20;

        private const double pp_norm_exponent = 6;

        private static readonly OsuTouchAction[] actions = [OsuTouchAction.Left, OsuTouchAction.Right, OsuTouchAction.Drag];

        public static List<OsuTouchAction> FindOptimalActionSequence(List<OsuDifficultyHitObject> objects)
        {
            if (objects.Count == 0)
                return [];

            // The first object must be tapped with either your left or right hand.
            // It cannot be dragged, since objects hit with drag are still assigned a hand corresponding
            // to the most recent non-dragged object.
            var currentCandidates = new List<TouchSequenceCandidate>
            {
                createInitialSequence(objects[0], OsuTouchAction.Right),
                createInitialSequence(objects[0], OsuTouchAction.Left)
            };

            for (int i = 1; i < objects.Count; i++)
            {
                var current = objects[i];
                var nextCandidates = new List<TouchSequenceCandidate>(currentCandidates.Count * actions.Length);

                // Rhythm difficulty is independent of touch action sequence.
                // Since rhythm calc is computationally expensive, we compute it here instead of inside
                // evaluateAction() so that the result can be reused.
                // TODO: there's probably a cleaner way of doing this.
                double rhythm = RhythmEvaluator.EvaluateDifficultyOf(current);
                foreach (var state in currentCandidates)
                {
                    // For each candidate action sequence for hitting the first i objects:
                    // Compute the approximate new PP value for each new possible action we could hit the next object with
                    foreach (var action in actions)
                        nextCandidates.Add(advance(state, current, action, rhythm));
                }

                nextCandidates.Sort((a, b) => a.ApproximateSR.CompareTo(b.ApproximateSR));

                if (nextCandidates.Count > beam_width)
                    nextCandidates.RemoveRange(beam_width, nextCandidates.Count - beam_width);

                currentCandidates = nextCandidates;
            }

            return reconstructActionList(currentCandidates[0].ActionHistory, objects.Count);
        }

        private static TouchSequenceCandidate createInitialSequence(OsuDifficultyHitObject osuCurrent, OsuTouchAction action)
        {
            var result = OsuTouchDataIncrementalState.Advance(OsuTouchDataIncrementalState.INITIAL, osuCurrent, action);

            // The strain for the first object is always zero.
            return new TouchSequenceCandidate(
                result.NextState,
                aimStrainState: new Aim.StrainState(0, 0),
                speedStrainState: 0,
                approximatePP: 0,
                prevSequence: new PathNode(action, null));
        }

        private static TouchSequenceCandidate advance(TouchSequenceCandidate state, OsuDifficultyHitObject osuCurrent, OsuTouchAction action, double rhythm)
        {
            var historyAdvanceResult = OsuTouchDataIncrementalState.Advance(state.TouchDataState, osuCurrent, action);

            // Keep track of previous touch data so we can restore it later. 
            // This should always be null if the pattern solver is only invoked once, but we keep track of it for good measure.
            var previousTouchData = osuCurrent.TouchData;

            // Temporarily evaluate the strains of the current object as if it were completed with the given action.
            // IMPORTANT NOTE: strain evaluation should only depend on the current object's TouchData and not previous objects' touch data.
            // We do not set TouchData for the previous objects since doing so would require expensive deep clones and rewriting of object histories.
            osuCurrent.TouchData = historyAdvanceResult.TouchData;

            var newAimStrainState = Aim.AdvanceStrainState(state.AimStrainState, osuCurrent, isTouch: true, includeSliders: true);
            double newSpeedStrainState = Speed.AdvanceStrainState(state.SpeedStrainState, osuCurrent, isTouch: true);

            double currentAimStrain = Aim.ComputeOverallStrain(newAimStrainState);
            double currentSpeedStrain = Speed.ComputeOverallStrain(newSpeedStrainState, rhythm);

            osuCurrent.TouchData = previousTouchData;

            // 1.5 is an approximate relation between strain values and PP, since SR ~ sqrt(sum of weighted strains) and PP ~ SR^3
            double totalStrain = DifficultyCalculationUtils.Norm(1.5, currentAimStrain, currentSpeedStrain);

            // The true SR is the sum of weighted section peaks, which is computationally expensive to compute.
            // Using a power norm is a reasonable enough approximation for beam search.
            double newApproximateSR = DifficultyCalculationUtils.Norm(pp_norm_exponent, state.ApproximateSR, totalStrain);

            return new TouchSequenceCandidate(
                historyAdvanceResult.NextState,
                newAimStrainState,
                newSpeedStrainState,
                newApproximateSR,
                new PathNode(action, state.ActionHistory));
        }

        private static List<OsuTouchAction> reconstructActionList(PathNode? tail, int length)
        {
            var path = new List<OsuTouchAction>(length);

            for (var node = tail; node != null; node = node.Previous)
                path.Add(node.Action);

            path.Reverse();
            return path;
        }
        private sealed class PathNode
        {
            public readonly OsuTouchAction Action;
            public readonly PathNode? Previous;

            public PathNode(OsuTouchAction action, PathNode? previous)
            {
                Action = action;
                Previous = previous;
            }
        }
        private readonly struct TouchSequenceCandidate
        {
            public readonly OsuTouchDataIncrementalState TouchDataState;
            public readonly Aim.StrainState AimStrainState;
            public readonly double SpeedStrainState;
            public readonly double ApproximateSR;
            public readonly PathNode? ActionHistory;

            public TouchSequenceCandidate(
                OsuTouchDataIncrementalState touchDataState,
                Aim.StrainState aimStrainState,
                double speedStrainState,
                double approximatePP,
                PathNode? prevSequence)
            {
                TouchDataState = touchDataState;
                AimStrainState = aimStrainState;
                SpeedStrainState = speedStrainState;
                ApproximateSR = approximatePP;
                ActionHistory = prevSequence;
            }
        }
    }
}
