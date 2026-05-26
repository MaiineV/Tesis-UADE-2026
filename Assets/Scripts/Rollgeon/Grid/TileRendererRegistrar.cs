using System;
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

            var markers = instance.SpawnedPrefab.GetComponentsInChildren<TileMarker>(true);
            int registered = 0;
            foreach (var marker in markers)
            {
                var renderer = marker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    highlight.RegisterTile(marker.Coord, renderer);
                    registered++;
                }
            }
            UnityEngine.Debug.Log($"[TileRendererRegistrar] OnRoomEntered — found {markers.Length} TileMarkers, registered {registered} renderers");
        }
    }
}
