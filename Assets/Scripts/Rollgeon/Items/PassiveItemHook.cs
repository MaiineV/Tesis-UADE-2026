using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Items
{
    [Serializable, HideReferenceObjectPicker]
    public class PassiveItemHook
    {
        [InfoBox("Evento que dispara el efecto pasivo. Ej: OnTurnStarted, OnComboMatched, OnDamageResolved.")]
        public EventName TriggerEvent;

        [OdinSerialize]
        public EffectData Effect = new();

        [InfoBox("Modificadores que se aplican mientras el item este en el inventario. " +
                 "Se remueven automaticamente si el item se pierde.")]
        [OdinSerialize]
        public List<PersistentModifierDef> PersistentModifiers = new();
    }
}
