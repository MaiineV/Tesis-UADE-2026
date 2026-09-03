using System;
using Patterns;
using Rollgeon.Combat.TurnState;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Largo de la cadena de combos distintos consecutivos del combate (contando el
    /// primero) × <see cref="PerStepAmount"/>, salvo que la cadena tenga un solo combo
    /// (el primero del combate o el que rompió la racha): ahí vale 0. Para "Mosaico
    /// Errático" — decisión GD 2026-09-03: doble par → par → trío → doble par paga
    /// 0, +4, +6, +8; repetir el último combo paga 0 y deja la cadena en 1.
    /// </summary>
    /// <remarks>
    /// <see cref="IPlayerTurnStateService.ComboVarietyStreak"/> cuenta los combos distintos
    /// DESPUÉS del primero (0, 1, 2…), así que el largo de la cadena es racha + 1. El
    /// servicio la actualiza SINCRÓNICAMENTE dentro del dispatch de ComboPlayed y se
    /// suscribe antes que los items, así que leída desde el hook ya incluye el combo en
    /// curso. Cuenta ataques, defensas y curas. Sin servicio → 0.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadComboVarietyStreakScaled : EffectIntReader
    {
        [MinValue(0)]
        [Tooltip("Daño por cada combo de la cadena (contando el primero) cuando la cadena tiene " +
                 "2 o más: el 2º distinto paga 2×esto, el 3º 3×esto… TUNEABLE GD — Mosaico Errático: 2.")]
        public int PerStepAmount = 2;

        public override int Read(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IPlayerTurnStateService>(out var state) || state == null)
                return 0;
            int streak = state.ComboVarietyStreak;
            // Cadena de 1 (primer combo o el que rompe) no cobra; de ahí en más paga el largo.
            return streak <= 0 ? 0 : (streak + 1) * PerStepAmount;
        }
    }
}
