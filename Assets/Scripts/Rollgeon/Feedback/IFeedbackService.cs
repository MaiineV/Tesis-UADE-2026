using System;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Contrato del servicio de feedback visual/sonoro/animación. TECHNICAL.md §10.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Versión inicial stub (Sprint 03 FP). La implementación real (<c>FeedbackManager</c>
    /// con <c>FeedbackDBSO</c>, dispatch por <c>FeedbackType</c>, watchdog de timeout)
    /// queda para tickets posteriores.
    /// </para>
    /// <para>
    /// Única entrada pública por ahora: <see cref="RequestFeedbackBlocking"/>. El caller
    /// pasa el request + un callback que se invoca cuando el feedback termina. El stub
    /// invoca el callback inmediatamente.
    /// </para>
    /// </remarks>
    public interface IFeedbackService
    {
        /// <summary>
        /// Encola un feedback bloqueante y devuelve al caller. Cuando el feedback termina,
        /// invoca <paramref name="onComplete"/>. Contract: <paramref name="onComplete"/>
        /// siempre se invoca exactamente una vez (incluso si el feedback id es inválido,
        /// en cuyo caso se completa inmediatamente).
        /// </summary>
        void RequestFeedbackBlocking(FeedbackRequest request, Action onComplete);
    }
}
