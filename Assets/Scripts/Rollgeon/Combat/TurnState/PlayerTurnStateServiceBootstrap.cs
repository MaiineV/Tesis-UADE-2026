using Patterns;
using Rollgeon.Movement;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.TurnState
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Run-scope y Priority 80: después de <c>MovementServiceBootstrap</c> (78), cuyo
    /// <see cref="IMovementService"/> se resuelve acá para engancharse a
    /// <c>OnEntityMoved</c>. Instancia nueva por run (mismo patrón que Movement).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Player Turn State Service Bootstrap",
        fileName = "PlayerTurnStateServiceBootstrap")]
    public sealed class PlayerTurnStateServiceBootstrap : ScriptableObject, IPreloadableService
    {
        public int Priority => 80;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            if (!ServiceLocator.TryGetService<IMovementService>(out var movement) || movement == null)
            {
                Debug.LogError("[PlayerTurnStateServiceBootstrap] IMovementService no registrado — " +
                    "MovementServiceBootstrap tiene que correr antes (priority 78 < 80).");
                return;
            }
            var instance = new PlayerTurnStateService(movement);
            ServiceLocator.AddService<IPlayerTurnStateService>(instance, ServiceScope.Run);
        }
    }
}
