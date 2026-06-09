using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.DiceBlock
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que arrastra el <see cref="DiceBlockService"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>. Thin — instancia + delega
    /// <see cref="IPreloadableService.Register"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Dice Block Service Bootstrap",
        fileName = "DiceBlockServiceBootstrap")]
    public sealed class DiceBlockServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private DiceBlockService _instance;

        public int Priority => 80;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new DiceBlockService();
            _instance.Register();
        }
    }
}
