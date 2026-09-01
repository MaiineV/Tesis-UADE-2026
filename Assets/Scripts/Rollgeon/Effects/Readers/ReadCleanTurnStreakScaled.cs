using System;
using Patterns;
using Rollgeon.Combat.TurnState;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Rondas limpias consecutivas (sin perder vida) × <see cref="PerTurnAmount"/>, con
    /// floor al leer (<see cref="IPlayerTurnStateService.CleanTurnStreak"/>). Para
    /// "Furia Contenida" (GDD: +0.25 de daño base por turno completo sin recibir daño).
    /// </summary>
    /// <remarks>
    /// La fracción NO viaja por el pipeline (N de la fórmula es int): el streak entero
    /// vive en el servicio y la tasa float en el asset — con la escala ×10 del daño,
    /// GD tunea <c>PerTurnAmount = 2.5</c> sin tocar código. Sin servicio → 0.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadCleanTurnStreakScaled : EffectIntReader
    {
        [MinValue(0f)]
        [Tooltip("Daño base por ronda limpia acumulada. GDD Furia: 0.25 (2.5 con escala ×10).")]
        public float PerTurnAmount = 0.25f;

        public override int Read(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IPlayerTurnStateService>(out var state) || state == null)
                return 0;
            return Mathf.FloorToInt(state.CleanTurnStreak * PerTurnAmount);
        }
    }
}
