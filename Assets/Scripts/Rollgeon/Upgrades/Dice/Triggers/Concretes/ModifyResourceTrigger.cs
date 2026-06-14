using System;
using Rollgeon.Dice;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Trigger genérico parametrizable desde el editor: aplica una operación
    /// (Add/Subtract/Multiply/Set) sobre un recurso (oro o stat) cuando dispara el
    /// evento configurado. Reemplaza a los concretos hardcodeados (AddGoldOnStraight,
    /// AddShieldOnSpecificCombo, etc.).
    /// </summary>
    /// <remarks>
    /// El <see cref="Filter"/> solo aplica cuando <see cref="When"/> = ComboMatched.
    /// El valor numérico se lee vía <see cref="Amount"/> (literal, stat, combo counter…).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ModifyResourceTrigger
        : IOnComboMatchedTrigger, IOnRollResolvedTrigger, IOnDiceRolledTrigger
    {
        [Title("Cuándo dispara")]
        public TriggerWhen When = TriggerWhen.ComboMatched;

        [Title("Filtro de combo")]
        [InfoBox("Solo se evalúa cuando When = ComboMatched.")]
        [ShowIf(nameof(When), TriggerWhen.ComboMatched)]
        public ComboFilter Filter = new ComboFilter();

        [Title("Qué modifica")]
        public ResourceTarget Target = ResourceTarget.Gold;

        public ResourceOperation Operation = ResourceOperation.Add;

        [Title("Cantidad")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Amount;

        [Title("Condición (opcional)")]
        [InfoBox("Condición extra además del filtro de combo. NoComboMatched: solo si el " +
                 "dado no entró en combo (típico con When=RollResolved). DieOnMaxFace: solo " +
                 "si el dado carrier muestra su cara máxima.")]
        public TriggerCondition Condition = TriggerCondition.None;

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (When != TriggerWhen.ComboMatched) return;
            if (ctx?.Scratch == null) return;
            if (!Filter.Matches(ctx.ComboId)) return;
            if (!PassesCondition(ctx)) return;
            ApplyTo(ctx.Scratch, ctx.Effect);
        }

        public void OnRollResolved(EnchantmentTriggerContext ctx)
        {
            if (When != TriggerWhen.RollResolved) return;
            if (ctx?.Scratch == null) return;
            if (!PassesCondition(ctx)) return;
            ApplyTo(ctx.Scratch, ctx.Effect);
        }

        public void OnDiceRolled(EnchantmentTriggerContext ctx)
        {
            if (When != TriggerWhen.DiceRolled) return;
            if (ctx?.Scratch == null) return;
            if (!PassesCondition(ctx)) return;
            ApplyTo(ctx.Scratch, ctx.Effect);
        }

        private bool PassesCondition(EnchantmentTriggerContext ctx)
        {
            switch (Condition)
            {
                case TriggerCondition.NoComboMatched:
                    return !(ctx.Effect != null
                             && ctx.Effect.ComboResult.HasValue
                             && ctx.Effect.ComboResult.Value.IsMatch);
                case TriggerCondition.DieOnMaxFace:
                    return IsCarrierOnMaxFace(ctx);
                default:
                    return true;
            }
        }

        private static bool IsCarrierOnMaxFace(EnchantmentTriggerContext ctx)
        {
            var dice = ctx.Effect?.DiceResult;
            if (dice == null) return false;
            int idx = ctx.Slot.BagSlotIndex;
            if (idx < 0 || idx >= dice.Count) return false;
            return dice[idx] == ctx.Slot.Type.MaxFace();
        }

        private void ApplyTo(EnchantmentScratch scratch, Rollgeon.Effects.EffectContext effect)
        {
            if (scratch == null) return;
            int amount = Amount != null ? Amount.Read(effect) : 0;
            scratch.Modify(Target, Operation, amount);
        }
    }
}
