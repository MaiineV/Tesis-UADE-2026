using System;
using System.Collections.Generic;

namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Resultado de una ronda de selección. TECHNICAL.md §11.1. El <c>SelectionController</c>
    /// runtime (out of scope en esta foundation) construye este objeto tras recibir input del
    /// jugador; los effects con <c>HasSelectionRequirement == true</c> lo consumen en <c>Apply</c>.
    /// </summary>
    public class TargetSelectionResult
    {
        /// <summary>La selección se completó con éxito — hay <see cref="SelectedTargets"/> válidos.</summary>
        public bool WasCompleted;

        /// <summary>El jugador canceló la selección (ESC / back). <see cref="SelectedTargets"/> puede ser null.</summary>
        public bool WasCancelled;

        /// <summary>El efecto decidió skipear la selección (p.ej. <c>IsSkippable == true</c> y no había targets).</summary>
        public bool WasSkipped;

        /// <summary>Lista de targets elegidos. Nula / vacía cuando <see cref="WasCancelled"/> o <see cref="WasSkipped"/>.</summary>
        public List<TargetRef> SelectedTargets;

        /// <summary>Azúcar — primer slot id. Devuelve <c>-1</c> si la lista es null/vacía.</summary>
        public int FirstSelectedId
        {
            get
            {
                if (SelectedTargets == null || SelectedTargets.Count == 0) return -1;
                return SelectedTargets[0]?.SlotId ?? -1;
            }
        }

        /// <summary>Azúcar — primer guid. Devuelve <see cref="System.Guid.Empty"/> si la lista es null/vacía.</summary>
        public Guid FirstSelectedGuid
        {
            get
            {
                if (SelectedTargets == null || SelectedTargets.Count == 0) return Guid.Empty;
                return SelectedTargets[0]?.Guid ?? Guid.Empty;
            }
        }

        /// <summary>
        /// Azúcar — primera celda seleccionada. Devuelve <c>false</c> si no hay targets
        /// o si el primer <see cref="TargetRef"/> no trae <see cref="TargetRef.Cell"/>.
        /// </summary>
        public bool TryGetFirstSelectedCell(out Rollgeon.Grid.GridCoord cell)
        {
            cell = default;
            if (SelectedTargets == null || SelectedTargets.Count == 0) return false;
            var first = SelectedTargets[0];
            if (first == null || !first.HasCell) return false;
            cell = first.Cell;
            return true;
        }
    }
}
