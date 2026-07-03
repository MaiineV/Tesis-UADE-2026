using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.UI.HUD.DragDrop
{
    /// <summary>
    /// Bridge entre el drop de una acción y el pipeline de combate. Espejo de Bot-Game
    /// <c>CardPlaySelectionDispatcher</c>. NO reimplementa lógica de combate: invoca el mismo
    /// seam que un click (<see cref="ActionButton.OnClicked"/>) y, si la acción abre una
    /// selección de tile, la autocompleta con la celda soltada.
    /// </summary>
    /// <remarks>
    /// La cadena <c>OnBehaviorSelected → DoConfirm → RequestAction → PlayerSelectingSubState →
    /// SelectionController.BeginSelection</c> es síncrona, así que cuando
    /// <see cref="ActionButton.OnClicked"/> retorna, <see cref="ISelectionController.IsSelecting"/>
    /// ya es <c>true</c> para acciones con selección BeforeRoll (Movement). No hace falta
    /// deferral. Para ataques con dados (auto-target) no se abre selección y el feed se saltea.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Action Play Dispatcher")]
    public sealed class ActionPlayDispatcher : MonoBehaviour
    {
        /// <summary>
        /// Commit atómico de un drop sobre <paramref name="coord"/>: selecciona la acción
        /// (= click) y, si eso abre una selección de tile, la resuelve con la celda soltada.
        /// Si tras el feed la selección sigue abierta (celda rechazada, o acción
        /// multi-target / no-AutoAccept) la cancela, de modo que el drop ejecute del todo o
        /// cancele limpio — nunca deja una selección colgada degradando a click-to-target.
        /// </summary>
        public void Commit(ActionButton button, GridCoord coord)
        {
            if (button == null) return;

            // 1) Selección de la acción — espejo byte-a-byte de un click en el botón.
            button.OnClicked?.Invoke();

            if (!ServiceLocator.TryGetService<ISelectionController>(out var selection) || selection == null)
            {
                Debug.LogWarning("[DragDrop] Commit: no hay ISelectionController registrado.");
                return;
            }

            Debug.Log($"[DragDrop] Commit: tras OnClicked IsSelecting={selection.IsSelecting} coord={coord}");

            // 2) Si la acción abrió una selección de tile (Movement / chain con target before-roll),
            //    la autocompletamos con la celda soltada. Con AutoAccept + 1 target se resuelve y
            //    ejecuta síncrono. Ataques auto-target no abren selección → IsSelecting == false.
            if (selection.IsSelecting)
                selection.OnTargetClicked(TargetRef.At(coord));

            Debug.Log($"[DragDrop] Commit: tras OnTargetClicked IsSelecting={selection.IsSelecting}");

            // 3) Atomicidad: si sigue seleccionando, la celda no era válida o la acción no se
            //    satisface con un solo drop → cancelar (el cancel de Movement no cobra energía).
            if (selection.IsSelecting)
                selection.CancelSelection();
        }
    }
}
