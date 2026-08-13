using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Tutorial.UI
{
    /// <summary>
    /// Registra el <see cref="TutorialOverlay"/> como <see cref="ITutorialOverlayService"/>
    /// (Global + DontDestroyOnLoad — inerte mientras está oculto). Mismo patrón que
    /// <c>LoadingScreenBootstrap</c>.
    /// </summary>
    /// <remarks>
    /// <b>Setup.</b> Crear el asset desde
    /// <c>Assets / Create / Rollgeon / Tutorial / Tutorial Overlay Bootstrap</c>,
    /// asignar el <see cref="TutorialOverlaySettingsSO"/> y agregarlo a
    /// <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Tutorial/Tutorial Overlay Bootstrap",
        fileName = "TutorialOverlayBootstrap")]
    public sealed class TutorialOverlayBootstrap : ScriptableObject, IPreloadableService
    {
        [SerializeField, Tooltip("Settings con material dim, sprite de flecha y estilo del popup.")]
        private TutorialOverlaySettingsSO _settings;

        private TutorialOverlay _instance;

        public int Priority => 62;

        public void Register()
        {
            if (_instance != null) return;

            if (_settings == null)
            {
                Debug.LogError("[TutorialOverlayBootstrap] TutorialOverlaySettingsSO no asignado — no se registra ITutorialOverlayService.");
                return;
            }

            _instance = TutorialOverlay.Create(_settings);
            ServiceLocator.AddService<ITutorialOverlayService>(_instance, ServiceScope.Global);
        }
    }
}
