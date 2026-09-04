using System;
using System.Collections.Generic;
using Rollgeon.Combat.Damage;
using Rollgeon.Upgrades;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>Qué dado del combo mueve <see cref="EffMoveDieToMultiplier"/>. APPEND-ONLY.</summary>
    public enum ContributingDiePick
    {
        /// <summary>La cara más alta entre los dados que forman el combo (empate: el primero).</summary>
        Highest = 0,

        /// <summary>La cara más baja entre los dados que forman el combo (empate: el primero).</summary>
        Lowest = 1,
    }

    /// <summary>
    /// Marca un dado del combo jugado para que su cara cuente en M (bono aditivo del
    /// multiplicador) y NO en Σcaras de N — Fuente Mágica (GDD: "el dado más alto del combo
    /// suma su cara al multiplicador, no al daño base"). Solo escribe
    /// <see cref="Upgrades.Dice.EnchantmentScratch.MoveDieToMultiplier"/>; la fórmula
    /// (<c>PlayerComboDamage.Resolve</c>) mueve la cara y el desglose muestra el dado volando
    /// a M en vez de entrar a N y restarse después.
    /// </summary>
    /// <remarks>
    /// Reemplaza el par <c>EffAddComboMultiplier(ReadHighestContributingDie)</c> +
    /// <c>EffAddComboBonus(Subtract)</c>: el número final era el mismo, pero la animación
    /// mostraba el dado sumando en N y después un "−X" del item — el jugador leía que
    /// contaba en los dos (playtest 2026-09-04). Solo funciona dentro de un dispatch de
    /// trigger de combo (<see cref="ScratchTriggerContext"/>), como el resto de la familia
    /// scratch. Sin combo con índices no hay dado que mover: no-op.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffMoveDieToMultiplier : BaseEffect,
        IComboScratchWriter, IRequiresTriggerContext<ScratchTriggerContext>
    {
        [Title("Move Die To Multiplier")]
        [Tooltip("Qué dado del combo sale de N y entra a M. Fuente Mágica: Highest.")]
        [SerializeField]
        private ContributingDiePick _pick = ContributingDiePick.Highest;

        protected override bool ShowSelection => false;

        public ContributingDiePick Pick
        {
            get => _pick;
            set => _pick = value;
        }

        public override string GetEffectName() => "Move Die To Multiplier";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null
                || !context.TryGetTriggerContext<ScratchTriggerContext>(out var trig)
                || trig.Scratch == null)
            {
                Debug.LogWarning("[EffMoveDieToMultiplier] sin ScratchTriggerContext — este efecto " +
                                 "solo funciona dentro de un dispatch de trigger de combo.");
                return false;
            }

            int local = FindLocalIndex(context, _pick);
            if (local < 0) return true;

            int bagSlot = ContributingDiceResolver.ResolveBagSlot(local, context.KeptDiceOriginalIndices);
            trig.Scratch.MoveDieToMultiplier(bagSlot);
            return true;
        }

        /// <summary>
        /// Índice local (mismo espacio que <c>ComboResult.ContributingIndices</c> sobre
        /// <c>KeptDice ?? DiceResult</c>, como <c>ReadHighestContributingDie</c>) del dado
        /// elegido; -1 sin combo, sin índices o sin dados.
        /// </summary>
        public static int FindLocalIndex(EffectContext context, ContributingDiePick pick)
        {
            if (context?.ComboResult is not { IsMatch: true } combo) return -1;
            var indices = combo.ContributingIndices;
            if (indices == null || indices.Count == 0) return -1;

            IReadOnlyList<int> dice = context.KeptDice != null && context.KeptDice.Count > 0
                ? context.KeptDice
                : context.DiceResult;
            if (dice == null || dice.Count == 0) return -1;

            int bestIndex = -1;
            int bestFace = 0;
            for (int i = 0; i < indices.Count; i++)
            {
                int idx = indices[i];
                if (idx < 0 || idx >= dice.Count) continue;
                int face = dice[idx];
                bool better = bestIndex < 0
                              || (pick == ContributingDiePick.Highest ? face > bestFace : face < bestFace);
                if (!better) continue;
                bestIndex = idx;
                bestFace = face;
            }
            return bestIndex;
        }
    }
}
