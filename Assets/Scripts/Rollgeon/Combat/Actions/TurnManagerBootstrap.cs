using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Actions
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para arrastrar el <see cref="TurnManager"/> al
    /// inspector de <c>ServiceBootstrapSO.ExtraServices</c>. Thin — su unica responsabilidad
    /// es instanciar <see cref="TurnManager"/> y delegar <see cref="IPreloadableService.Register"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por que existe.</b> <see cref="TurnManager"/> es una clase plain C# que ya implementa
    /// <see cref="IPreloadableService"/>. Tecnicamente podria vivir directo en la lista
    /// polimorfica <c>ExtraServices</c> via Odin, pero un <see cref="ScriptableObject"/>
    /// wrapper da una UX de authoring predecible (drag-and-drop del <c>.asset</c>) identica a
    /// <c>EnergyServiceBootstrap</c> / <c>TurnOrderServiceBootstrap</c>.
    /// </para>
    /// <para>
    /// <b>Priority.</b> Hereda <see cref="TurnManager.Priority"/> (<c>60</c>) — despues del
    /// <c>EnergyServiceBootstrap</c> (<c>50</c>). Ver plan §10 R9.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Turn Manager Bootstrap", fileName = "TurnManagerBootstrap")]
    public sealed class TurnManagerBootstrap : ScriptableObject, IPreloadableService
    {
        private TurnManager _instance;

        /// <summary>Matchea <see cref="TurnManager.Priority"/> — se propaga para el sort de <c>ExtraServices</c>.</summary>
        public int Priority => 60;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new TurnManager();
            _instance.Register();
        }
    }
}
