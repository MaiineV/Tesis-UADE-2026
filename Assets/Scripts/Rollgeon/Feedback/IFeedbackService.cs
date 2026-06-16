using System;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Contrato del servicio de feedback visual/sonoro/animación. TECHNICAL.md §10.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dos implementaciones: <c>FeedbackManager</c> (real — DB, dispatch por
    /// <c>FeedbackType</c>, secuencias, watchdog de timeout) y <c>FeedbackServiceStub</c>
    /// (no-op para EditMode tests / tooling). Solo una puede estar registrada (ver
    /// invariante en <c>FeedbackServiceStub</c>).
    /// </para>
    /// <para>
    /// Única entrada pública: <see cref="RequestFeedbackBlocking"/>. El caller pasa el
    /// request + un callback que se invoca cuando el feedback termina.
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
