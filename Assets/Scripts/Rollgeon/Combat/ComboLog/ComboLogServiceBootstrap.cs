using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.ComboLog
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="ComboLogService"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>. Thin — instancia + delega
    /// <see cref="IPreloadableService.Register"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Combo Log Service Bootstrap",
        fileName = "ComboLogServiceBootstrap")]
    public sealed class ComboLogServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private ComboLogService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new ComboLogService();
            _instance.Register();
        }
    }
}
