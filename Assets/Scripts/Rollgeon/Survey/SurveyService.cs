using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Run;
using UnityEngine;

namespace Rollgeon.Survey
{
    /// <summary>
    /// <see cref="ISurveyService"/> de producción (Feature#0074). Clase plana con
    /// inyección total para tests. Regla central: <b>siempre disco primero</b> —
    /// <see cref="Submit"/> escribe la respuesta en el store antes de tocar el sink,
    /// así un stand sin wifi no pierde nada; <see cref="FlushPending"/> reintenta al
    /// arrancar, al empezar cada run y después de cada envío exitoso.
    /// </summary>
    public sealed class SurveyService : ISurveyService, IDisposable
    {
        private const string LogPrefix = "[Survey] ";

        private readonly SurveyConfigSO _config;
        private readonly ISurveyStore _store;
        private readonly ISurveySink _sink;
        private readonly Func<DateTime> _utcNow;
        private readonly Func<string> _deviceId;
        private readonly Func<string> _appVersion;
        private readonly bool _isEventBuild;

        // Claves con un POST en vuelo: un flush no debe volver a mandarlas.
        private readonly HashSet<string> _inFlight = new HashSet<string>(StringComparer.Ordinal);

        private bool _promptedThisRun;
        private bool _isTutorialRun;
        private bool _flushing;
        private bool _subscribed;
        private EventManager.EventReceiver _onRunStartHandler;

        public SurveyService(
            SurveyConfigSO config,
            ISurveyStore store,
            ISurveySink sink,
            Func<DateTime> utcNow = null,
            Func<string> deviceId = null,
            Func<string> appVersion = null,
            bool isEventBuild = SurveyDefines.IsEventBuild)
        {
            _config = config;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _deviceId = deviceId ?? (() => SystemInfo.deviceUniqueIdentifier);
            _appVersion = appVersion ?? (() => Application.version);
            _isEventBuild = isEventBuild;
        }

        // ====================================================================
        // ISurveyService — estado
        // ====================================================================

        public bool IsEnabled => ResolveEnabled(_config, _isEventBuild);

        public bool IsEventBuild => _isEventBuild;

        public SurveyConfigSO Config => _config;

        public bool PromptedThisRun => _promptedThisRun;

        public int PendingCount => _store.PendingCount;

        public IReadOnlyList<string> PendingKeys => _store.ListPending();

        public event Action<string, SurveyDeliveryState> DeliveryChanged;

        /// <summary>Regla de activación, estática para poder testear el caso "build de evento" sin el define.</summary>
        public static bool ResolveEnabled(SurveyConfigSO config, bool isEventBuild)
        {
            if (config == null || !config.HasQuestions) return false;
            return isEventBuild || config.Enabled;
        }

        // ====================================================================
        // Suscripción al bus
        // ====================================================================

        public void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            _onRunStartHandler = OnRunStartHandler;
            EventManager.Subscribe(EventName.OnRunStart, _onRunStartHandler);
        }

