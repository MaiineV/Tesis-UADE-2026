using System;
using Patterns;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Si este dado se holdea entre rolls, su resultado aumenta +1 por roll
    /// holdeado (max +3). Hook: <c>OnEnchantmentApplied</c>,
    /// <c>OnRollResolved</c>. Encantamiento "Ancla".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Estado.</b> Usa <see cref="IDiceEnchantmentRuntime"/> counter para
    /// trackear cuantos rolls consecutivos se holdeo el dado.
    /// </para>
    /// <para>
    /// <b>TODO Phase 4:</b> detectar si el dado fue holdeado (no rerolled) en
    /// este roll. Requiere integracion con el roll service que exponga el
    /// estado de hold per-die. Actualmente la infraestructura de counters
    /// queda lista; la deteccion de hold y aplicacion del bonus se conectan
    /// en Phase 4.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AnchorAccumulate
        : IOnEnchantmentAppliedTrigger,
          IOnRollResolvedTrigger
    {
        private const string CounterKey = "anchor_held_count";

        [Title("Max Accumulation")]
        [InfoBox("Maximo de bonus acumulable por holds consecutivos. " +
                 "Default: +3. Cada roll holdeado suma +1 al resultado.")]
        [MinValue(1)]
        public int MaxAccumulation = 3;

        public void OnEnchantmentApplied(EnchantmentTriggerContext ctx)
        {
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var rt) || rt == null) return;
            rt.ResetCounter(ctx.Slot, CounterKey);
        }

        public void OnRollResolved(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var rt) || rt == null) return;

            // TODO Phase 4: detectar si el dado fue holdeado (no rerolled) en este roll.
            // Si fue holdeado, incrementar counter (capped a MaxAccumulation).
            // Si fue rerolled, resetear counter a 0.
            // Aplicar bonus acumulado: ctx.Scratch.BonusComboDamage += counter value.
            //
            // Pseudocodigo Phase 4:
            // bool wasHeld = rollService.WasDieHeld(ctx.Slot.BagSlotIndex);
            // if (wasHeld)
            // {
            //     int count = rt.IncrementCounter(ctx.Slot, CounterKey);
            //     if (count > MaxAccumulation)
            //     {
            //         rt.ResetCounter(ctx.Slot, CounterKey);
            //         for (int i = 0; i < MaxAccumulation; i++) rt.IncrementCounter(ctx.Slot, CounterKey);
            //         count = MaxAccumulation;
            //     }
            //     ctx.Scratch.BonusComboDamage += count;
            // }
            // else
            // {
            //     rt.ResetCounter(ctx.Slot, CounterKey);
            // }

            Debug.Log(
                $"[AnchorAccumulate] Roll resolved para slot {ctx.Slot.BagSlotIndex} " +
                $"— hold detection pendiente de integracion con roll service.");
        }
    }
}