using System;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Implementación stub de <see cref="IFeedbackService"/>. Invoca el callback
    /// inmediatamente sin tocar audio/VFX/animator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fallback para EditMode tests / scenes sin arte / tooling que no quiere pagar
    /// el costo de instanciar el <see cref="FeedbackManager"/> real. Para combate
    /// jugable usar <see cref="FeedbackManager"/> via <see cref="FeedbackManagerBootstrap"/>.
    /// </para>
    /// <para>
    /// <b>Invariante.</b> Sólo uno de los dos (<see cref="FeedbackServiceStubBootstrap"/>
    /// o <see cref="FeedbackManagerBootstrap"/>) puede estar en
    /// <c>ServiceBootstrapSO.ExtraServices</c> — no pueden coexistir dos
    /// <see cref="IFeedbackService"/> registrados.
    /// </para>
    /// </remarks>
    public sealed class FeedbackServiceStub : IFeedbackService
    {
        public void RequestFeedbackBlocking(FeedbackRequest request, Action onComplete)
        {
            if (!string.IsNullOrEmpty(request.FeedbackId))
            {
                Debug.Log($"[FeedbackServiceStub] feedback '{request.FeedbackId}' " +
                          $"source={request.SourceGuid} target={request.TargetGuid} (stub no-op)");
            }
            onComplete?.Invoke();
        }
    }
}
