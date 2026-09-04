using System;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades.Dice.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Effects
{
    /// <summary>
    /// Cambia cuánto vale la cara del dado CARRIER en Σcaras de la fórmula v3 escribiendo
    /// <see cref="EnchantmentScratch.AddFaceDelta"/> para su bag slot: "no suma" (−cara),
    /// "vale doble" (+cara), "mitad", "triple si impar"… El breakdown muestra al dado ya
    /// transformado (+0 / +12), no la cara real seguida de un proc que la deshace — que era lo
    /// que hacía <c>EffAddComboBonus(ReadCarrierRollDelta)</c> y el tester leía como "suma y
    /// resta a la vez".
    /// </summary>
    /// <remarks>
    /// Solo toca el daño: la detección del combo sigue viendo la cara tirada. Es un
    /// <see cref="IComboScratchWriter"/>, así que vale en <c>ComboMatched</c> (preview) y en
    /// <c>ComboPlayed</c> (Frágil resuelve la moneda recién al jugar). Autorar con
    /// <c>RequireCarrierParticipates</c>: un dado fuera del combo no está en Σcaras.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffMutateCarrierFace : BaseEffect,
        IComboScratchWriter, IRequiresTriggerContext<ScratchTriggerContext>
    {
        [Title("Face Delta")]
        [Tooltip("Delta sobre la cara del carrier. Normalmente ReadCarrierRollDelta (Exclude, Double, " +
                 "DoubleMaxHalveRest, TripleOddZeroEven…); una constante negativa también sirve.")]
        [OdinSerialize, SerializeReference]
        private EffectIntReader _delta = new ReadCarrierRollDelta();

        protected override bool ShowSelection => false;

        public EffectIntReader Delta
        {
            get => _delta;
            set => _delta = value;
        }

        public override string GetEffectName() => "Mutate Carrier Face";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null
                || !context.TryGetTriggerContext<ScratchTriggerContext>(out var trig)
                || trig.Scratch == null || trig.Slot == null)
            {
                Debug.LogWarning("[EffMutateCarrierFace] sin ScratchTriggerContext con Slot — este efecto " +
                                 "solo funciona dentro de un dispatch de encantamiento de dados.");
                return false;
            }

            int delta = _delta?.Read(context) ?? 0;
            trig.Scratch.AddFaceDelta(trig.Slot.Value.BagSlotIndex, delta);
            return true;
        }
    }
}
