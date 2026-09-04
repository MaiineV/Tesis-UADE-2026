using System;

namespace Rollgeon.Survey
{
    /// <summary>Destino remoto de una respuesta (Feature#0074). Hoy: Google Sheets vía Apps Script.</summary>
    public interface ISurveySink
    {
        /// <summary><c>false</c> si no hay a dónde mandar (endpoint vacío): todo queda pendiente en disco.</summary>
        bool IsConfigured { get; }

        /// <summary>Envía el JSON de wire; <paramref name="onDone"/> recibe <c>true</c> solo si el servidor confirmó.</summary>
        void Send(string wireJson, Action<bool> onDone);
    }
}
