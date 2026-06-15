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

        // Visualiza el NavGraph horneado (lo que usa el runtime) + los volúmenes de
        // blocker. Sirve para revisar de un vistazo: si un piso real NO aparece verde
        // su nodo se cayó en el bake (IsBlocker / IntersectsAnyBlocker); si un blocker
        // rojo pisa celdas vecinas de piso, eso es el "reconoce los costados".
        private void OnDrawGizmosSelected()
        {
            var origin = GetOrigin();
            float ts = Mathf.Max(TileSize, 0.01f);

            if (NavGraph != null && !NavGraph.IsEmpty)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                foreach (var n in NavGraph.Nodes)
                {
                    var c = origin + new Vector3((n.Coord.X + 0.5f) * ts, n.Height + 0.02f, (n.Coord.Y + 0.5f) * ts);
                    Gizmos.DrawWireCube(c, new Vector3(ts * 0.9f, 0.02f, ts * 0.9f));
                }

                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.35f);
                foreach (var e in NavGraph.Edges)
                {
                    var a = origin + new Vector3((e.From.X + 0.5f) * ts, 0.02f, (e.From.Y + 0.5f) * ts);
                    var b = origin + new Vector3((e.To.X + 0.5f) * ts, 0.02f, (e.To.Y + 0.5f) * ts);
                    Gizmos.DrawLine(a, b);
                }
            }

            // Footprint XZ de cada blocker, idéntico a NavGraphBaker.BlockerBounds:
            // minX = pos.x + (off.x - 0.5)*ts ; size = max(1, fp)*ts.
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            foreach (var m in GetComponentsInChildren<TileMarker>(true))
            {
                if (m == null || !m.IsBlocker) continue;
                var pos = m.transform.position;
                var fp = m.Footprint;
                var off = m.FootprintOffset;
                float sx = Mathf.Max(1, fp.x) * ts;
                float sz = Mathf.Max(1, fp.z) * ts;
                float cx = pos.x + (off.x - 0.5f) * ts + sx * 0.5f;
                float cz = pos.z + (off.z - 0.5f) * ts + sz * 0.5f;
                Gizmos.DrawWireCube(new Vector3(cx, pos.y + 0.5f, cz), new Vector3(sx, 1f, sz));
            }
        }
#endif
    }
}