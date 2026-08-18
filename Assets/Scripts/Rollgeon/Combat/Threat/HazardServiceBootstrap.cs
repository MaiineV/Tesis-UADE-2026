using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="HazardService"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>. Mismo patrón thin que
    /// <c>DiceBlockServiceBootstrap</c> / <c>RainHazardServiceBootstrap</c>.
    /// </summary>
    /// <remarks>
    /// Hasta ahora el <see cref="HazardService"/> solo nacía por el camino lazy de
    /// <see cref="RainHazardService.Register"/> — o sea, solo si había lluvia en la escena. Los
    /// hazards de área dinámica (fuego/hielo) los activa un nodo de AI del boss sin pasar por rain,
    /// así que necesitan que el servicio exista igual. Este bootstrap es ese camino explícito;
    /// el de rain sigue funcionando intacto si este asset no está en la escena, y si están los dos
    /// el que llegue segundo no hace nada (ver <see cref="Register"/>).
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Hazard Service Bootstrap",
        fileName = "HazardServiceBootstrap")]
    public sealed class HazardServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private HazardService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;

            // Chequeo extra respecto de los otros bootstraps thin: RainHazardService puede haber
            // creado y registrado el servicio antes que nosotros. Registrar un segundo HazardService
            // encima dejaría dos suscriptores de OnTurnQueueBuilt vivos — el hazard tickearía dos
            // veces por ronda y el que quedara fuera del ServiceLocator nunca se limpiaría.
            if (ServiceLocator.TryGetService<IHazardService>(out var existing) && existing != null) return;

            _instance = new HazardService();
            _instance.Register();
        }
    }
}
