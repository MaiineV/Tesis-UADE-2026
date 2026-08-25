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
    /// ya es <c>true</c> para acciones con selección BeforeRoll. No hace falta deferral.
    /// Desde CNF-002 los ataques también abren selección BeforeRoll (elegir enemigo antes de
    /// tirar), así que soltar Attack sobre un enemigo lo targetea y dispara la tirada.
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
        /// <param name="feedTile">false ⇒ el drop NO targetea (§6.6: Movimiento con dado propio —
        /// la selección de tile se abre recién al revelar la cara, con el rango real; alimentarla
        /// con la celda del drop la rechazaría o cancelaría la acción ya cobrada).</param>
        public void Commit(ActionButton button, GridCoord coord, bool feedTile = true)
        {
            if (button == null) return;

            // 1) Selección de la acción — espejo byte-a-byte de un click en el botón.
            button.OnClicked?.Invoke();

            if (!feedTile) return;

            if (!ServiceLocator.TryGetService<ISelectionController>(out var selection) || selection == null)
            {
                return;
            }


            // 2) Si la acción abrió una selección de tile (Movement / ataque con target
            //    before-roll, CNF-002), la autocompletamos con la celda soltada. Con
            //    AutoAccept + 1 target se resuelve síncrono (en ataques: dispara la tirada).
            if (selection.IsSelecting)
                selection.OnTargetClicked(TargetRef.At(coord));


            // 3) Atomicidad: si sigue seleccionando, la celda no era válida o la acción no se
            //    satisface con un solo drop → cancelar (el cancel de Movement no cobra energía).
            if (selection.IsSelecting)
                selection.CancelSelection();
        }
    }
}
