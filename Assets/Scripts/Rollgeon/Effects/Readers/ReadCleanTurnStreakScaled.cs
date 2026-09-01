using System;
using Patterns;
using Rollgeon.Combat.TurnState;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Rondas limpias consecutivas (sin perder vida) × <see cref="PerTurnAmount"/>
    /// (<see cref="IPlayerTurnStateService.CleanTurnStreak"/>). Para
    /// "Furia Contenida" (GDD: +0.25 de daño base por turno completo sin recibir daño).
    /// </summary>
    /// <remarks>
    /// La fracción viaja entera por <see cref="ReadFloat"/> — el canal de base damage
    /// override es float y el redondeo pasa UNA sola vez al final de la fórmula N×M.
    /// <see cref="Read"/> (consumidores int legacy) floorea. Sin servicio → 0.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadCleanTurnStreakScaled : EffectIntReader
    {
        [MinValue(0f)]
        [Tooltip("Daño base por ronda limpia acumulada. TUNEABLE GD — el GDD de Furia " +
                 "menciona 0.25 y 0.50.")]
        public float PerTurnAmount = 0.25f;

        public override int Read(EffectContext context)
            => Mathf.FloorToInt(ReadFloat(context));

        public override float ReadFloat(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IPlayerTurnStateService>(out var state) || state == null)
                return 0f;
            return state.CleanTurnStreak * PerTurnAmount;
        }
    }
}
