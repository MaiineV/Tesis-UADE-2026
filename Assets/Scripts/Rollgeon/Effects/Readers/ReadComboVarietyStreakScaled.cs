using System;
using Patterns;
using Rollgeon.Combat.TurnState;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Combos distintos consecutivos del combate × <see cref="PerStepAmount"/>
    /// (<see cref="IPlayerTurnStateService.ComboVarietyStreak"/>). Para "Mosaico Errático"
    /// (GDD: +2 de daño por cada combo distinto al anterior; repetir reinicia a 0).
    /// </summary>
    /// <remarks>
    /// El servicio actualiza la racha SINCRÓNICAMENTE dentro del dispatch de ComboPlayed y
    /// se suscribe antes que los items, así que leído desde el hook del item la racha ya
    /// incluye el combo en curso: el segundo combo distinto del combate vale +2. Sin
    /// servicio → 0.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadComboVarietyStreakScaled : EffectIntReader
    {
        [MinValue(0)]
        [Tooltip("Daño por cada combo distinto encadenado. TUNEABLE GD — Mosaico Errático: 2.")]
        public int PerStepAmount = 2;

        public override int Read(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IPlayerTurnStateService>(out var state) || state == null)
                return 0;
            return state.ComboVarietyStreak * PerStepAmount;
        }
    }
}
