using System;
using System.Collections;
using System.Text;
using Rollgeon.Patterns;
using UnityEngine;
using UnityEngine.Networking;

namespace Rollgeon.Survey
{
    /// <summary>
    /// Único punto del juego que habla HTTP (Feature#0074). Corre en
    /// <see cref="CoroutineHost"/> (DDOL) para sobrevivir al pop del overlay, que
    /// desactiva su GameObject y mataría cualquier coroutine propia.
    /// <para>
    /// El body va como <c>text/plain</c> a propósito: Apps Script lo lee igual desde
    /// <c>e.postData.contents</c> y en WebGL evita el preflight CORS que
    /// <c>application/json</c> dispararía (Apps Script no lo responde). El 302 con que
    /// Apps Script redirige a googleusercontent lo sigue <c>UnityWebRequest</c> solo.
    /// </para>
    /// </summary>
    public sealed class UnityWebRequestSurveyTransport : ISurveyTransport
    {
        private const string LogPrefix = "[Survey] ";
        private const string ContentType = "text/plain;charset=utf-8";

        public void Post(string url, string body, int timeoutSeconds, Action<SurveyPostResult> onDone)
        {
            CoroutineHost.Run(PostRoutine(url, body, timeoutSeconds, onDone));
        }

        private static IEnumerator PostRoutine(string url, string body, int timeoutSeconds, Action<SurveyPostResult> onDone)
        {
            SurveyPostResult result;

            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
                request.uploadHandler = new UploadHandlerRaw(bytes) { contentType = ContentType };
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Mathf.Max(1, timeoutSeconds);

                yield return request.SendWebRequest();

                bool ok = request.result == UnityWebRequest.Result.Success;
                string text = request.downloadHandler != null ? request.downloadHandler.text : null;
                result = new SurveyPostResult(ok, request.responseCode, text, ok ? null : request.error);
            }

            try
            {
                onDone?.Invoke(result);
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + "El callback del POST tiró: " + e);
            }
        }
    }
}
