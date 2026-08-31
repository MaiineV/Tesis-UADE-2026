using System;
using System.Collections.Generic;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Desparrama clutter puramente decorativo (fichas, botellas, cartas, huesos...) sobre el
    /// piso de una sala, sorteado de un <see cref="ScatterPropSetSO"/>. Corre una sola vez en
    /// <see cref="Awake"/>: el prefab de sala se instancia una única vez por run
    /// (<c>DungeonManager.InstantiateRoomPrefab</c>) y esa instancia persiste entre entradas —
    /// no hay que re-sortear en cada <c>OnRoomEntered</c>.
    /// </summary>
    /// <remarks>
    /// No depende de <see cref="IGridManager"/>: la sala puede estar instanciada como parte de
    /// la generación del piso mucho antes de ser la sala activa, momento en el que el grid vivo
    /// contiene la ocupancia de OTRA sala. En vez de eso lee directo del <see cref="RoomLayout"/>
    /// del propio prefab — mismo criterio que ya usa <c>RoomDoorBakeValidator</c> (Editor) para
    /// resolver coords de puerta sin un grid en vivo.
    /// <para>
    /// Puramente visual a propósito: no registra ocupancia en ningún grid ni agrega
    /// <c>PropTileBlocker</c>. Clutter chico tirado en el piso no tiene por qué estorbar el
    /// pathing — a diferencia del mobiliario de combate de <c>RoomObjectDefinitionSO</c>, que sí
    /// bloquea porque es parte de la pelea.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(RoomLayout))]
    [AddComponentMenu("Rollgeon/Dungeon/Room Prop Scatter")]
    public sealed class RoomPropScatter : MonoBehaviour
    {
        [Tooltip("Set ponderado de props candidatos. Sin esto (o vacío) el scatter no hace nada.")]
        public ScatterPropSetSO PropSet;

        [Min(0)]
        [Tooltip("Cantidad de props a colocar. El sorteo se degrada en silencio si la sala no " +
                 "tiene suficientes celdas libres — mismo criterio que ScatteredSlots de combate.")]
        public int Count = 8;

        [Min(0)]
        [Tooltip("Separación mínima (Chebyshev, en tiles) entre props elegidos.")]
        public int MinSpacing = 1;

        [Min(0)]
        [Tooltip("Radio (Chebyshev, en tiles) que se mantiene libre alrededor de cada puerta, " +
                 "spawn point y tile especial — para no tapar nada funcional.")]
        public int DoorExclusionRadius = 1;

        [Tooltip("Rotación Y random por instancia.")]
        public bool RandomYRotation = true;

        [Range(0f, 1f)]
        [Tooltip("Chance de que una instancia de una entry con Entry.CanTipOver aparezca tumbada " +
                 "de costado en vez de parada. No afecta a entries con CanTipOver en false.")]
        public float TipOverChance = 0.35f;

        [Range(0f, 0.5f)]
        [Tooltip("Desplazamiento random dentro de la celda (fracción del tile) para que el " +
                 "resultado no se lea como una grilla perfecta.")]
        public float CellJitter = 0.3f;

        [Range(0f, 1f)]
        [Tooltip("Cuánto favorece el sorteo a las celdas pegadas a un borde (pared, pilar, " +
                 "cualquier tile no-walkable vecino) por sobre las del centro de la sala. " +
                 "0 = sorteo uniforme (comportamiento viejo). 1 = casi siempre pegado al borde.")]
        public float PerimeterBias = 0.6f;

        private void Awake() => Scatter();

        private void Scatter()
        {
            if (PropSet == null || PropSet.Entries == null || PropSet.Entries.Count == 0) return;
            if (Count <= 0) return;

            var layout = GetComponent<RoomLayout>();
            if (layout == null || layout.NavGraph == null || layout.NavGraph.IsEmpty) return;

            var origin = layout.GetOrigin();
            float tileSize = Mathf.Max(layout.TileSize, 0.01f);

            var excluded = BuildExclusionSet(layout, origin, tileSize);
            var candidates = new List<NavNode>(layout.NavGraph.NodeCount);
            foreach (var node in layout.NavGraph.Nodes)
            {
                if (excluded.Contains(node.Coord)) continue;
                candidates.Add(node);
            }
            if (candidates.Count == 0) return;

            var weights = BuildPerimeterWeights(layout.NavGraph);
            var chosen = PickSpaced(candidates, Count, MinSpacing, weights);
            if (chosen.Count == 0) return;

            var rng = new System.Random();
            foreach (var node in chosen)
            {
                var entry = PropSet.PickWeighted(rng);
                if (entry?.Prefab == null) continue;

                var cellCenter = origin + new Vector3(
                    (node.Coord.X + 0.5f) * tileSize,
                    node.Height,
                    (node.Coord.Y + 0.5f) * tileSize);

                var jitter = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * CellJitter,
                    0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * CellJitter) * tileSize;

                float yaw = RandomYRotation ? (float)(rng.NextDouble() * 360.0) : 0f;
                bool tipOver = entry.CanTipOver && rng.NextDouble() < TipOverChance;

                // Tumbado: primero se acuesta 90° sobre su lado (local X), y RECIÉN sobre eso se
                // aplica el yaw — así el objeto queda de costado apuntando en una dirección
                // random en vez de siempre "cayendo" hacia el mismo lado. LiftToFloor mide el
                // AABB ya con esta rotación aplicada, así que apoya bien tumbado o parado.
                var rotation = tipOver
                    ? Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f)
                    : Quaternion.Euler(0f, yaw, 0f);

                var instance = Instantiate(entry.Prefab, cellCenter + jitter, rotation, transform);
                float scale = Mathf.Lerp(entry.UniformScaleRange.x, entry.UniformScaleRange.y, (float)rng.NextDouble());
                instance.transform.localScale *= scale;

                LiftToFloor(instance, cellCenter.y);
            }
        }

        /// <summary>
        /// Celdas a mantener libres de scatter: el frente de cada puerta, cada spawn point
        /// autorado y cada tile especial — todas ensanchadas <see cref="DoorExclusionRadius"/>
        /// tiles (Chebyshev) para que el prop no quede pegado a lo funcional.
        /// </summary>
        private HashSet<GridCoord> BuildExclusionSet(RoomLayout layout, Vector3 origin, float tileSize)
        {
            var seeds = new List<GridCoord>();

            if (layout.DoorSlots != null)
            {
                foreach (var slot in layout.DoorSlots)
                {
                    if (slot?.Anchor == null) continue;
                    // Misma resolución que AINode_SpawnRoomObjects.ResolveDoorFrontCoords /
                    // RoomDoorBakeValidator / DoorTileQuery: un paso hacia adentro desde el
                    // anchor cae en la primera celda interior.
                    seeds.Add(WorldToGrid(slot.Anchor.position, origin, tileSize) + slot.Direction.InwardOffset());
                }
            }

            AddTransformSeeds(layout.PlayerSpawnPoint, seeds, origin, tileSize);
            AddTransformSeeds(layout.EnemySpawnPoints, seeds, origin, tileSize);
            AddTransformSeeds(layout.RewardSpawnPoints, seeds, origin, tileSize);
            AddTransformSeeds(layout.ObstacleSpawnPoints, seeds, origin, tileSize);

            if (layout.SpecialTilePlacements != null)
                foreach (var p in layout.SpecialTilePlacements) seeds.Add(p.Coord);

            if (layout.SpecialTileSlots != null)
                foreach (var s in layout.SpecialTileSlots) seeds.Add(s.Coord);

            var excluded = new HashSet<GridCoord>();
            foreach (var seed in seeds)
            {
                for (int dx = -DoorExclusionRadius; dx <= DoorExclusionRadius; dx++)
                    for (int dy = -DoorExclusionRadius; dy <= DoorExclusionRadius; dy++)
                        excluded.Add(new GridCoord(seed.X + dx, seed.Y + dy));
            }
            return excluded;
        }

        /// <summary>
        /// Sube <paramref name="instance"/> en Y hasta que la base de su AABB real (post
        /// rotación/escala) quede en <paramref name="floorY"/>. Los 9 FBX de ScatterProps
        /// vienen con el pivot en el centro geométrico, no en la base (confirmado por sondeo de
        /// <c>Mesh.bounds</c>: los 9 tienen <c>min.y</c> negativo) — sin esto quedan enterrados
        /// hasta la mitad. Medir el bounds ya rotado/escalado en vez de aplicar un offset fijo
        /// por prop es lo que hace que la corrección siga siendo válida si el pivot de origen
        /// cambia (ej. si se reexporta con "Origin to Base" en Blender, esto pasa a sumar ~0).
        /// </summary>
        private static void LiftToFloor(GameObject instance, float floorY)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            instance.transform.position += new Vector3(0f, floorY - bounds.min.y, 0f);
        }

        private static void AddTransformSeeds(Transform t, List<GridCoord> seeds, Vector3 origin, float tileSize)
        {
            if (t == null) return;
            seeds.Add(WorldToGrid(t.position, origin, tileSize));
        }

        private static void AddTransformSeeds(List<Transform> ts, List<GridCoord> seeds, Vector3 origin, float tileSize)
        {
            if (ts == null) return;
            foreach (var t in ts) AddTransformSeeds(t, seeds, origin, tileSize);
        }

        // Réplica exacta de GridManager.WorldToGrid — no hay IGridManager en vivo acá (ver
        // remarks de la clase), mismo motivo por el que RoomDoorBakeValidator tiene su propia
        // copia en vez de depender del grid runtime.
        private static GridCoord WorldToGrid(Vector3 world, Vector3 origin, float tileSize)
        {
            var local = world - origin;
            return new GridCoord(
                Mathf.FloorToInt(local.x / tileSize),
                Mathf.FloorToInt(local.z / tileSize));
        }

        /// <summary>
        /// Sorteo goloso con separación mínima — mismo shape de algoritmo que
        /// <c>AINode_SpawnRoomObjects.ScatteredSlots</c> (Combat.Rooms), reimplementado acá
        /// standalone porque esa versión depende de <c>AIContext</c>/<c>IGridManager</c> y no es
        /// reusable desde un componente que corre sin combate ni grid vivo. A diferencia del
        /// original, el pick de cada ronda es PONDERADO por <paramref name="weights"/> en vez de
        /// uniforme — es lo que hace que <see cref="PerimeterBias"/> tenga efecto.
        /// </summary>
        private List<NavNode> PickSpaced(List<NavNode> pool, int count, int minSpacing, Dictionary<GridCoord, float> weights)
        {
            var candidates = new List<NavNode>(pool);
            var rng = new System.Random();
            var result = new List<NavNode>(count);

            while (result.Count < count && candidates.Count > 0)
            {
                int idx = PickWeightedIndex(candidates, weights, rng);
                var picked = candidates[idx];
                candidates.RemoveAt(idx);
                result.Add(picked);

                if (minSpacing > 0)
                    candidates.RemoveAll(n => n.Coord.Chebyshev(picked.Coord) < minSpacing);
            }
            return result;
        }

        private static int PickWeightedIndex(List<NavNode> candidates, Dictionary<GridCoord, float> weights, System.Random rng)
        {
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
                total += weights.TryGetValue(candidates[i].Coord, out var w) ? w : 1f;

            if (total <= 0f) return rng.Next(candidates.Count);

            double roll = rng.NextDouble() * total;
            double accum = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                accum += weights.TryGetValue(candidates[i].Coord, out var w) ? w : 1f;
                if (roll <= accum) return i;
            }
            return candidates.Count - 1;
        }

        /// <summary>
        /// Peso de sorteo por celda según <see cref="PerimeterBias"/>: 1 para las celdas pegadas
        /// a un borde (distancia 0 — cualquier tile con al menos un vecino cardinal NO walkable,
        /// sea pared exterior o un pilar interior) y decayendo geométricamente con la distancia
        /// BFS al borde más cercano. <c>PerimeterBias = 0</c> da peso 1 parejo a toda distancia
        /// (sorteo uniforme, comportamiento de antes).
        /// </summary>
        private Dictionary<GridCoord, float> BuildPerimeterWeights(NavGraph graph)
        {
            var distance = new Dictionary<GridCoord, int>();
            var queue = new Queue<GridCoord>();

            foreach (var node in graph.Nodes)
            {
                bool isEdge = false;
                foreach (var n in node.Coord.Neighbors4())
                {
                    if (graph.HasNode(n)) continue;
                    isEdge = true;
                    break;
                }
                if (!isEdge) continue;

                distance[node.Coord] = 0;
                queue.Enqueue(node.Coord);
            }

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int d = distance[c];
                foreach (var n in c.Neighbors4())
                {
                    if (!graph.HasNode(n) || distance.ContainsKey(n)) continue;
                    distance[n] = d + 1;
                    queue.Enqueue(n);
                }
            }

            // Base del decaimiento geométrico: 1 (sin decaimiento, bias=0) a 0.1 (decaimiento
            // fuerte, bias=1) — cada tile de distancia extra multiplica el peso por esta base.
            float decayBase = 1f - PerimeterBias * 0.9f;

            var weights = new Dictionary<GridCoord, float>(distance.Count);
            foreach (var kv in distance)
                weights[kv.Key] = Mathf.Pow(decayBase, kv.Value);
            return weights;
        }
    }
}