        public void Unsubscribe()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRunStart, _onRunStartHandler);
            _onRunStartHandler = null;
            _subscribed = false;
        }

        public void Dispose() => Unsubscribe();

        // Schema OnRunStart: [Guid runId, string rulesetId]. PendingRunRequest sigue
        // seteado en este punto (se limpia después de StartRun) — mismo truco que
        // AnalyticsTrackerService para saber si es el tutorial.
        private void OnRunStartHandler(params object[] args)
        {
            _promptedThisRun = false;
            _isTutorialRun = PendingRunRequest.IsTutorial;
            FlushPending();
        }

        // ====================================================================
        // Prompt gating
        // ====================================================================

        public bool ShouldPrompt(int floorIndex)
        {
            if (!IsEnabled) return false;
            if (_isTutorialRun) return false;
            if (_promptedThisRun) return false;
            return floorIndex == _config.TriggerFloorIndex;
        }

        public void MarkPrompted() => _promptedThisRun = true;

        public void ResetPromptGuard() => _promptedThisRun = false;

        // ====================================================================
        // Submit / flush
        // ====================================================================

        public void Submit(SurveyResponse response)
        {
            if (response == null) return;

            var now = _utcNow();
            if (string.IsNullOrEmpty(response.response_id)) response.response_id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(response.event_id)) response.event_id = _config != null ? _config.EventId : string.Empty;
            if (string.IsNullOrEmpty(response.created_at)) response.created_at = now.ToString("o");
            if (string.IsNullOrEmpty(response.app_version)) response.app_version = SafeCall(_appVersion);
            if (string.IsNullOrEmpty(response.device_id)) response.device_id = SafeCall(_deviceId);
            if (!response.raffle_opt_in) response.email = string.Empty;
            response.answers ??= new List<SurveyAnswer>();

            var key = BuildStoreKey(now, response.response_id);
            string json = SurveyPayload.ToStoredJson(response);

            try
            {
                _store.WritePending(key, json);
            }
            catch (Exception e)
            {
                // Sin disco igual intentamos mandar: mejor una fila en la planilla que nada.
                Debug.LogError(LogPrefix + $"No se pudo guardar la respuesta {response.response_id}: {e.Message}");
            }

            Raise(response.response_id, SurveyDeliveryState.Pending);
            TrySend(key, response, thenFlush: true);
        }

        public void FlushPending()
        {
            if (!IsEnabled || _flushing || !_sink.IsConfigured) return;

            var snapshot = new List<string>(_store.ListPending());
            snapshot.RemoveAll(_inFlight.Contains);
            if (snapshot.Count == 0) return;

            _flushing = true;
            FlushNext(snapshot, 0);
        }

        private void FlushNext(List<string> keys, int index)
        {
            if (index >= keys.Count)
            {
                _flushing = false;
                return;
            }

            var key = keys[index];
            var response = SurveyPayload.FromStoredJson(_store.ReadPending(key));
            if (response == null)
            {
                // Archivo desaparecido o corrupto: no bloquea a los demás.
                FlushNext(keys, index + 1);
                return;
            }

            SendInternal(key, response, _ => FlushNext(keys, index + 1));
        }

        private void TrySend(string key, SurveyResponse response, bool thenFlush)
        {
            if (!_sink.IsConfigured)
            {
                // Sin endpoint no es una falla: queda Pending, se manda cuando haya URL.
                return;
            }

            SendInternal(key, response, ok =>
            {
                if (ok && thenFlush) FlushPending();
            });
        }

        private void SendInternal(string key, SurveyResponse response, Action<bool> onDone)
        {
            _inFlight.Add(key);
            Raise(response.response_id, SurveyDeliveryState.Sending);

            string wire = SurveyPayload.ToWireJson(response, _config != null ? _config.SharedSecret : null);
            bool completed = false;

            void Complete(bool ok)
            {
                if (completed) return;
                completed = true;
                _inFlight.Remove(key);

                if (ok)
                {
                    try
                    {
                        _store.MarkSent(key);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(LogPrefix + $"Enviada pero no se pudo mover a sent/: {e.Message}");
                    }
                }

                Raise(response.response_id, ok ? SurveyDeliveryState.Sent : SurveyDeliveryState.Failed);
                onDone?.Invoke(ok);
            }

            try
            {
                _sink.Send(wire, Complete);
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogPrefix + $"El sink tiró al enviar {response.response_id}: {e.Message}");
                Complete(false);
            }
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        /// <summary>Prefijo de timestamp para que el store liste en orden cronológico.</summary>
        public static string BuildStoreKey(DateTime utc, string responseId)
            => utc.ToString("yyyyMMdd-HHmmss") + "_" + responseId;

        private void Raise(string responseId, SurveyDeliveryState state)
        {
            var handlers = DeliveryChanged;
            if (handlers == null) return;

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<string, SurveyDeliveryState>)handler).Invoke(responseId, state);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private static string SafeCall(Func<string> f)
        {
            try
            {
                return f?.Invoke() ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
