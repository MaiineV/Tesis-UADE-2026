using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using UnityEngine;

namespace Rollgeon.Grid
{
    public sealed class TileRendererRegistrar
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
            if (!ServiceLocator.TryGetService<ITileHighlightService>(out var highlight)) return;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return;

            highlight.UnregisterAll();

            var instance = dungeon.CurrentRoomInstance;
            if (instance?.SpawnedPrefab == null) return;

            var layout = instance.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null) return;

            var grid = ServiceLocator.TryGetService<IGridManager>(out var g) ? g : null;
            if (grid == null) return;

            var markers = instance.SpawnedPrefab.GetComponentsInChildren<TileMarker>(true);
            foreach (var marker in markers)
            {
                marker.Coord = grid.WorldToGrid(marker.transform.position);
                var renderer = marker.GetComponent<Renderer>();
                if (renderer != null)
                    highlight.RegisterTile(marker.Coord, renderer);
            }
        }
    }
}
