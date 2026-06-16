using System;
using Patterns;
using Rollgeon.Dungeon.Components;
using Rollgeon.GameCamera;
using Rollgeon.Grid;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Listener Run-scope que carga el <see cref="IGridManager"/> al entrar a
    /// cada sala. TECHNICAL.md §17.§I + §13.6.
    /// <para>
    /// Se suscribe a <see cref="EventName.OnRoomEntered"/>. Lee el
    /// <see cref="RoomLayout.NavGraph"/> del prefab instanciado de la sala
    /// activa y llama <see cref="IGridManager.LoadRoom"/>. Si la sala no tiene
    /// prefab instanciado (tests EditMode sin prefab), carga un snapshot vacío.
    /// </para>
    /// </summary>
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

            var instance = dungeon.CurrentRoomInstance;
            if (instance?.SpawnedPrefab == null)
            {
                _grid.LoadRoom(new NavGraph());
                return;
            }

            var layout = instance.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null)
            {
                _grid.LoadRoom(new NavGraph());
                return;
            }

            _grid.LoadRoom(layout.NavGraph, layout.GetOrigin(), layout.TileSize);
            RecenterCameraIfAvailable();
        }

        private static void RecenterCameraIfAvailable()
        {
            // Al entrar a una sala, pedir al camera service un recenter
            // instantáneo para evitar un smooth largo entre salas (§17.E.10).
            if (ServiceLocator.TryGetService<ICameraService>(out var cam))
            {
                cam.RecenterOnPlayer(instant: true);
            }
        }
    }
}
