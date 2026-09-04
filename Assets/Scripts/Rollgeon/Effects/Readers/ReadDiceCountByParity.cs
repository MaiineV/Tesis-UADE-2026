using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    public enum DiceParity
    {
        Odd,
        Even,
    }

    /// <summary>Qué dados cuenta el reader. APPEND-ONLY: se serializa el int del enum.</summary>
    public enum DiceParityScope
    {
        /// <summary>Toda la tirada (<c>DiceResult</c>), holdeados o no. Default — preserva
        /// los assets ya autorados (Odin no corre field initializers: un campo ausente
        /// deserializa 0).</summary>
        WholeRoll = 0,

        /// <summary>
        /// Solo los dados que FORMAN el combo (<c>ComboResult.ContributingIndices</c> sobre
        /// <c>KeptDice ?? DiceResult</c>, mismo espacio de índices que
        /// <c>ReadHighestContributingDie</c>). Sin combo → 0. Bolsa del Impar: el oro se paga
        /// por dado impar que participa del combo, no por tirar impares (playtest 2026-09-04).
        /// </summary>
        ComboDice = 1,
    }

    /// <summary>
    /// Cantidad de dados con la paridad pedida × <see cref="PerDieAmount"/>. Según
    /// <see cref="Scope"/> mira toda la tirada o solo los dados que forman el combo. Lleva su
    /// propio factor porque <c>EffModifyGold</c> no escala readers.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadDiceCountByParity : EffectIntReader
    {
        [Tooltip("Qué dados cuentan. Bolsa del Impar: Odd.")]
        public DiceParity Parity = DiceParity.Odd;

        [Tooltip("WholeRoll = toda la tirada (hook de OnRollResolved). ComboDice = solo los dados " +
                 "que forman el combo jugado (hook ComboPlayed) — sin combo vale 0. Bolsa del Impar: ComboDice.")]
        public DiceParityScope Scope = DiceParityScope.WholeRoll;

        [Tooltip("Cuánto vale cada dado que cumple. Bolsa del Impar: 3 (oro).")]
        public int PerDieAmount = 3;

        public override int Read(EffectContext context)
        {
            if (context == null) return 0;
            int wanted = Parity == DiceParity.Odd ? 1 : 0;
            int count = Scope == DiceParityScope.ComboDice
                ? CountComboDice(context, wanted)
                : CountAll(context.DiceResult, wanted);
            return count * PerDieAmount;
        }

        private static int CountAll(IReadOnlyList<int> dice, int wanted)
        {
            if (dice == null) return 0;
            int count = 0;
            for (int i = 0; i < dice.Count; i++)
                if (Matches(dice[i], wanted)) count++;
            return count;
        }

        private static int CountComboDice(EffectContext context, int wanted)
        {
            if (context.ComboResult is not { IsMatch: true } combo) return 0;
            var indices = combo.ContributingIndices;
            if (indices == null || indices.Count == 0) return 0;

            var dice = context.KeptDice != null && context.KeptDice.Count > 0
                ? context.KeptDice
                : context.DiceResult;
            if (dice == null || dice.Count == 0) return 0;

            int count = 0;
            for (int i = 0; i < indices.Count; i++)
            {
                int idx = indices[i];
                if (idx < 0 || idx >= dice.Count) continue;
                if (Matches(dice[idx], wanted)) count++;
            }
            return count;
        }

        private static bool Matches(int face, int wanted) => Math.Abs(face) % 2 == wanted;
    }
}
