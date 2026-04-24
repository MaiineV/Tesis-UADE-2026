using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Audio
{
    /// <summary>
    /// Registra el <see cref="AudioManager"/> como <see cref="IAudioService"/>.
    /// Crea un GameObject persistente en <c>DontDestroyOnLoad</c> que dueña el
    /// pool de <c>AudioSource</c> y los canales de música. TECHNICAL.md §17.A.
    /// </summary>
    /// <remarks>
    /// <b>Setup.</b> Crear el asset desde <c>Assets / Create / Rollgeon / Audio / Audio Manager Bootstrap</c>,
    /// asignar la <see cref="AudioSettingsSO"/> y agregarlo a
    /// <c>ServiceBootstrapSO.ExtraServices</c>. Priority 50 — antes de
    /// <c>FeedbackManagerBootstrap</c> (55) para que el dispatch de SFX del
    /// feedback ya encuentre el servicio al registrarse.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Audio/Audio Manager Bootstrap",
        fileName = "AudioManagerBootstrap")]
    public sealed class AudioManagerBootstrap : ScriptableObject, IPreloadableService
    {
        [SerializeField, Tooltip("Settings SO con mixer, groups, pool size y defaults de volumen.")]
        private AudioSettingsSO _settings;

        private AudioManager _instance;

        public int Priority => 50;

        public void Register()
        {
            if (_instance != null) return;

            if (_settings == null)
            {
                Debug.LogError("[AudioManagerBootstrap] AudioSettingsSO no asignado — no se registra IAudioService.");
                return;
            }

            var go = new GameObject("[AudioManager]");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioManager>();
            _instance.Configure(_settings);

            ServiceLocator.AddService<IAudioService>(_instance, ServiceScope.Global);
        }
    }
}
