using System;
using Patterns;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Listener Run-scope que carga el <see cref="IGridManager"/> al entrar a cada sala.
    /// TECHNICAL.md §17.§I + §13.3.
    /// </summary>
    /// <remarks>
    /// Se suscribe a <see cref="EventName.OnRoomEntered"/>. Cuando la sala cambia, lee
    /// <see cref="RoomSO.GridLayout"/> de <see cref="IDungeonService.CurrentRoom"/> y llama
    /// <see cref="IGridManager.LoadRoom"/>. Si el room no tiene layout autorado, carga un
    /// snapshot vacío — el <see cref="GridManager"/> trata eso como rectángulo walkable.
    /// </remarks>
    public sealed class RoomGridLoader : IDisposable
    {
        private readonly IGridManager _grid;
        private readonly IDungeonService _dungeon;
        private bool _subscribed;

        public RoomGridLoader(IGridManager grid, IDungeonService dungeon)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _dungeon = dungeon ?? throw new ArgumentNullException(nameof(dungeon));
            EventManager.Subscribe(EventName.OnRoomEntered, OnRoomEntered);
            _subscribed = true;
            LoadCurrent();
        }

        public void Dispose()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRoomEntered, OnRoomEntered);
            _subscribed = false;
        }

        private void OnRoomEntered(params object[] _) => LoadCurrent();

        private void LoadCurrent()
        {
            var room = _dungeon.CurrentRoom;
            if (room == null)
            {
                _grid.LoadRoom(GridSnapshot.Empty);
                return;
            }
            _grid.LoadRoom(room.GridLayout);
        }
    }
}
