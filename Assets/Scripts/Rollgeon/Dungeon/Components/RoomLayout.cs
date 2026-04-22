using System.Collections.Generic;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Componente opcional sobre un prefab de sala. TECHNICAL.md §13.3.
    /// </summary>
    /// <remarks>
    /// El FP carga la grilla desde <see cref="RoomSO.GridLayout"/>. Cuando el pipeline de
    /// salas se migre a prefabs (§13.3), este componente reemplazará al RoomSO-embedded
    /// layout: un editor tool escaneará los <see cref="SpawnPoint"/>s hijos y bakeará
    /// el <see cref="GridSnapshot"/> a partir de la geometría del prefab.
    /// </remarks>
    public sealed class RoomLayout : MonoBehaviour
    {
        public GridSnapshot GridSnapshot;
        public Transform GridOrigin;
        public float TileSize = 1f;
        public List<SpawnPoint> SpawnPoints = new List<SpawnPoint>();
        public List<DoorSlot> DoorSlots = new List<DoorSlot>();

        /// <summary>
        /// Bounding box de la sala en coordenadas locales del prefab.
        /// Consumido por el camera service para (a) clampear el pan al piso
        /// (§17.E.6) y (b) dimensionar las shells del floor view (§17.E.9).
        /// </summary>
        /// <remarks>
        /// Si se deja en default y <see cref="GridSnapshot"/> tiene datos, un
        /// editor tool post-FP puede bakear este valor; por ahora el diseñador
        /// lo setea a mano en el inspector.
        /// </remarks>
        public Bounds LocalBounds = new Bounds(Vector3.zero, Vector3.one);

        public Vector3 GetOrigin() =>
            GridOrigin != null ? GridOrigin.position : transform.position;
    }
}
