using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Player
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para arrastrar el
    /// <see cref="PlayerHealthEventBridge"/> a <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Thin — instancia y delega <c>Register()</c>, igual que <c>RollPoolServiceBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Player/Player Health Event Bridge Bootstrap",
        fileName = "PlayerHealthEventBridgeBootstrap")]
    public sealed class PlayerHealthEventBridgeBootstrap : ScriptableObject, IPreloadableService
    {
        private PlayerHealthEventBridge _instance;

        public int Priority => 55;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new PlayerHealthEventBridge();
            _instance.Register();
        }
    }
}
