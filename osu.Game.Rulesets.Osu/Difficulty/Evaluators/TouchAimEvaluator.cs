// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class TouchAimEvaluator
    {
        private const double hand_coordination_bonus = 1.275;
        private const double transition_to_drag_bonus = 2.35;
        private const double aim_obstruction_max_bonus = 3.85;

        /// <summary>
        /// Evaluates the difficulty of aiming the current object with touch device, based on:
        /// <list type="bullet">
        /// <item><description>the standard aim difficulty from the previous object hit with the same hand,</description></item>
        /// <item><description>difficulty of coordinating between two hands,</description></item>
        /// <item><description>physical obstruction from the other hand,</description></item>
        /// <item><description>and difficulty of transitioning from tapping to dragging.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(
            DifficultyHitObject current,
            bool includeSliders)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;

            if (osuCurrent.TouchData == null || osuCurrent.TouchData.Value.PerHandObject == null)
                return 0;

            var touchData = osuCurrent.TouchData.Value;

            bool isHandSwitch = touchData.ActingHand != touchData.PrevActingHand;

            bool isTransitionToDrag = touchData.Action is OsuTouchAction.OsuDragAction
                && touchData.PrevAction is not OsuTouchAction.OsuDragAction;

            double aimMultiplier = 1.0;

            // Reward the hand coordination required to switch aim hands across consecutive objects.
            if (isHandSwitch)
                aimMultiplier += hand_coordination_bonus;

            // Reward the difficulty of suddenly transitioning from tapping to dragging on jumps.
            if (isTransitionToDrag)
                aimMultiplier += transition_to_drag_bonus;

            // Reward physical obstruction created by overlapping hands during jumps.
            aimMultiplier += touchData.ObstructionFactor * aim_obstruction_max_bonus;

            double aimDifficulty = AimEvaluator.EvaluateDifficultyOf(touchData.PerHandObject, includeSliders);

            // The previous difficulties only apply to the sliderhead and not the rest of the slider.
            // Thus, apply the reward to only the non-slider component of aim.
            if (includeSliders)
            {
                double aimNoSliders = AimEvaluator.EvaluateDifficultyOf(touchData.PerHandObject, false);
                aimDifficulty = aimNoSliders * aimMultiplier + (aimDifficulty - aimNoSliders);
            }
            else
            {
                aimDifficulty *= aimMultiplier;
            }

            return aimDifficulty;
        }
    }
}
