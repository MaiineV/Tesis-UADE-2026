using System;
using Patterns;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Fuerza un reroll completo en un turno específico del combate (default: turno 2).
    /// Hooks: <c>OnEnchantmentApplied</c>, <c>OnTurnFinished</c>.
    /// Encantamiento "Torpe" (negativo).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Estado.</b> Usa <see cref="IDiceEnchantmentRuntime"/> counter para trackear
    /// el turno actual del combate. El counter se resetea al aplicar el encantamiento
    /// y se incrementa al finalizar cada turno.
    /// </para>
    /// <para>
    /// <b>TODO Phase 4:</b> integrar con el roll service para forzar el reroll
    /// efectivamente. Actualmente solo loguea un warning cuando el turno coincide.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ForceRerollOnTurn
        : IOnEnchantmentAppliedTrigger,
          IOnTurnFinishedTrigger
    {
        private const string CounterKey = "force_reroll_turn";

        [Title("Trigger Turn")]
        [InfoBox("En qué turno del combate se fuerza el reroll. Default: 2.")]
        [MinValue(1)]
        public int TriggerOnTurn = 2;

        public void OnEnchantmentApplied(EnchantmentTriggerContext ctx)
        {
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var rt) || rt == null) return;
            rt.ResetCounter(ctx.Slot, CounterKey);
        }

        public void OnTurnFinished(EnchantmentTriggerContext ctx)
        {
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var rt) || rt == null) return;
            int turn = rt.IncrementCounter(ctx.Slot, CounterKey);

            if (turn == TriggerOnTurn)
            {
                // TODO Phase 4: integrar con el roll service para forzar el reroll.
                // Por ahora solo se loguea; la estructura de datos queda lista.
                Debug.LogWarning(
                    $"[ForceRerollOnTurn] Turno {turn} alcanzado para slot {ctx.Slot.BagSlotIndex} " +
                    $"— reroll forzado pendiente de integración con roll service.");
            }
        }
    }
}