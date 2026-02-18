// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    /// <summary>
    /// Touch data produced by <see cref="OsuTouchDataIncrementalState.Advance"/>, used to evaluate aim and speed difficulty of an <see cref="OsuDifficultyHitObject"/> hit with touch device.
    /// </summary>
    public readonly struct OsuDifficultyHitObjectTouchData
    {
        /// <summary>
        /// The touch action used to hit this <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public readonly OsuTouchAction Action;

        /// <summary>
        /// The hand used to aim this <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public readonly OsuTouchHand ActingHand;

        /// <summary>
        /// The touch action used on the previous <see cref="OsuDifficultyHitObject"/>. Null for the first object.
        /// </summary>
        public readonly OsuTouchAction? PrevAction;

        /// <summary>
        /// The hand that aimed the previous <see cref="OsuDifficultyHitObject"/>. Null for the first object.
        /// </summary>
        public readonly OsuTouchHand? PrevActingHand;

        /// <summary>
        /// A synthetic <see cref="OsuDifficultyHitObject"/> built from the objects hit using the same <see cref="ActingHand"/>.
        /// Null when there is no prior object hit with this <see cref="ActingHand"/>.
        /// </summary>
        public readonly OsuDifficultyHitObject? PerHandObject;

        /// <summary>
        /// A factor in [0, 1] representing how much the <see cref="PrevActingHand"/> physically obstructs the <see cref="ActingHand"/>'s path to this <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public readonly double ObstructionFactor;

        public OsuDifficultyHitObjectTouchData(
            OsuTouchAction action,
            OsuTouchHand actingHand,
            OsuTouchAction? prevAction,
            OsuTouchHand? prevActingHand,
            OsuDifficultyHitObject? perHandObject,
            double obstructionFactor)
        {
            Action = action;
            ActingHand = actingHand;
            PrevAction = prevAction;
            PrevActingHand = prevActingHand;
            PerHandObject = perHandObject;
            ObstructionFactor = obstructionFactor;
        }
    }
}
