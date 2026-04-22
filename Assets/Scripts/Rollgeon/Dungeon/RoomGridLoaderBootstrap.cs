using Patterns;
using Rollgeon.Grid;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// SO que registra <see cref="RoomGridLoader"/> como servicio Run-scope. TECHNICAL.md §17.§I.
    /// </summary>
    /// <remarks>
    /// Depende de <see cref="IGridManager"/> (75) y de <see cref="IDungeonService"/>
    /// (creado por <c>RunController.OnRunStart</c> — siempre presente cuando arranca la run).
    /// Priority 80 — justo después del grid manager y movement para garantizar que el
    /// IGridManager está registrado cuando se suscribe.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Dungeon/Room Grid Loader Bootstrap",
        fileName = "RoomGridLoaderBootstrap")]
    public sealed class RoomGridLoaderBootstrap : ScriptableObject, IPreloadableService
    {
        private RoomGridLoader _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
            {
                Debug.LogError("[RoomGridLoaderBootstrap] IGridManager no registrado.");
                return;
            }
            // IDungeonService todavía no existe en este punto (se crea en RunController.OnRunStart).
            // El RoomGridLoader lo resuelve lazy en cada OnRoomEntered — ver doc-comment.
            _instance = new RoomGridLoader(grid);
            ServiceLocator.AddService<RoomGridLoader>(_instance, ServiceScope.Run);
        }
    }
}
