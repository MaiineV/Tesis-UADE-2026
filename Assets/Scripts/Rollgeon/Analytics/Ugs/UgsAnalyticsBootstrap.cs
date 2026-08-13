using System;
using System.Threading.Tasks;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

namespace Rollgeon.Analytics.Ugs
{
    /// <summary>
    /// Inicializa Unity Gaming Services durante el bootstrap global
    /// (Feature#0029). Va en la lista <c>ExtraServices</c> de
    /// <c>ServiceBootstrap.asset</c>, después de Steam (10) y antes del
    /// <c>AnalyticsTrackerService</c> (96).
    /// <para>
    /// Patrón Steam: registra <see cref="IAnalyticsSink"/>/<see cref="IAnalyticsGateway"/>
    /// SIEMPRE, con el init de UGS como fire-and-forget — analytics jamás
    /// bloquea el arranque. Sin red o sin proyecto linkeado a Unity Cloud
    /// degrada a warning único y el sink queda mudo la sesión entera.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class UgsAnalyticsBootstrap : IPreloadableService, IDisposable
    {
        /// <summary>Después de Steam (10): el tracker (96) resuelve el sink lazy igual.</summary>
        public const int DefaultPriority = 15;

        // Los dashboards de balance filtran por environment: lo del editor va a
        // development para no contaminar los datos de playtest de builds.
        private const string DevelopmentEnvironment = "development";
        private const string ProductionEnvironment = "production";

        // Estáticos: el init de UnityServices es por-proceso, no por-instancia
        // (y la instancia viene deserializada por Odin).
        private static UgsAnalyticsSink s_sink;
        private static bool s_initStarted;

        // Default false a propósito (y es el default CLR — sin field initializer,
        // compatible con el gotcha de Odin): el editor NO envía telemetría salvo
        // opt-in explícito del equipo. Las builds ignoran este tick y envían siempre.
        [Tooltip("Enviar telemetría desde el EDITOR. OFF por default para no spamear datos " +
                 "de desarrollo — prenderlo solo para smokes del pipeline. Las builds " +
                 "SIEMPRE envían (este tick no aplica fuera del editor).")]
        [SerializeField]
        private bool _sendFromEditor;

        [NonSerialized] private bool _registered;

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        // Fast-enter-playmode (domain reload off): el estado por-proceso puede
        // quedar colgado de la sesión de Play anterior. UnityServices en sí
        // puede seguir Initialized — InitializeUgsAsync lo tolera.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_sink = null;
            s_initStarted = false;
        }

        // ====================================================================
        // IPreloadableService
        // ====================================================================

        /// <inheritdoc />
        public void Register()
        {
            if (_registered) return;
            _registered = true;

            var sink = new UgsAnalyticsSink();
            s_sink = sink;
            ServiceLocator.AddService<IAnalyticsSink>(sink, ServiceScope.Global);
            ServiceLocator.AddService<IAnalyticsGateway>(sink, ServiceScope.Global);

            // Editor sin opt-in → UGS ni se inicializa: el sink queda mudo
            // (Initialized=false) y todo dropea. La UI de consent sigue viva —
            // la decisión se persiste y aplica cuando el envío esté habilitado.
            if (Application.isEditor && !_sendFromEditor)
            {
                Debug.Log("[Analytics] Envío desde editor deshabilitado — prender el tick " +
                          "'Send From Editor' en ServiceBootstrap.asset → ExtraServices → " +
                          "UgsAnalyticsBootstrap para habilitarlo. Las builds envían siempre.");
                return;
            }

            if (s_initStarted) return;
            s_initStarted = true;
            InitializeUgsAsync(sink);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // UGS no tiene Shutdown — alcanza con enmudecer el sink.
            if (s_sink != null)
            {
                s_sink.Initialized = false;
            }
        }

        // ====================================================================
        // Init async
        // ====================================================================

        // async void intencional: fire-and-forget con try/catch total (mismo
        // criterio que BootstrapRunner.Awake). El resultado se comunica por
        // sink.Initialized + log único.
        private static async void InitializeUgsAsync(UgsAnalyticsSink sink)
        {
            try
            {
                var environment = Application.isEditor ? DevelopmentEnvironment : ProductionEnvironment;

                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    var options = new InitializationOptions().SetEnvironmentName(environment);
                    await UnityServices.InitializeAsync(options);
                }
                else
                {
                    // Otro sistema (o la sesión de Play anterior sin domain
                    // reload) ya lo arrancó — solo esperar a que termine.
                    while (UnityServices.State == ServicesInitializationState.Initializing)
                    {
                        await Task.Yield();
                    }
                }

                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    Debug.LogWarning("[Analytics] UnityServices no inicializó — telemetría deshabilitada esta sesión.");
                    return;
                }

                sink.Initialized = true;

                // Releer prefs directo (no vía consent service) cubre la carrera
                // init-async vs decisión del jugador en el main menu: si decidió
                // mientras UGS inicializaba, acá se aplica igual.
                if (AnalyticsPrefs.HasDecision)
                {
                    sink.ApplyConsent(AnalyticsPrefs.IsGranted);
                }

                var consentLabel = !AnalyticsPrefs.HasDecision ? "sin decidir"
                    : AnalyticsPrefs.IsGranted ? "granted" : "denied";
                Debug.Log($"[Analytics] UGS init OK (env={environment}, consent={consentLabel}).");
            }
            catch (Exception e)
            {
                // Típico: proyecto sin linkear a Unity Cloud, o sin red.
                Debug.LogWarning($"[Analytics] Init de UGS falló — telemetría deshabilitada esta sesión. ({e.Message})");
            }
        }
    }
}
