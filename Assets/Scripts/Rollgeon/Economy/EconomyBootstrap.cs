using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Economy
{
    /// <summary>
    /// Registra una <see cref="EconomyService"/> como <see cref="IEconomyService"/>.
    /// MVP standalone — cuando aterrice el sistema de atributos (§1.3) este
    /// bootstrap se reemplaza por un adapter sobre el atributo <c>Gold</c>.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Rollgeon/Economy/Economy Bootstrap",
        fileName = "EconomyBootstrap")]
    public sealed class EconomyBootstrap : ScriptableObject, IPreloadableService
    {
        [Title("Starting gold"), MinValue(0)]
        [Tooltip("Oro con el que arranca un run fresco. Usado por el MVP sin save real.")]
        [SerializeField] private int _startingGold = 10;

        public int Priority => 40;

        public void Register()
        {
            var service = new EconomyService(_startingGold);
            ServiceLocator.AddService<IEconomyService>(service, ServiceScope.Global);
        }
    }
}
