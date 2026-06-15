using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using UnityEngine;

namespace Rollgeon.Grid
{
    public sealed class TileRendererRegistrar : IDisposable
    {
        public TileRendererRegistrar()
        {
            EventManager.Subscribe(EventName.OnRoomEntered, OnRoomEntered);
        }

        public void Dispose()
        {
            EventManager.UnSubscribe(EventName.OnRoomEntered, OnRoomEntered);
        }

        private void OnRoomEntered(params object[] args)
        {
            if (!ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
            {
                UnityEngine.Debug.LogWarning("[TileRendererRegistrar] ITileHighlightService not registered");
                return;
            }
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
            {
                UnityEngine.Debug.LogWarning("[TileRendererRegistrar] IDungeonService not registered");
                return;
            }

            highlight.UnregisterAll();

            var instance = dungeon.CurrentRoomInstance;
            if (instance?.SpawnedPrefab == null)
            {
                UnityEngine.Debug.LogWarning("[TileRendererRegistrar] CurrentRoomInstance or SpawnedPrefab is null");
                return;
            }

            var layout = instance.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null)
            {
                UnityEngine.Debug.LogWarning("[TileRendererRegistrar] RoomLayout not found on SpawnedPrefab");
                return;
            }

            int registered = RegisterRoomTiles(instance.SpawnedPrefab, layout, highlight);
            UnityEngine.Debug.Log($"[TileRendererRegistrar] OnRoomEntered — registered {registered} renderers");
        }

        /// <summary>
        /// Registra los renderers pintables de la sala en el highlight service.
        /// Estático para poder testearlo sin stubear <c>IDungeonService</c>.
        /// </summary>
        public static int RegisterRoomTiles(
            GameObject spawnedPrefab, RoomLayout layout, ITileHighlightService highlight)
        {
            int registered = 0;

            // Los markers de Floor son dueños del slot de su coord: un prop o
            // decoración stackeado sobre el piso no debe robarse el renderer
            // pintable de esa celda (pintaría el prop y el piso quedaría default).
            var floorOwned = new HashSet<GridCoord>();
            var owned = new HashSet<GridCoord>();
            var ignoredExtraFloors = new List<GridCoord>(); // celdas con >1 Floor (autoría)

            var markers = spawnedPrefab.GetComponentsInChildren<TileMarker>(true);
            foreach (var marker in markers)
            {
                // El mesh puede vivir en un hijo del root del tile (Pedestal,
                // TileWall, CornerUp…) — buscar solo en el mismo GO dejaba esas
                // celdas caminables sin registrar y el highlight las salteaba
                // en silencio.
                var renderer = marker.GetComponentInChildren<Renderer>(true);
                if (renderer == null) continue;

                bool isFloor = marker.Type == TileType.Floor;

                // El PRIMER Floor que reclama una celda es dueño del slot pintable.
                // Una vez tomado, ningún otro marker (otro Floor o un prop) puede
                // pisarle el renderer al piso real. Antes _tileRenderers[coord] hacía
                // "último gana", así que un prop mal tipado como Type=Floor (o con su
                // Coord sin autorar → default (0,0)) le robaba el slot al piso visible
                // y ese tile quedaba caminable pero sin highlight (el tinte iba al mesh
                // equivocado, ej. 'Columnas-pared' en (0,0) de Start_Room01).
                if (floorOwned.Contains(marker.Coord))
                {
                    ignoredExtraFloors.Add(marker.Coord);
                    continue;
                }

                // Un Floor sí puede pisar a un no-floor previo en la misma celda
                // (el piso debe ser dueño del slot por encima de decoraciones).
                if (!isFloor && owned.Contains(marker.Coord)) continue;

                highlight.RegisterTile(marker.Coord, renderer);
                owned.Add(marker.Coord);
                if (isFloor) floorOwned.Add(marker.Coord);
                registered++;
            }

            // Paridad con NavGraphBaker: los meshes legacy sin TileMarker generan
            // nodos caminables en el bake (coord inferida por posición) — sin
            // registrarlos acá esas celdas quedan walkable pero imposibles de pintar.
            float tileSize = Mathf.Max(layout.BakeSettings?.TileSize ?? 1f, 0.01f);
            var root = spawnedPrefab.transform;
            foreach (var r in spawnedPrefab.GetComponentsInChildren<Renderer>(includeInactive: false))
            {
                if (r.GetComponentInParent<TileMarker>() != null) continue;

                var lp = root.InverseTransformPoint(r.bounds.center);
                var coord = new GridCoord(
                    Mathf.FloorToInt(lp.x / tileSize),
                    Mathf.FloorToInt(lp.z / tileSize));
                if (owned.Contains(coord)) continue;

                highlight.RegisterTile(coord, r);
                owned.Add(coord);
                registered++;
            }

            // Resumen único (sin spam per-marker): celdas donde un marker extra
            // Type=Floor fue ignorado por colisión. Suele ser piso duplicado (benigno)
            // o un prop/pared mal tipado como Floor / con Coord sin autorar (default
            // (0,0)). Para el detalle por GameObject usar "Validate Tiles" en el editor.
            if (ignoredExtraFloors.Count > 0)
                UnityEngine.Debug.LogWarning(
                    $"[TileRendererRegistrar] {ignoredExtraFloors.Count} marker(s) Type=Floor extra " +
                    $"ignorados por colisión de celda: {string.Join(", ", ignoredExtraFloors)}. " +
                    $"Revisar con 'Validate Tiles' en el RoomLayout.");

            // DIAG: nodos walkable del grafo que quedaron sin renderer registrado son
            // caminables pero imposibles de pintar — esta es la causa de "el highlight
            // no aparece en todos los tiles caminables". 'owned' es exactamente el set
            // de coords registrados arriba, así que el diff contra los nodos del bake
            // localiza el gap sin depender de gameplay.
            if (layout.NavGraph != null && !layout.NavGraph.IsEmpty)
            {
                var missing = new List<GridCoord>();
                foreach (var c in layout.NavGraph.AllCoords())
                    if (!owned.Contains(c)) missing.Add(c);
                if (missing.Count > 0)
                    UnityEngine.Debug.LogWarning(
                        $"[TileRendererRegistrar] {missing.Count}/{layout.NavGraph.NodeCount} nodo(s) walkable SIN renderer registrado " +
                        $"(caminables pero sin highlight): {string.Join(", ", missing)}");
            }

            return registered;
        }
    }
}
