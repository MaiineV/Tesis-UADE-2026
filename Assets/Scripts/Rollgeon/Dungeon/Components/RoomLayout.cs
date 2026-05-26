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

        [Tooltip("Nav graph de la sala (nodos + edges). Empty = sin restricciones.")]
        public NavGraph NavGraph;

        public NavGraphBakeSettings BakeSettings = new NavGraphBakeSettings();

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
        public void AutoPopulateDoorSlots()
        {
            UnityEditor.Undo.RecordObject(this, "Auto-Populate Door Slots");

            DoorSlots.Clear();

            var controllers = GetComponentsInChildren<DoorController>(includeInactive: true);
            var usedDirections = new HashSet<DoorDirection>();
            var wallPlugField = typeof(DoorController).GetField(
                "_wallPlug",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var controller in controllers)
            {
                var localPos = transform.InverseTransformPoint(controller.transform.position);
                var dir = DoorDirectionExtensions.FromLocalPosition(localPos);

                if (!usedDirections.Add(dir))
                {
                    Debug.LogWarning(
                        $"[RoomLayout] '{name}': dirección {dir} duplicada en " +
                        $"DoorController '{controller.gameObject.name}'. Revisar posiciones.");
                    continue;
                }

                UnityEditor.Undo.RecordObject(controller, "Auto-Populate Door Slots");
                controller.Direction = dir;
                UnityEditor.EditorUtility.SetDirty(controller);

                var doorGroup = FindDoorGroup(controller.transform);
                var anchor = doorGroup != null ? doorGroup : controller.transform;

                GameObject wallPlug = null;
                if (wallPlugField != null)
                    wallPlug = wallPlugField.GetValue(controller) as GameObject;

                DoorSlots.Add(new DoorSlotRef
                {
                    Direction = dir,
                    Anchor = anchor,
                    WallPlug = wallPlug,
                    DoorRoot = controller.gameObject,
                });
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[RoomLayout] '{name}': auto-populated {DoorSlots.Count} door slots.");
        }

        private Transform FindDoorGroup(Transform child)
        {
            var current = child;
            while (current != null && current != transform && current.parent != transform)
                current = current.parent;
            return current != transform ? current : null;
        }

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