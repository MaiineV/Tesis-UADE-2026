using System;
using Patterns;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// "Evil": el encantamiento se auto-quita si el dado no participa en un combo
    /// durante <see cref="MaxTurnsUnused"/> turnos seguidos. Cubre el ejemplo del
    /// GDD: "Si no uso el dado en 3 combos EXPLOTA". MVP simplifica "uso del
    /// dado en combo" como "algún combo matcheó en el turno".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Estado per-slot.</b> El counter vive en <see cref="IDiceEnchantmentRuntime"/>
    /// (Phase 4) keyed por <c>(slot, CounterKey)</c>. El trigger es stateless;
    /// solo configura el threshold.
    /// </para>
    /// <para>
    /// <b>Hooks:</b>
    /// <list type="bullet">
    /// <item><description><c>OnEnchantmentApplied</c> — resetea counter a 0.</description></item>
    /// <item><description><c>OnComboMatched</c> — resetea counter (asume que el dado participó).</description></item>
    /// <item><description><c>OnTurnFinished</c> — incrementa counter. Si llega a <see cref="MaxTurnsUnused"/>, llama <c>RemoveEnchantment</c>.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ExplodeIfUnusedForTurns
        : IOnEnchantmentAppliedTrigger,
          IOnComboMatchedTrigger,
          IOnTurnFinishedTrigger
    {
        private const string CounterKey = "explode_if_unused";

        [Title("Threshold")]
        [InfoBox("Cuántos turnos seguidos sin matchear combo antes de auto-quitarse. " +
                 "GDD: 3.")]
        [MinValue(1)]
        public int MaxTurnsUnused = 3;

        public void OnEnchantmentApplied(EnchantmentTriggerContext ctx)
        {
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var rt) || rt == null) return;
            rt.ResetCounter(ctx.Slot, CounterKey);
        }

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var rt) || rt == null) return;
            // Asume que el dado participó en el combo (MVP simplification).
            rt.ResetCounter(ctx.Slot, CounterKey);
        }

        public void OnTurnFinished(EnchantmentTriggerContext ctx)
        {
            if (!ServiceLocator.TryGetService<IDiceEnchantmentRuntime>(out var rt) || rt == null) return;
            int count = rt.IncrementCounter(ctx.Slot, CounterKey);
            if (count >= MaxTurnsUnused)
            {
                rt.RemoveEnchantment(ctx.Slot);
            }
        }
    }
}
