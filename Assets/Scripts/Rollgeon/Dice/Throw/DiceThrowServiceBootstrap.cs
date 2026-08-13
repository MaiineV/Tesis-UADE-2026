using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Registra <see cref="DiceThrowService"/> en el <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </summary>
    /// <remarks>
    /// <b>Priority.</b> <c>82</c> — después del <c>EnchantedDiceRoller</c> (80): el hub
    /// resuelve <see cref="IDiceRoller"/> perezosamente en cada request, pero queda
    /// agrupado detrás del roller definitivo por claridad de ordenamiento.
    /// <b>Scope.</b> Run — la sesión de throw no debe sobrevivir entre runs.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Dice/Dice Throw Service Bootstrap",
        fileName = "DiceThrowServiceBootstrap")]
    public sealed class DiceThrowServiceBootstrap : ScriptableObject, IPreloadableService
    {
        [SerializeField, Tooltip("Settings de modo default + tuning 2D/3D. Null = Classic puro.")]
        private DiceThrowSettingsSO _settings;

        public int Priority => 82;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            // BUG-016: sin cache guard por campo — la verdad está en el locator.
            if (ServiceLocator.HasService<IDiceThrowService>()) return;
            ServiceLocator.AddService<IDiceThrowService>(
                new DiceThrowService(_settings), ServiceScope.Run);
        }
    }
}
