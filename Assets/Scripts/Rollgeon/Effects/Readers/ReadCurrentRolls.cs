using System;
using Patterns;
using Rollgeon.Combat.Rolls;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Rolls disponibles del jugador × <see cref="PerRollAmount"/>
    /// (<see cref="IRollPoolService.GetCurrent"/>). Colgado del trigger
    /// <c>turn.rolls.leftover</c> lee exactamente los rolls SOBRANTES del turno:
    /// <c>RollPoolService</c> emite ese evento antes del grant/clamp, así que durante
    /// el dispatch el pool todavía es el leftover. Corazón/Tesoro de la fortuna.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadCurrentRolls : EffectIntReader
    {
        [MinValue(0)]
        [Tooltip("Cuánto vale cada roll. Corazón de la fortuna (GDD): 5 (HP por roll sobrante).")]
        public int PerRollAmount = 1;

        public override int Read(EffectContext context)
        {
            if (context == null || context.SourceGuid == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<IRollPoolService>(out var rolls) || rolls == null)
                return 0;
            return rolls.GetCurrent(context.SourceGuid) * PerRollAmount;
        }
    }
}
