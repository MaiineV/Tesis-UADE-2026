using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using UnityEngine;
using UnityEngine.UnityConsent;

namespace Rollgeon.Analytics.Ugs
{
    /// <summary>
    /// Implementación UGS de <see cref="IAnalyticsSink"/> + <see cref="IAnalyticsGateway"/>
    /// (Feature#0029). Todos los tipos del SDK quedan detrás de este archivo —
    /// mismo esquema que <c>SteamService</c> con Steamworks.
    /// <para>
    /// <c>AnalyticsService.Instance</c> tira si UnityServices no inicializó:
    /// nunca se toca antes de que <see cref="Initialized"/> sea <c>true</c>
    /// (lo flipea <c>UgsAnalyticsBootstrap</c> al completar su init async).
    /// </para>
    /// </summary>
    public sealed class UgsAnalyticsSink : IAnalyticsSink, IAnalyticsGateway
    {
        private bool _consentGranted;

        /// <inheritdoc />
        public bool Initialized { get; internal set; }

        /// <inheritdoc />
        public bool Ready => Initialized && _consentGranted;

        /// <summary>Eventos dropeados por sink no listo — diagnóstico para DevConsole.</summary>
        public int DroppedEvents { get; private set; }

        /// <inheritdoc />
        public string PrivacyUrl => Initialized ? AnalyticsService.Instance.PrivacyUrl : null;

        /// <inheritdoc />
        public void Send(string eventName, Dictionary<string, object> parameters)
        {
            if (!Ready)
            {
                DroppedEvents++;
                return;
            }

            var customEvent = new CustomEvent(eventName);
            if (parameters != null)
            {
                foreach (var pair in parameters)
                {
                    customEvent.Add(pair.Key, pair.Value);
                }
            }

            AnalyticsService.Instance.RecordEvent(customEvent);
        }

        /// <inheritdoc />
        public void Flush()
        {
            if (!Ready) return;
            AnalyticsService.Instance.Flush();
        }

        /// <inheritdoc />
        public void ApplyConsent(bool granted)
        {
            if (!Initialized) return;

            // Vía moderna (Unity 6.2+ / SDK 6.1+): el estado de consentimiento
            // del engine arranca/frena la recolección — StartDataCollection es
            // obsoleto. Solo se pisa AnalyticsIntent; AdsIntent queda como esté.
            var state = EndUserConsent.GetConsentState();
            state.AnalyticsIntent = granted ? ConsentStatus.Granted : ConsentStatus.Denied;
            EndUserConsent.SetConsentState(state);

            _consentGranted = granted;
        }

        /// <inheritdoc />
        public bool TryRequestDataDeletion()
        {
            if (!Initialized) return false;

            try
            {
                AnalyticsService.Instance.RequestDataDeletion();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] RequestDataDeletion falló: {e.Message}");
                return false;
            }
        }
    }
}
