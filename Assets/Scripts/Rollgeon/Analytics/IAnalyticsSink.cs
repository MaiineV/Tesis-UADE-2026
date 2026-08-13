using System.Collections.Generic;

namespace Rollgeon.Analytics
{
    /// <summary>
    /// Destino de eventos de telemetría (Feature#0029). Implementado por la capa
    /// UGS (<c>UgsAnalyticsSink</c>) y por fakes en tests. Vive en el asm
    /// <c>Rollgeon</c> para que el tracker no dependa de tipos del SDK — mismo
    /// esquema contrato-en-asm-principal que <c>ISteamService</c>.
    /// </summary>
    public interface IAnalyticsSink
    {
        /// <summary>
        /// El sink puede enviar: SDK inicializado Y consentimiento aplicado.
        /// Con <c>false</c>, <see cref="Send"/> dropea silenciosamente.
        /// </summary>
        bool Ready { get; }

        /// <summary>Eventos dropeados por sink no listo — diagnóstico (DevConsole `analytics status`).</summary>
        int DroppedEvents { get; }

        /// <summary>Encola un evento custom con sus parámetros (nombres snake_case de <see cref="AnalyticsEvents"/>).</summary>
        void Send(string eventName, Dictionary<string, object> parameters);

        /// <summary>
        /// Fuerza el upload del buffer. Se llama tras <c>run_ended</c> porque el
        /// scene unload puede llegar antes del flush periódico del SDK.
        /// </summary>
        void Flush();
    }
}
