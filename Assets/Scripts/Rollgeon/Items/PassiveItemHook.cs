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
        [InfoBox("Evento del bus que dispara el efecto. Usables: OnTurnStarted, OnTurnFinished, " +
                 "OnRollStarted, OnDiceRolled, OnRollResolved, OnDamageIncoming, OnDamageOutgoing, " +
                 "OnComboCrossed, OnWeaknessHit, OnPlayerHealthChanged.")]
        [InfoBox("El hook filtra por args[0] == Guid del jugador (convención §18). Un evento que NO " +
                 "arranca con un Guid dispara siempre — no hay a quién comparar.",
                 InfoMessageType.Warning)]
        public EventName TriggerEvent;

        [OdinSerialize]
        public EffectData Effect = new();

        [InfoBox("Modificadores que se aplican mientras el item este en el inventario. " +
                 "Se remueven automaticamente si el item se pierde.")]
        [OdinSerialize]
        public List<PersistentModifierDef> PersistentModifiers = new();
    }
}
