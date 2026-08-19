using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Audio
{
    /// <summary>
    /// Crea el <see cref="MusicDirector"/> global que traduce eventos de juego a
    /// música. Requiere <see cref="IAudioService"/> ya registrado.
    /// </summary>
    /// <remarks>
    /// <b>Setup.</b> Crear el asset desde <c>Assets / Create / Rollgeon / Audio / Music Director Bootstrap</c>,
    /// asignar la <see cref="MusicLibrarySO"/> y agregarlo a
    /// <c>ServiceBootstrapSO.ExtraServices</c>. Priority 60 — después de
    /// <c>AudioManagerBootstrap</c> (50), que registra el servicio que consume.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Audio/Music Director Bootstrap",
        fileName = "MusicDirectorBootstrap")]
    public sealed class MusicDirectorBootstrap : ScriptableObject, IPreloadableService
    {
        [SerializeField, Tooltip("Catálogo de música por contexto y piso.")]
        private MusicLibrarySO _library;

        private MusicDirector _instance;

        public int Priority => 60;

        public void Register()
        {
            if (_instance != null) return;

            if (_library == null)
            {
                Debug.LogError("[MusicDirectorBootstrap] MusicLibrarySO no asignada — no se crea el MusicDirector.");
                return;
            }

            if (!ServiceLocator.TryGetService<IAudioService>(out var audio) || audio == null)
            {
                Debug.LogError("[MusicDirectorBootstrap] IAudioService no registrado — " +
                               "verificar que AudioManagerBootstrap (Priority 50) esté antes en ExtraServices.");
                return;
            }

            _instance = new MusicDirector(audio, _library);
        }
    }
}
