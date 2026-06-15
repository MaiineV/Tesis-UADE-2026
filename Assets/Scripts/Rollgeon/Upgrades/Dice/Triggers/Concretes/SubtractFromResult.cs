using System;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Resta N del resultado del dado (mínimo 1). Hook: <c>OnRollResolved</c>.
    /// Encantamiento "Oxidado" (negativo).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Aproximación MVP.</b> El scratch no soporta modificación per-die del
    /// resultado todavía — se aplica como daño negativo al combo. Phase 4 agrega
    /// per-die result modification y este trigger se actualiza para modificar
    /// <c>DiceResult[idx]</c> directamente.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class SubtractFromResult : IOnRollResolvedTrigger
    {
        [Title("Subtraction")]
        [InfoBox("Cuánto se resta del resultado del dado. Se aplica como penalidad " +
                 "al daño del combo (aproximación hasta Phase 4).")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Amount;

        public void OnRollResolved(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;
            int amount = Amount != null ? Amount.Read(ctx.Effect) : 0;
            // Aproximación: resta del bonus de combo hasta que Phase 4 soporte
            // modificación per-die del resultado.
            ctx.Scratch.BonusComboDamage -= amount;
        }
    }
}