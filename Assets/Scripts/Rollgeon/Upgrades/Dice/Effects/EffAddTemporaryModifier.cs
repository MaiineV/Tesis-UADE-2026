using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Effects
{
    /// <summary>Stat que puede recibir un modificador temporal desde el canal dados.</summary>
    public enum TemporaryModifierStat
    {
        Attack = 0,
        MoveRange = 1,
    }

    /// <summary>
    /// Modificador aditivo "hasta el final del turno" sobre un stat del dueño (Carga: +1 ATQ por
    /// casilla recorrida; Torbellino: +2 de Movimiento en esa acción). Usa el ciclo de vida
    /// <see cref="ModifierLifetime.Turns"/> con duración 1 sobre <c>OnTurnFinished</c>: el
    /// primer fin de turno que llega es el del jugador, así que el bono muere solo.
    /// </summary>
    /// <remarks>
    /// Sin tope por copia: varias copias disparan cada una su propio modificador (Carga: "cada
    /// copia añade +1 por casilla"). Si un encantamiento quiere stacking redundante, que lo
    /// gatee con <see cref="OnlyFirstCopy"/>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffAddTemporaryModifier : BaseEffect, IRequiresTriggerContext<ScratchTriggerContext>
    {
        [Tooltip("Stat del dueño que recibe el bono.")]
        public TemporaryModifierStat Stat = TemporaryModifierStat.Attack;

        [Tooltip("Monto fijo. Se ignora si hay Reader.")]
        public int Amount = 1;

        [OdinSerialize, SerializeReference]
        [Tooltip("Monto dinámico (ej. ReadTilesTraversed). Null = usa Amount.")]
        public EffectIntReader Reader;

        [MinValue(1)]
        [Tooltip("Turnos que dura (1 = hasta el fin del turno actual del jugador).")]
        public int DurationTurns = 1;

        [Tooltip("Solo la primera copia viva del encantamiento aplica (stacking redundante).")]
        public bool OnlyFirstCopy;

        protected override bool ShowSelection => false;

        public override string GetEffectName() => "Add Temporary Modifier";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;
            var target = context.SourceGuid;
            if (target == Guid.Empty) return false;

            Guid sourceId = Guid.Empty;
            if (context.TryGetTriggerContext<ScratchTriggerContext>(out var trig) && trig.Slot != null)
            {
                if (OnlyFirstCopy)
                {
                    MovementLaneCopies.Count(trig.Slot.Value, out bool first);
                    if (!first) return true;
                }
                sourceId = SourceIdFor(trig.Slot.Value);
            }

            int amount = Reader != null ? Reader.Read(context) : Amount;
            if (amount == 0) return true;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                Debug.LogWarning("[EffAddTemporaryModifier] AttributesManager no registrado.");
                return false;
            }

            var modifier = new Modifier<int>(amount, ModifierOperation.Add, DurationTurns,
                target, sourceId, ModifierDirection.Intrinsic, ModifierLifetime.Turns,
                EventName.OnTurnFinished);

            return Stat switch
            {
                TemporaryModifierStat.MoveRange => attrs.AddModifier<MoveRange, int>(target, modifier),
                _ => attrs.AddModifier<Attack, int>(target, modifier),
            };
        }

        /// <summary>Identidad estable por (dado, slot): permite auditar/remover por fuente.</summary>
        private static Guid SourceIdFor(EnchantmentSlotRef slot)
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(slot.BagSlotIndex).CopyTo(bytes, 0);
            BitConverter.GetBytes(slot.EnchantmentSlotIndex).CopyTo(bytes, 4);
            bytes[8] = 0x5E; bytes[9] = 0x7C; // marca "enchantment slot"
            return new Guid(bytes);
        }
    }
}
