// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class TouchSpeedAimEvaluator
    {
        private const double hand_coordination_bonus = 1.775;
        private const double transition_to_drag_bonus = 1.075;
        private const double speed_aim_obstruction_max_bonus = 5.25;

        /// <summary>
        /// Evaluates the difficulty of aiming the current object with touch device, based on:
        /// <list type="bullet">
        /// <item><description>the standard speed aim difficulty from the previous object hit with the same hand,</description></item>
        /// <item><description>difficulty of coordinating between two hands,</description></item>
        /// <item><description>physical obstruction from the other hand,</description></item>
        /// <item><description>and difficulty of transitioning from tapping to dragging.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;

            if (osuCurrent.TouchData == null || osuCurrent.TouchData.Value.PerHandObject == null)
                return 0;

            var touchData = osuCurrent.TouchData.Value;

            bool isHandSwitch = touchData.ActingHand != touchData.PrevActingHand;

            bool isTransitionToDrag = touchData.Action is OsuTouchAction.OsuDragAction
                && touchData.PrevAction is not OsuTouchAction.OsuDragAction;

            double speedAimMultiplier = 1.0;

            // Reward the hand coordination required to switch aim hands across consecutive objects.
            if (isHandSwitch)
                speedAimMultiplier += hand_coordination_bonus;

            // Slightly reward the difficulty of suddenly transitioning from tapping to dragging on speed aim.
            if (isTransitionToDrag)
                speedAimMultiplier += transition_to_drag_bonus;

            // Heavily reward physical obstruction created by overlapping hands during speed aim.
            speedAimMultiplier += touchData.ObstructionFactor * speed_aim_obstruction_max_bonus;

            return SpeedAimEvaluator.EvaluateDifficultyOf(touchData.PerHandObject) * speedAimMultiplier;
        }
    }
}
