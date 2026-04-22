using System.Collections.Generic;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Component sobre un prefab de sala. TECHNICAL.md §13.3.
    /// <para>
    /// Describe, en coordenadas del prefab, los puntos de spawn (player,
    /// enemigos, rewards, obstáculos), los 4 slots de puerta N/S/E/W y el
    /// bounding box local consumido por el camera service (§17.E) para el
    /// clamp de pan y el dimensionado de las shells del floor view.
    /// </para>
    /// </summary>
    public sealed class RoomLayout : MonoBehaviour
    {
        [Header("Grid")]
        [Tooltip("Origin world del tile (0,0). Si null, se usa transform.position.")]
        public Transform GridOrigin;

        [Min(0.01f)] public float TileSize = 1f;

        [Tooltip("Grid snapshot de la sala (walkable/blocked). Empty = rectángulo sin obstáculos.")]
        public GridSnapshot GridOverride;

        [Header("Spawn Points")]
        [Tooltip("Transform donde aparece el hero al entrar a la sala.")]
        public Transform PlayerSpawnPoint;

        public List<Transform> EnemySpawnPoints = new List<Transform>();

        public List<Transform> RewardSpawnPoints = new List<Transform>();

        public List<Transform> ObstacleSpawnPoints = new List<Transform>();

        [Header("Doors")]
        [Tooltip("4 slots (N/S/E/W). El DungeonManager instancia DoorPrefab si conecta, si no activa WallPlug.")]
        public List<DoorSlotRef> DoorSlots = new List<DoorSlotRef>();

        [Header("Bounds")]
        [Tooltip("Bounding box local del prefab. Recalculado OnValidate desde Renderers children; consumido por el camera service para el clamp de pan y las shells del floor view.")]
        public Bounds LocalBounds = new Bounds(Vector3.zero, Vector3.one);

        /// <summary>Origin world del grid: <see cref="GridOrigin"/> si está, sino <c>transform.position</c>.</summary>
        public Vector3 GetOrigin() =>
            GridOrigin != null ? GridOrigin.position : transform.position;

        /// <summary>
        /// Devuelve el <see cref="DoorSlotRef"/> matching <paramref name="direction"/>, o <c>null</c>
        /// si el prefab no tiene esa puerta autorada.
        /// </summary>
        public DoorSlotRef GetDoorSlot(DoorDirection direction)
        {
            for (int i = 0; i < DoorSlots.Count; i++)
                if (DoorSlots[i] != null && DoorSlots[i].Direction == direction)
                    return DoorSlots[i];
            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Recalcula <see cref="LocalBounds"/> desde los <see cref="Renderer"/>s children.
        /// Corre en editor; los consumidores (shells, camera pan) leen el valor cacheado.
        /// </summary>
        private void OnValidate()
        {
            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0) return;

            var world = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                world.Encapsulate(renderers[i].bounds);

            Vector3 localCenter = transform.InverseTransformPoint(world.center);
            LocalBounds = new Bounds(localCenter, world.size);
        }
#endif
    }
}