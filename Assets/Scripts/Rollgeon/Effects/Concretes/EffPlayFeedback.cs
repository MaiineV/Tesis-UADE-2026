using System;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Puente entre la pipeline de effects (§8) y el sistema de feedback (§10). Solicita
    /// un feedback bloqueante al <see cref="IFeedbackService"/> y gatea al <see cref="TurnManager"/>
    /// vía <see cref="TurnManager.BeginFeedbackWait"/> / <see cref="TurnManager.OnFeedbackComplete"/>.
    /// TECHNICAL.md §10.9.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Atómico: arma el <see cref="FeedbackRequest"/> vía <see cref="BaseEffect.GetFeedbackRequest"/>
    /// — que snapshotea el bag del <see cref="EffectContext.SourceBehavior"/> (§9.5) — y
    /// delega al servicio. El callback bajado al servicio marca el feedback como completo
    /// en el turn manager.
    /// </para>
    /// <para>
    /// [TODO §10] Hoy el servicio es un stub (<c>FeedbackServiceStub</c>) que completa
    /// inmediatamente. Cuando llegue <c>FeedbackManager</c> con <c>FeedbackDBSO</c>, este
    /// effect no cambia — lo único que cambia es el lado del servicio.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffPlayFeedback : BaseEffect,
        IUsesFeedback, ICanBeAnimFeedback, ICanBeSFXFeedback, ICanBeVFXFeedback
    {
        [Title("Feedback")]
        [SerializeField]
        [Tooltip("Id de la entry en el FeedbackDBSO que dispara este effect.")]
        private string _feedbackId;

        public override string GetEffectName() => "Play Feedback";

        public override string GetFeedbackId() => _feedbackId;

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
            {
                Debug.LogWarning("[EffPlayFeedback] IFeedbackService no registrado — no-op.");
                return true;
            }

            ServiceLocator.TryGetService<TurnManager>(out var turn);

            turn?.BeginFeedbackWait();
            var request = GetFeedbackRequest(context);
            feedback.RequestFeedbackBlocking(request, () => turn?.OnFeedbackComplete());
            return true;
        }
    }
}
