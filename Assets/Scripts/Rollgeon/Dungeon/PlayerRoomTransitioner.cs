using System;
using Patterns;
using Rollgeon.Dungeon.Components;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Player;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Reposiciona al player en el tile correcto al entrar a una sala.
    /// Si <see cref="IDungeonService.LastEntryDirection"/> tiene valor, lo ubica
    /// en el <see cref="DoorSlotRef.Anchor"/> de esa dirección; si no (sala
    /// inicial o teleport), usa <see cref="RoomLayout.PlayerSpawnPoint"/>.
    /// </summary>
    /// <remarks>
    /// Run-scope. Priority 82 — corre después de <see cref="RoomGridLoader"/> (80)
    /// para que la grilla ya esté cargada al registrar al player.
    /// </remarks>
    public sealed class PlayerRoomTransitioner : IDisposable
    {
        private readonly IGridManager _grid;
        private readonly IPlayerService _player;
        private bool _subscribed;

        public PlayerRoomTransitioner(IGridManager grid, IPlayerService player)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            EventManager.Subscribe(EventName.OnRoomEntered, OnRoomEntered);
            _subscribed = true;
        }

        public void Dispose()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRoomEntered, OnRoomEntered);
            _subscribed = false;
        }

        private void OnRoomEntered(params object[] _)
        {
            var playerGuid = _player.PlayerGuid;
            if (playerGuid == Guid.Empty) return;

            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return;

            var instance = dungeon.CurrentRoomInstance;
            if (instance?.SpawnedPrefab == null) return;

            var layout = instance.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null) return;

            var spawnCoord = ResolveSpawnCoord(dungeon.LastEntryDirection, layout);

            _grid.Register(playerGuid, spawnCoord);

            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals)
                && visuals.TryGetPawn(playerGuid, out var pawn))
            {
                pawn.SnapToGrid(_grid, spawnCoord);
            }
        }

        private GridCoord ResolveSpawnCoord(DoorDirection? entryDirection, RoomLayout layout)
        {
            if (entryDirection.HasValue)
            {
                var slot = layout.GetDoorSlot(entryDirection.Value);
                if (slot?.Anchor != null)
                    return _grid.WorldToGrid(slot.Anchor.position);
            }

            if (layout.PlayerSpawnPoint != null)
                return _grid.WorldToGrid(layout.PlayerSpawnPoint.position);

            return GridCoord.Zero;
        }
    }
}
