using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Stub <see cref="MonoBehaviour"/> que representa la salida al siguiente piso, spawneada
    /// por el <c>BossDeathBehavior</c> (plan §4.5 — follow-up). En este worktree solo expone
    /// <see cref="Interact"/>: en produccion dispara una transicion real de piso via el
    /// <c>DungeonManager</c>; hoy solo loguea, para que el smoke-test pueda validar el flow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope.</b> Fuera de scope del #103: transicion real de piso. Ver plan §1.2.
    /// El prop se disena con <c>InteractableComponent</c> (§7.7) en la foundation real.
    /// </para>
    /// </remarks>
    public class FloorExitInteractable : MonoBehaviour
    {
        [Tooltip("Label mostrado al jugador. Se localiza downstream — literal en el FP.")]
        public string InteractLabel = "Avanzar al siguiente piso";

        [Tooltip("Id del piso actual (se propaga al handler de DungeonManager en followup).")]
        public string CurrentFloorId;

        /// <summary>
        /// Punto de entrada del interaccion. Consumido por el <c>InteractableComponent</c>
        /// real (foundation §7.7) o por tests edit-mode llamandolo directamente.
        /// En el FP solo loguea — la transicion de piso real es follow-up.
        /// </summary>
        public void Interact()
        {
            Debug.Log(
                $"[FloorExitInteractable] Interact — CurrentFloorId='{CurrentFloorId}'. " +
                "Transicion real de piso es follow-up (DungeonManager fuera de scope del #103).");
        }
    }
}
