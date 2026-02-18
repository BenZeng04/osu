// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuStrainSkill
    {
        private const double skill_multiplier_aim = 25.85;
        private const double skill_multiplier_speed = 1.35;
        private const double skill_multiplier_total = 1.0;
        private const double mean_exponent = 1.2;
        private const double strain_decay_base_aim = 0.15;
        private const double strain_decay_base_speed = 0.3;

        public readonly bool IncludeSliders;

        private readonly List<double> sliderStrains = new List<double>();

        private StrainState strainState;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        public readonly struct StrainState
        {
            public readonly double CurrentAimStrain;
            public readonly double CurrentSpeedStrain;

            public StrainState(double currentAimStrain, double currentSpeedStrain)
            {
                CurrentAimStrain = currentAimStrain;
                CurrentSpeedStrain = currentSpeedStrain;
            }
        }

        private static double strainDecayAim(double ms) => Math.Pow(strain_decay_base_aim, ms / 1000);
        private static double strainDecaySpeed(double ms) => Math.Pow(strain_decay_base_speed, ms / 1000);

        /// <summary>
        /// Computes the next aim strain state by applying strain decay and evaluating the current object's aim and speed-aim difficulty.
        /// </summary>
        public static StrainState AdvanceStrainState(StrainState strainState, DifficultyHitObject current, bool isTouch, bool includeSliders, bool suppressSpeedComponent = false)
        {
            double decayAim = strainDecayAim(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            double decaySpeed = strainDecaySpeed(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            double aimDifficulty;
            double speedDifficulty;

            if (isTouch)
            {
                aimDifficulty = TouchAimEvaluator.EvaluateDifficultyOf(current, includeSliders);
                speedDifficulty = TouchSpeedAimEvaluator.EvaluateDifficultyOf(current);
            }
            else
            {
                aimDifficulty = AimEvaluator.EvaluateDifficultyOf(current, includeSliders);
                speedDifficulty = SpeedAimEvaluator.EvaluateDifficultyOf(current);
            }

            if (suppressSpeedComponent)
                speedDifficulty = 0;

            double newAimStrain = strainState.CurrentAimStrain * decayAim;
            newAimStrain += aimDifficulty * (1 - decayAim) * skill_multiplier_aim;

            double newSpeedStrain = strainState.CurrentSpeedStrain * decaySpeed;
            newSpeedStrain += speedDifficulty * (1 - decaySpeed) * skill_multiplier_speed;

            return new StrainState(newAimStrain, newSpeedStrain);
        }

        /// <summary>
        /// Combines the aim and speed components of a <see cref="StrainState"/> into a single overall strain value.
        /// </summary>
        public static double ComputeOverallStrain(StrainState strainState)
        {
            double totalStrain = DifficultyCalculationUtils.Norm(mean_exponent, strainState.CurrentAimStrain, strainState.CurrentSpeedStrain);
            return totalStrain * skill_multiplier_total;
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) =>
            DifficultyCalculationUtils.Norm(mean_exponent,
                strainState.CurrentAimStrain * strainDecayAim(time - current.Previous(0).StartTime),
                strainState.CurrentSpeedStrain * strainDecaySpeed(time - current.Previous(0).StartTime)) * skill_multiplier_total;

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            bool isTouch = Mods.Any(m => m is OsuModTouchDevice);
            bool suppressSpeedComponent = Mods.Any(m => m is OsuModRelax);

            strainState = AdvanceStrainState(
                strainState,
                current,
                isTouch,
                IncludeSliders,
                suppressSpeedComponent
            );
            double currentStrain = ComputeOverallStrain(strainState);

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            return currentStrain;
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders(double difficultyValue)
            => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, difficultyValue);
    }
}
