using System;
using Rollgeon.PreConditions;
using Rollgeon.Upgrades.Dice.Triggers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.PreConditions
{
    /// <summary>
    /// "El dado carrier participó del combo" como PreCondition, para triggers que
    /// necesitan reaccionar en AMBOS casos (participó / no participó) desde un mismo
    /// bridge con <c>RequireCarrierParticipates=false</c>: Solitario paga si el dado
    /// quedó afuera; Racha incrementa si entró y resetea si no.
    /// </summary>
    /// <remarks>
    /// Misma regla que <see cref="ExecuteEffectsOnDiceEvent.CarrierParticipates(Rollgeon.Effects.EffectContext,int)"/>:
    /// sin ComboResult o sin índices de contribución el dado NO participa.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcCarrierParticipates : BasePreCondition, IReadsTriggerEffect
    {
        [Tooltip("true = dispara cuando el dado NO participó del combo.")]
        public bool Negate;

        public override string ConditionName => Negate ? "Carrier NO participa" : "Carrier participa";

        public override bool Evaluate(PreConditionContext context)
        {
            var eff = context?.Effect;
            if (eff == null) return false;
            if (!eff.TryGetTriggerContext<ScratchTriggerContext>(out var trig) || trig.Slot == null)
                return false;

            // Sin combo resuelto no hay "adentro" ni "afuera": conservador en ambos sentidos
            // (un Negate no debe pagar Solitario en una tirada sin combo).
            if (eff.ComboResult == null || !eff.ComboResult.Value.IsMatch) return false;

            bool participates = ExecuteEffectsOnDiceEvent.CarrierParticipates(eff, trig.Slot.Value.BagSlotIndex);
            return Negate ? !participates : participates;
        }
    }
}
