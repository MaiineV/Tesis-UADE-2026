using System;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos.Triggers.Concretes
{
    /// <summary>
    /// "Ganás N oro cada vez que entrás a una sala" — primer concrete de los hooks
    /// genéricos (no-combo). Hook: <see cref="IOnRoomEnteredPassiveTrigger"/>.
    /// Con <see cref="RoomIdFilter"/> el designer restringe a un template de sala.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AddGoldOnRoomEntered : IOnRoomEnteredPassiveTrigger
    {
        [Title("Gold Amount")]
        [InfoBox("Cuánto oro se suma al entrar a una sala. ReadConstantInt(2) = '+2 oro por sala'.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Amount;

        [Title("Room Filter")]
        [Tooltip("Vacío = cualquier sala. Con valor, solo dispara cuando ctx.RoomId " +
                 "coincide exactamente (id de template, no de instancia).")]
        [OdinSerialize]
        public string RoomIdFilter;

        public void OnRoomEntered(ComboPassiveContext ctx)
        {
            if (ctx?.Scratch == null) return;
            if (!string.IsNullOrEmpty(RoomIdFilter) && ctx.RoomId != RoomIdFilter) return;

            int amount = Amount != null ? Amount.Read(ctx.Effect) : 0;
            ctx.Scratch.BonusGold += amount;
        }
    }
}
