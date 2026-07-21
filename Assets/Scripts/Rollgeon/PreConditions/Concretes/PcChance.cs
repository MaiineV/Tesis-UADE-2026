using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>Forma de expresar la probabilidad de <see cref="PcChance"/>.</summary>
    public enum ChanceMode
    {
        /// <summary>Probabilidad directa 0..1 (0.5 = 50%).</summary>
        Percent01,

        /// <summary>"1 en N" (5 = 20%). Forma del GDD para dados de suerte.</summary>
        OneInN,
    }

    /// <summary>
    /// Pasa con la probabilidad configurada. Reemplaza los checks de RNG hardcodeados
    /// de los triggers legacy (ChanceToNotCount, LuckyChanceComboBonus) — y a diferencia
    /// de ellos tiene seam determinística para tests.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcChance : BasePreCondition
    {
        public ChanceMode Mode = ChanceMode.Percent01;

        [ShowIf(nameof(Mode), ChanceMode.Percent01)]
        [Range(0f, 1f)]
        public float Chance = 0.5f;

        [ShowIf(nameof(Mode), ChanceMode.OneInN)]
        [MinValue(1)]
        public int OneIn = 5;

        /// <summary>
        /// Fuente de aleatoriedad [0,1). Reemplazable SOLO en tests (determinismo);
        /// restaurar con <see cref="ResetRandomSource"/> en el teardown.
        /// </summary>
        public static Func<float> RandomSource = DefaultRandom;

        public static void ResetRandomSource() => RandomSource = DefaultRandom;

        private static float DefaultRandom() => UnityEngine.Random.value;

        public override string ConditionName =>
            Mode == ChanceMode.OneInN ? $"Chance 1 en {OneIn}" : $"Chance {Chance:P0}";

        public override bool Evaluate(PreConditionContext context)
        {
            float p = Mode == ChanceMode.OneInN
                ? 1f / Mathf.Max(1, OneIn)
                : Chance;
            return RandomSource() < p;
        }
    }
}
