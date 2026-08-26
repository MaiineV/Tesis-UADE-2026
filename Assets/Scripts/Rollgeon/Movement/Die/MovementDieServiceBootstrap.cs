using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Movement.Die
{
    /// <summary>
    /// SO wrapper que registra <see cref="MovementDieService"/> como
    /// <see cref="IMovementDieService"/>. TECHNICAL.md §6.6.
    /// </summary>
    /// <remarks>
    /// <b>Scope.</b> Run. Lee el <see cref="IPlayerService"/> (Global) para resolver el dado de
    /// la clase. Priority 79: después de <c>MovementServiceBootstrap</c> (78) — no depende de
    /// él, solo mantiene el grupo "Movement" contiguo en el orden de registro.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Movement/Movement Die Service Bootstrap",
        fileName = "MovementDieServiceBootstrap")]
    public sealed class MovementDieServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private MovementDieService _instance;

        public int Priority => 79;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            ServiceLocator.TryGetService<IPlayerService>(out var player);
            if (player == null)
                Debug.LogWarning("[MovementDieServiceBootstrap] IPlayerService no registrado — " +
                                 "el dado de Movimiento usará el tipo default (D4).");

            _instance?.Dispose();
            _instance = new MovementDieService(player);
            ServiceLocator.AddService<IMovementDieService>(_instance, ServiceScope.Run);
        }
    }
}
