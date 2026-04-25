using Patterns;
using Rollgeon.Grid;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// SO que registra <see cref="PlayerRoomTransitioner"/> como servicio Run-scope.
    /// </summary>
    /// <remarks>
    /// Priority 82 — después de <see cref="RoomGridLoaderBootstrap"/> (80) para que
    /// la grilla ya esté cargada cuando el transitioner responda a OnRoomEntered.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Dungeon/Player Room Transitioner Bootstrap",
        fileName = "PlayerRoomTransitionerBootstrap")]
    public sealed class PlayerRoomTransitionerBootstrap : ScriptableObject, IPreloadableService
    {
        private PlayerRoomTransitioner _instance;

        public int Priority => 82;

        public void Register()
        {
            if (_instance != null) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
            {
                Debug.LogError("[PlayerRoomTransitionerBootstrap] IGridManager no registrado.");
                return;
            }
            if (!ServiceLocator.TryGetService<IPlayerService>(out var player))
            {
                Debug.LogError("[PlayerRoomTransitionerBootstrap] IPlayerService no registrado.");
                return;
            }
            _instance = new PlayerRoomTransitioner(grid, player);
            ServiceLocator.AddService<PlayerRoomTransitioner>(_instance, ServiceScope.Run);
        }
    }
}
