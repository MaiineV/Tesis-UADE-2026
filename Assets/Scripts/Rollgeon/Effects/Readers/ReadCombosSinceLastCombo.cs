using System;
using Patterns;
using Rollgeon.Combat.TurnState;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Cantidad de combos de combate jugados DESPUÉS de la última aparición de
    /// <see cref="ResetComboId"/> en el combate actual
    /// (<see cref="IPlayerTurnStateService.ComboHistoryThisCombat"/>), incluyendo el combo en
    /// curso. Para "Vértigo" (GDD: +0.05 al multiplicador por combo; un Par reinicia a 0 —
    /// el 0.05 va en <c>EffAddComboMultiplier.ReaderScale</c>).
    /// </summary>
    /// <remarks>
    /// El servicio agrega el combo en curso al historial ANTES de que corran los hooks de
    /// items, así que en el propio Par este reader ya devuelve 0, y el primer combo
    /// distinto tras el Par devuelve 1. Sin <c>ResetComboId</c> cuenta todos los combos
    /// del combate. Sin servicio → 0.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadCombosSinceLastCombo : EffectIntReader
    {
        [Tooltip("Id del combo que reinicia la cuenta. Vértigo: combo.pair. Vacío = cuenta todo el combate.")]
        public string ResetComboId = "combo.pair";

        public override int Read(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IPlayerTurnStateService>(out var state) || state == null)
                return 0;
            var history = state.ComboHistoryThisCombat;
            if (history == null) return 0;

            int count = 0;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(ResetComboId) && history[i] == ResetComboId) break;
                count++;
            }
            return count;
        }
    }
}
