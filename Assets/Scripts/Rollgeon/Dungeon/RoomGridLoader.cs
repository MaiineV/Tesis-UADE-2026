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
    /// Se suscribe a <see cref="EventName.OnRoomEntered"/>. Resuelve
    /// <see cref="IDungeonService"/> lazy en cada evento porque el bootstrap del loader
    /// corre antes de <see cref="Rollgeon.Run.RunController.OnRunStart"/> (donde
    /// <c>DungeonManager.CreateAndRegister</c> publica el servicio). Cuando la sala
    /// cambia, lee <see cref="RoomSO.GridLayout"/> de <see cref="IDungeonService.CurrentRoom"/>
    /// y llama <see cref="IGridManager.LoadRoom"/>.
    /// </remarks>
    public sealed class RoomGridLoader : IDisposable
    {
        private readonly IGridManager _grid;
        private readonly IDungeonService _explicitDungeon;
        private bool _subscribed;

        /// <summary>
        /// Overload para tests / wiring manual donde el dungeon service se conoce
        /// en construcción. En producción, pasar <c>null</c> y dejar que el loader
        /// resuelva via <see cref="ServiceLocator"/> en cada evento.
        /// </summary>
        public RoomGridLoader(IGridManager grid, IDungeonService dungeon = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _explicitDungeon = dungeon;
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
            var dungeon = _explicitDungeon;
            if (dungeon == null)
            {
                ServiceLocator.TryGetService<IDungeonService>(out dungeon);
            }
            if (dungeon == null)
            {
                // Sin run activa — nada que cargar. Al siguiente OnRoomEntered dentro de una run
                // el service ya va a estar registrado y el loader puede continuar.
                return;
            }

            var room = dungeon.CurrentRoom;
            if (room == null)
            {
                _grid.LoadRoom(GridSnapshot.Empty);
                return;
            }
            _grid.LoadRoom(room.GridLayout);
        }
    }
}
