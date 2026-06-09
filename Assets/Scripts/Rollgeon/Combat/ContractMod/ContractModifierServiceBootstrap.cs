using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.ContractMod
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="ContractModifierService"/>
    /// al <c>ServiceBootstrapSO.ExtraServices</c>. Thin — instancia + delega
    /// <see cref="IPreloadableService.Register"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Contract Modifier Service Bootstrap",
        fileName = "ContractModifierServiceBootstrap")]
    public sealed class ContractModifierServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private ContractModifierService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new ContractModifierService();
            _instance.Register();
        }
    }
}
