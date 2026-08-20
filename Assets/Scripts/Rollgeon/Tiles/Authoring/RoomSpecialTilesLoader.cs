using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Tiles.Authoring
{
    /// <summary>
    /// Listener global que materializa la autoría de casillas de la sala al entrar:
    /// resuelve <see cref="SpecialTileResolver"/> sobre el <see cref="RoomLayout"/> del
    /// prefab instanciado y coloca las instancias en <see cref="ISpecialTileService"/>,
    /// linkeando los pares de portal.
    /// </summary>
    /// <remarks>
    /// En play mode difiere un frame (patrón <c>PropTileBlocker</c>): el propio
    /// <c>SpecialTileService</c> hace su hard-reset con el mismo <c>OnRoomEntered</c> y el
    /// orden entre suscriptores no está garantizado — colocar en el frame siguiente
    /// asegura que el reset ya pasó. En EditMode (tests) corre sincrónico: ahí el orden de
    /// suscripción es determinista (el loader se construye después del servicio).
    /// </remarks>
    public sealed class RoomSpecialTilesLoader : IPreloadableService, System.IDisposable
    {
        private EventManager.EventReceiver _onRoomEnteredHandler;

        /// <summary>Después del SpecialTileService (79) y de Grid/Movement.</summary>
        public int Priority => 83;

        public void Register()
        {
            SubscribeHandlers();
            ServiceLocator.AddService<RoomSpecialTilesLoader>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests.</summary>
        public void ConfigureForTests() => SubscribeHandlers();

        private void SubscribeHandlers()
        {
            UnsubscribeHandlers();
            _onRoomEnteredHandler = OnRoomEnteredExternal;
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
        }

        private void UnsubscribeHandlers()
        {
            if (_onRoomEnteredHandler == null) return;
            EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
            _onRoomEnteredHandler = null;
        }

        public void Dispose() => UnsubscribeHandlers();

        private void OnRoomEnteredExternal(params object[] args)
        {
            if (Application.isPlaying)
            {
                CoroutineHost.Run(DeferredLoad());
                return;
            }
            LoadCurrentRoom();
        }

        private IEnumerator DeferredLoad()
        {
            yield return null; // el hard-reset del SpecialTileService ya corrió
            LoadCurrentRoom();
        }

        /// <summary>Expuesto para tests: resuelve y coloca la sala actual, sin defer.</summary>
        public void LoadCurrentRoom()
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return;
            if (!ServiceLocator.TryGetService<ISpecialTileService>(out var tiles) || tiles == null) return;

            var room = dungeon.CurrentRoomInstance;
            var layout = room?.SpawnedPrefab != null ? room.SpawnedPrefab.GetComponent<RoomLayout>() : null;
            if (layout == null) return;

            var warnings = new List<string>();
            var resolved = SpecialTileResolver.Resolve(
                layout, dungeon.CurrentFloorSeed, room.GridCell, room.ObjectStates, warnings);

            foreach (var warning in warnings)
                Debug.LogWarning($"[RoomSpecialTilesLoader] {layout.name}: {warning}");

            // No-portales primero; los pares se colocan A → B con LinkTo para que el link
            // quede armado en ambos sentidos.
            var pendingPortals = new Dictionary<int, System.Guid>();
            foreach (var tile in resolved)
            {
                if (tile.Definition == null) continue;

                if (tile.PortalLinkId < 0)
                {
                    tiles.Place(tile.Definition, new[] { tile.Coord });
                    continue;
                }

                if (pendingPortals.TryGetValue(tile.PortalLinkId, out var firstEnd))
                {
                    tiles.Place(tile.Definition, new[] { tile.Coord },
                        new TilePlacementOptions { LinkTo = firstEnd });
                }
                else
                {
                    pendingPortals[tile.PortalLinkId] = tiles.Place(tile.Definition, new[] { tile.Coord });
                }
            }
        }
    }
}
