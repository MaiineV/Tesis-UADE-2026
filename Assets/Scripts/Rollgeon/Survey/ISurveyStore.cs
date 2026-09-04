using System.Collections.Generic;

namespace Rollgeon.Survey
{
    /// <summary>
    /// Persistencia local de respuestas (Feature#0074). Separada del sink remoto
    /// porque el contrato es "escribir → enviar → marcar enviado": el store necesita
    /// listar/leer/mover, un sink no.
    /// </summary>
    public interface ISurveyStore
    {
        int PendingCount { get; }

        /// <summary>Claves pendientes en orden de creación.</summary>
        IReadOnlyList<string> ListPending();

        /// <summary>Escribe (o pisa) una respuesta pendiente. Debe ser atómico.</summary>
        void WritePending(string key, string json);

        /// <summary>JSON guardado bajo la clave, o <c>null</c> si no existe.</summary>
        string ReadPending(string key);

        /// <summary>Saca la clave de pendientes (se conserva como enviada).</summary>
        void MarkSent(string key);
    }
}
