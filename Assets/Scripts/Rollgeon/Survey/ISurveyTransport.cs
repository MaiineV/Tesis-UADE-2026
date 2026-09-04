using System;

namespace Rollgeon.Survey
{
    /// <summary>Resultado crudo de un POST. <see cref="Ok"/> es solo "hubo respuesta HTTP sin error de red".</summary>
    public readonly struct SurveyPostResult
    {
        public readonly bool Ok;
        public readonly long StatusCode;
        public readonly string Body;
        public readonly string Error;

        public SurveyPostResult(bool ok, long statusCode, string body, string error)
        {
            Ok = ok;
            StatusCode = statusCode;
            Body = body;
            Error = error;
        }
    }

    /// <summary>
    /// Capa HTTP mínima (Feature#0074). Una sola implementación real toca
    /// <c>UnityWebRequest</c>; el servicio y los tests usan fakes.
    /// </summary>
    public interface ISurveyTransport
    {
        /// <summary>POST asincrónico; <paramref name="onDone"/> corre en el main thread, siempre exactamente una vez.</summary>
        void Post(string url, string body, int timeoutSeconds, Action<SurveyPostResult> onDone);
    }
}
