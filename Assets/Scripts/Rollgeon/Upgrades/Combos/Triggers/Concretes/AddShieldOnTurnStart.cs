using System;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos.Triggers.Concretes
{
    /// <summary>
    /// "Al empezar tu turno ganás N de escudo" — ejercita el hook de turno (que el
    /// service ya filtró a turnos del player) y el lado de shield del applier.
    /// Hook: <see cref="IOnTurnStartedPassiveTrigger"/>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddShieldOnTurnStart : IOnTurnStartedPassiveTrigger
    {
        [Title("Shield Amount")]
        [InfoBox("Cuánto escudo se suma al player al inicio de su turno. ReadConstantInt(1) = '+1 escudo por turno'.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Amount;

        public void OnTurnStarted(ComboPassiveContext ctx)
        {
            if (ctx?.Scratch == null) return;
            int amount = Amount != null ? Amount.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusShield += amount;
        }
    }
}
