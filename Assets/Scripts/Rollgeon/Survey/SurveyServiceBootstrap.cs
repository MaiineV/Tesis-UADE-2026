using System;
using System.Collections;
using Patterns;
using Rollgeon.Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Survey
{
    /// <summary>
    /// Entry de <c>ServiceBootstrap.asset → ExtraServices</c> (Feature#0074). Registra
    /// <see cref="ISurveyService"/> SIEMPRE: sin <see cref="SurveyConfigSO"/> queda
    /// deshabilitado con un warning único, así la consola tiene algo que reportar.
    /// Se cablea con <b>Rollgeon → Survey → Setup Survey</b>.
    /// </summary>
    /// <remarks>
    /// Odin deserializa esta instancia sin correr constructores ni field
    /// initializers: todo el estado se arma lazy en <see cref="Register"/>.
    /// </remarks>
    [Serializable]
    public sealed class SurveyServiceBootstrap : IPreloadableService, IDisposable
    {
        private const string LogPrefix = "[Survey] ";

        /// <summary>Después de Localization (-100), Steam (10) y UGS (15); no depende de ninguno.</summary>
        public const int DefaultPriority = 20;

        [NonSerialized] private bool _registered;
        [NonSerialized] private SurveyService _service;

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        /// <inheritdoc />
        public void Register()
        {
            if (_registered) return;
            _registered = true;

            ServiceLocator.TryGetService<SurveyConfigSO>(out var config);
            if (config == null)
            {
                Debug.LogWarning(LogPrefix + "SurveyConfigSO no está en SettingsAssets del bootstrap — encuesta " +
                                 "deshabilitada. Correr Rollgeon → Survey → Setup Survey.");
            }

            var store = new FileSurveyStore(FileSurveyStore.DefaultRoot);
            var sink = new AppsScriptSurveySink(
                config != null ? config.EndpointUrl : null,
                config != null ? config.TimeoutSeconds : 10,
                new UnityWebRequestSurveyTransport());

            _service = new SurveyService(config, store, sink);
            ServiceLocator.AddService<ISurveyService>(_service, ServiceScope.Global);
            _service.Subscribe();

            if (_service.IsEnabled)
            {
                Debug.Log(LogPrefix + $"Encuesta activa (evento='{config.EventId}', piso={config.TriggerFloorIndex}, " +
                          $"endpoint={(config.HasEndpoint ? "OK" : "vacío → solo disco")}, " +
                          $"pendientes={store.PendingCount}, eventBuild={SurveyDefines.IsEventBuild}).");

                // Un frame después: nada de red en medio de BootstrapRunner.Awake.
                if (Application.isPlaying) CoroutineHost.Run(FlushNextFrame(_service));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _service?.Dispose();
            _service = null;
            _registered = false;
        }

        private static IEnumerator FlushNextFrame(SurveyService service)
        {
            yield return null;
            service.FlushPending();
        }
    }
}
