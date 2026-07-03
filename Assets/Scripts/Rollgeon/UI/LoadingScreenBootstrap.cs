using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.UI
{
    /// <summary>
    /// Registra el <see cref="LoadingScreen"/> como <see cref="ILoadingScreenService"/>.
    /// Crea el GameObject persistente en <c>DontDestroyOnLoad</c>. Misma forma que
    /// <see cref="Rollgeon.Audio.AudioManagerBootstrap"/>.
    /// </summary>
    /// <remarks>
    /// <b>Setup.</b> Crear el asset desde
    /// <c>Assets / Create / Rollgeon / UI / Loading Screen Bootstrap</c>, asignar
    /// la <see cref="LoadingScreenSettingsSO"/> y agregarlo a
    /// <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/UI/Loading Screen Bootstrap",
        fileName = "LoadingScreenBootstrap")]
    public sealed class LoadingScreenBootstrap : ScriptableObject, IPreloadableService
    {
        [SerializeField, Tooltip("Settings SO con los materiales de fondo/spinner y parámetros de tween.")]
        private LoadingScreenSettingsSO _settings;

        private LoadingScreen _instance;

        public int Priority => 60;

        public void Register()
        {
            if (_instance != null) return;

            if (_settings == null)
            {
                Debug.LogError("[LoadingScreenBootstrap] LoadingScreenSettingsSO no asignado — no se registra ILoadingScreenService.");
                return;
            }

            _instance = LoadingScreen.Create(_settings);
            ServiceLocator.AddService<ILoadingScreenService>(_instance, ServiceScope.Global);
        }
    }
}
