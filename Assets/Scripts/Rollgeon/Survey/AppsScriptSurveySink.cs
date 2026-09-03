using System;
using UnityEngine;

namespace Rollgeon.Survey
{
    /// <summary>
    /// <see cref="ISurveySink"/> contra un Google Apps Script desplegado como Web App
    /// (<c>tools/survey/apps-script.gs</c>). Confirmado = HTTP 200 con body
    /// <c>{"ok":true}</c>; cualquier otra cosa (login HTML porque el deploy no es
    /// "cualquiera", <c>ok:false</c>, timeout) deja la respuesta pendiente.
    /// </summary>
    public sealed class AppsScriptSurveySink : ISurveySink
    {
        [Serializable]
        private sealed class Reply
        {
            public bool ok;
            public string error;
        }

        private readonly string _url;
        private readonly int _timeoutSeconds;
        private readonly ISurveyTransport _transport;

        public AppsScriptSurveySink(string url, int timeoutSeconds, ISurveyTransport transport)
        {
            _url = url?.Trim();
            _timeoutSeconds = Math.Max(1, timeoutSeconds);
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_url);

        public void Send(string wireJson, Action<bool> onDone)
        {
            if (!IsConfigured)
            {
                onDone?.Invoke(false);
                return;
            }

            _transport.Post(_url, wireJson, _timeoutSeconds, result => onDone?.Invoke(Accepted(result)));
        }

        /// <summary>Regla de aceptación, pura para tests.</summary>
        public static bool Accepted(SurveyPostResult result)
        {
            if (!result.Ok || result.StatusCode != 200) return false;
            if (string.IsNullOrWhiteSpace(result.Body)) return false;

            try
            {
                var reply = JsonUtility.FromJson<Reply>(result.Body);
                return reply != null && reply.ok;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
