using System;
using Patterns;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// "Cargado": una vez por combate, el dado puede re-tirarse y se queda con el
    /// resultado mas alto. Hook: <c>OnEnchantmentApplied</c> (reset),
    /// <c>OnRollResolved</c> (consume).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Estado per-slot.</b> El counter <c>reroll_keep_highest_used</c> vive en
    /// <see cref="IDiceEnchantmentRuntime"/> keyed por <c>(slot, key)</c>.
    /// </para>
    /// <para>
    /// <b>MVP placeholder.</b> La mecanica de reroll real requiere integracion con
    /// el roll UI/service (Phase 4). Por ahora el trigger solo trackea el counter
    /// y no modifica el resultado.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class RerollKeepHighest
        : IOnEnchantmentAppliedTrigger,
          IOnRollResolvedTrigger
    {
        private const string CounterKey = "reroll_keep_highest_used";

        public void OnEnchantmentApplied(EnchantmentTriggerContext ctx)
        {
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var rt) || rt == null) return;
            rt.ResetCounter(ctx.Slot, CounterKey);
        }

        public void OnRollResolved(EnchantmentTriggerContext ctx)
        {
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var rt) || rt == null) return;

            int used = rt.GetCounter(ctx.Slot, CounterKey);
            if (used > 0) return; // ya se uso esta run

            // TODO Phase 4: integrar con roll service para ejecutar el reroll real,
            // comparar ambos resultados y quedarse con el mayor. Por ahora el
            // counter se marca como usado sin efecto mecanico.
        }
    }
}