using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Run-scope, Priority 65 (después de InventoryService 60, al que consulta). Registra
    /// <see cref="DecayingMultiplierService"/> (Eco Menguante); se suscribe a ComboPlayed en
    /// el bootstrap, antes de que los items binden sus hooks.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Items/Decaying Multiplier Service Bootstrap",
        fileName = "DecayingMultiplierServiceBootstrap")]
    public sealed class DecayingMultiplierServiceBootstrap : ScriptableObject, IPreloadableService
    {
        public int Priority => 65;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            ServiceLocator.AddService<IDecayingMultiplierService>(new DecayingMultiplierService(), ServiceScope.Run);
        }
    }
}
