using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.AntiRepeat
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="AntiRepeatModeService"/>
    /// al <c>ServiceBootstrapSO.ExtraServices</c>. Thin — instancia + delega
    /// <see cref="IPreloadableService.Register"/>, igual que <c>DiceBlockServiceBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Anti-Repeat Mode Service Bootstrap",
        fileName = "AntiRepeatModeServiceBootstrap")]
    public sealed class AntiRepeatModeServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private AntiRepeatModeService _instance;

        // Después de DiceBlockService (80): no es estrictamente necesario (el candado se
        // resuelve en runtime), pero mantiene un orden legible en el bootstrap.
        public int Priority => 82;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new AntiRepeatModeService();
            _instance.Register();
        }
    }
}
