using System;
using System.Collections.Generic;

namespace Rollgeon.Survey
{
    /// <summary>Estado de entrega de una respuesta, para el label del overlay y la consola.</summary>
    public enum SurveyDeliveryState
    {
        /// <summary>Guardada en disco, todavía no se intentó enviar (o no hay endpoint).</summary>
        Pending,
        Sending,
        Sent,
        /// <summary>El envío falló; queda en disco y se reintenta en el próximo arranque.</summary>
        Failed,
    }

    /// <summary>
    /// Cuestionario de evento (Feature#0074). Global. Decide cuándo se muestra y se
    /// encarga de persistir y enviar las respuestas, offline-first.
    /// </summary>
    public interface ISurveyService
    {
        /// <summary>Hay config con preguntas y (build de evento o tick Enabled).</summary>
        bool IsEnabled { get; }

        /// <summary><c>true</c> si el player se compiló con <c>ROLLGEON_EVENT_BUILD</c>.</summary>
        bool IsEventBuild { get; }

        SurveyConfigSO Config { get; }

        bool PromptedThisRun { get; }

        int PendingCount { get; }

        IReadOnlyList<string> PendingKeys { get; }

        /// <summary>El overlay pregunta acá al recibir <c>OnFloorCleared</c>.</summary>
        bool ShouldPrompt(int floorIndex);

        /// <summary>Lo llama el overlay al mostrarse: una sola vez por run.</summary>
        void MarkPrompted();

        /// <summary>Vuelve a habilitar el prompt en la run actual (consola).</summary>
        void ResetPromptGuard();

        /// <summary>Completa metadata, guarda en disco y después intenta enviar.</summary>
        void Submit(SurveyResponse response);

        /// <summary>Reintenta todo lo pendiente, de a uno. No-op si ya hay un flush corriendo.</summary>
        void FlushPending();

        /// <summary>(response_id, estado) — cada transición de entrega.</summary>
        event Action<string, SurveyDeliveryState> DeliveryChanged;
    }
}
