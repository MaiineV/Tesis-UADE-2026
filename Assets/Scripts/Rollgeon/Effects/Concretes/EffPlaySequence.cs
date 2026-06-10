using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Dispara una secuencia multi-step de feedbacks (§10.8) y gatea al
    /// <see cref="TurnManager"/> vía <see cref="TurnManager.BeginFeedbackWait"/> /
    /// <see cref="TurnManager.OnFeedbackComplete"/>, igual que <see cref="EffPlayFeedback"/>.
    /// TECHNICAL.md §10.8 + §10.9.
    /// </summary>
    /// <remarks>
    /// Los steps corren en paralelo por default; el orden se autora con los triggers
    /// de Start/End de cada <see cref="FeedbackSequenceStep"/> (AfterPrevious, AfterStep,
    /// OnEvent). La secuencia reporta completa cuando todos los steps con
    /// <c>BlockSequence == true</c> terminaron.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffPlaySequence : BaseEffect, IUsesFeedbackSequence
    {
        [Title("Sequence")]
        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, ShowItemCount = true)]
        [Tooltip("Steps de la secuencia. Corren en paralelo salvo dependencias de Start/End.")]
        private List<FeedbackSequenceStep> _steps = new List<FeedbackSequenceStep>();

        public override string GetEffectName() => "Play Sequence";

        public override FeedbackRequest GetFeedbackRequest(EffectContext context)
        {
            var req = base.GetFeedbackRequest(context);
            req.FeedbackId = null;
            req.IsSequence = true;
            req.SequenceSteps = _steps;
            return req;
        }

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            if (_steps == null || _steps.Count == 0)
            {
                Debug.LogWarning("[EffPlaySequence] Sin steps autorados — no-op.");
                return true;
            }

            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
            {
                Debug.LogWarning("[EffPlaySequence] IFeedbackService no registrado — no-op.");
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
