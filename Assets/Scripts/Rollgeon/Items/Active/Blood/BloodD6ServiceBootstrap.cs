using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Items.Active.Blood
{
    /// <summary>
    /// Wrapper thin que arrastra el <see cref="BloodD6Service"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>. Patrón de <c>BleedServiceBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Items/Blood D6 Service Bootstrap",
        fileName = "BloodD6ServiceBootstrap")]
    public sealed class BloodD6ServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private BloodD6Service _instance;

        public int Priority => 85;

        public void Register()
        {
            if (_instance != null) return;
            if (ServiceLocator.TryGetService<IBloodD6Service>(out var existing) && existing != null) return;

            _instance = new BloodD6Service();
            _instance.Register();
        }
    }
}
