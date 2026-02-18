// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

// TODO: document code
namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    public readonly struct OsuTouchDataIncrementalState
    {
        private const double winding_decay_base = 0.8;

        public static readonly OsuTouchDataIncrementalState INITIAL = new OsuTouchDataIncrementalState(
            leftLast: null,
            leftLastLast: null,
            rightLast: null,
            rightLastLast: null,
            lastAction: null,
            lastNonDragHand: null,
            previousSeparationAngle: null,
            accumulatedWindingAngle: 0);

        private readonly OsuDifficultyHitObject? leftLast;
        private readonly OsuDifficultyHitObject? leftLastLast;
        private readonly OsuDifficultyHitObject? rightLast;
        private readonly OsuDifficultyHitObject? rightLastLast;
        private readonly OsuTouchAction? lastAction;
        private readonly OsuTouchHand? lastNonDragHand;
        private readonly double? previousSeparationAngle;
        private readonly double accumulatedWindingAngle;

        private OsuTouchDataIncrementalState(
            OsuDifficultyHitObject? leftLast,
            OsuDifficultyHitObject? leftLastLast,
            OsuDifficultyHitObject? rightLast,
            OsuDifficultyHitObject? rightLastLast,
            OsuTouchAction? lastAction,
            OsuTouchHand? lastNonDragHand,
            double? previousSeparationAngle,
            double accumulatedWindingAngle)
        {
            this.leftLast = leftLast;
            this.leftLastLast = leftLastLast;
            this.rightLast = rightLast;
            this.rightLastLast = rightLastLast;
            this.lastAction = lastAction;
            this.lastNonDragHand = lastNonDragHand;
            this.previousSeparationAngle = previousSeparationAngle;
            this.accumulatedWindingAngle = accumulatedWindingAngle;
        }

        public static AdvanceResult Advance(OsuTouchDataIncrementalState state, OsuDifficultyHitObject current, OsuTouchAction action)
        {
            OsuTouchHand actingHand = getActingHand(state, current, action);
            OsuDifficultyHitObject? actingLast = actingHand == OsuTouchHand.Left ? state.leftLast : state.rightLast;
            OsuDifficultyHitObject? actingLastLast = actingHand == OsuTouchHand.Left ? state.leftLastLast : state.rightLastLast;
            OsuDifficultyHitObject? otherLast = actingHand == OsuTouchHand.Left ? state.rightLast : state.leftLast;

            OsuDifficultyHitObject? perHandObject = createPerHandDifficultyObject(current, actingLast, actingLastLast);

            bool isHandSwitch = action is not OsuTouchAction.OsuDragAction
                && state.lastNonDragHand is { } prevHand
                && actingHand != prevHand;

            OsuDifficultyHitObject? nextLeftLast = state.leftLast;
            OsuDifficultyHitObject? nextLeftLastLast = state.leftLastLast;
            OsuDifficultyHitObject? nextRightLast = state.rightLast;
            OsuDifficultyHitObject? nextRightLastLast = state.rightLastLast;

            OsuDifficultyHitObject historyObject = perHandObject ?? current;

            if (actingHand == OsuTouchHand.Left)
            {
                nextLeftLastLast = nextLeftLast;
                nextLeftLast = historyObject;
            }
            else
            {
                nextRightLastLast = nextRightLast;
                nextRightLast = historyObject;
            }

            (double? nextSeparationAngle, double nextWindingAngle, double windingDelta) = updateWindingAngle(
                current,
                nextLeftLast,
                nextRightLast,
                state.previousSeparationAngle,
                state.accumulatedWindingAngle);

            double obstruction = 0;

            if (isHandSwitch)
            {
                obstruction = calculateObstructionFactor(
                    current, actingLast, otherLast,
                    state.accumulatedWindingAngle, windingDelta);
            }

            OsuTouchDataIncrementalState nextState = new OsuTouchDataIncrementalState(
                nextLeftLast,
                nextLeftLastLast,
                nextRightLast,
                nextRightLastLast,
                action,
                action is OsuTouchAction.OsuHandAction ha ? ha.Hand : state.lastNonDragHand,
                nextSeparationAngle,
                nextWindingAngle);

            OsuDifficultyHitObjectTouchData metadata = new OsuDifficultyHitObjectTouchData(
                action,
                actingHand,
                state.lastAction,
                state.lastNonDragHand,
                perHandObject,
                obstruction);

            return new AdvanceResult(nextState, metadata);
        }
        private static OsuDifficultyHitObject? createPerHandDifficultyObject(
            OsuDifficultyHitObject current,
            OsuDifficultyHitObject? last,
            OsuDifficultyHitObject? lastLast)
        {
            if (last == null)
                return null;

            var history = new List<DifficultyHitObject>(2);

            if (lastLast != null)
                history.Add(lastLast);

            history.Add(last);

            return new OsuDifficultyHitObject(current.BaseObject, last.BaseObject, current.ClockRate, history, history.Count);
        }
        private static OsuTouchHand getActingHand(OsuTouchDataIncrementalState state, OsuDifficultyHitObject current, OsuTouchAction action)
        {
            if (action is OsuTouchAction.OsuHandAction handAction)
                return handAction.Hand;

            var previous = current.Index > 0 ? (OsuDifficultyHitObject)current.Previous(0) : null;
            if (previous != null)
            {
                if (state.leftLast?.BaseObject == previous.BaseObject)
                    return OsuTouchHand.Left;

                if (state.rightLast?.BaseObject == previous.BaseObject)
                    return OsuTouchHand.Right;
            }

            return state.lastNonDragHand ?? OsuTouchHand.Left;
        }
        private static (double? previousSeparationAngle, double accumulatedWindingAngle, double windingDelta) updateWindingAngle(
            OsuDifficultyHitObject current,
            OsuDifficultyHitObject? leftLast,
            OsuDifficultyHitObject? rightLast,
            double? previousSeparationAngle,
            double accumulatedWindingAngle)
        {
            if (leftLast == null || rightLast == null)
                return (previousSeparationAngle, accumulatedWindingAngle, 0);

            Vector2 leftPos = leftLast.GetEndCursorPosition();
            Vector2 rightPos = rightLast.GetEndCursorPosition();
            double currentAngle = Math.Atan2(rightPos.Y - leftPos.Y, rightPos.X - leftPos.X);

            double decay = Math.Pow(winding_decay_base, current.AdjustedDeltaTime / 1000.0);
            double nextAccumulated = accumulatedWindingAngle * decay;

            if (!previousSeparationAngle.HasValue)
                return (currentAngle, nextAccumulated, 0);

            double delta = Math.IEEERemainder(currentAngle - previousSeparationAngle.Value, 2 * Math.PI);
            nextAccumulated += delta;

            return (currentAngle, nextAccumulated, delta);
        }
        private static double calculateObstructionFactor(
            OsuDifficultyHitObject target,
            OsuDifficultyHitObject? actingLast,
            OsuDifficultyHitObject? otherLast,
            double accumulatedWindingAngle,
            double windingDelta)
        {
            if (actingLast == null || otherLast == null)
                return 0;

            Vector2 targetPos = ((OsuHitObject)target.BaseObject).StackedPosition;
            Vector2 handPos = actingLast.GetEndCursorPosition();
            Vector2 otherPos = otherLast.GetEndCursorPosition();

            Vector2 movement = targetPos - handPos;
            float movementLengthSq = movement.LengthSquared;

            double crossingNow = 0;

            if (movementLengthSq > 1e-6f)
            {
                float t = Math.Clamp(Vector2.Dot(otherPos - handPos, movement) / movementLengthSq, 0f, 1f);
                Vector2 closestOnSegment = handPos + t * movement;
                float distance = (otherPos - closestOnSegment).Length;

                const double proximity_sigma = 100;
                double proximity = Math.Exp(-distance * distance / (2.0 * proximity_sigma * proximity_sigma));
                double betweenness = 4.0 * t * (1.0 - t);

                crossingNow = proximity * betweenness;
            }

            const double eps = 1e-6;

            double absAccum = Math.Abs(accumulatedWindingAngle);
            double absDelta = Math.Abs(windingDelta);

            double windingAmount = Math.Min(absAccum / Math.PI, 1.0);

            const double delta_scale = Math.PI / 6;
            double deltaMag = Math.Min(absDelta / delta_scale, 1.0);

            double denom = absAccum * absDelta;
            double align = denom < eps
                ? 0.5
                : 0.5 * (1.0 + (accumulatedWindingAngle * windingDelta) / denom);

            double tanglingRisk = windingAmount * deltaMag * align;

            const double tangling_weight = 0.6;
            double obstruction = crossingNow + tangling_weight * tanglingRisk * (1.0 - crossingNow);

            return Math.Pow(obstruction, 0.6);
        }

        public readonly struct AdvanceResult
        {
            public readonly OsuTouchDataIncrementalState NextState;
            public readonly OsuDifficultyHitObjectTouchData TouchData;

            public AdvanceResult(OsuTouchDataIncrementalState nextState, OsuDifficultyHitObjectTouchData touchData)
            {
                NextState = nextState;
                TouchData = touchData;
            }
        }
    }
}
