using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Wrapper thin que arrastra el <see cref="TeleportCooldownService"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>. Patrón de <c>PoisonServiceBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Teleport Cooldown Service Bootstrap",
        fileName = "TeleportCooldownServiceBootstrap")]
    public sealed class TeleportCooldownServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private TeleportCooldownService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;
            if (ServiceLocator.TryGetService<ITeleportCooldownService>(out var existing) && existing != null) return;

            _instance = new TeleportCooldownService();
            _instance.Register();
        }
    }
}
