using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="StunService"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>. Thin — instancia + delega
    /// <see cref="IPreloadableService.Register"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Stun Service Bootstrap",
        fileName = "StunServiceBootstrap")]
    public sealed class StunServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private StunService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new StunService();
            _instance.Register();
        }
    }
}
