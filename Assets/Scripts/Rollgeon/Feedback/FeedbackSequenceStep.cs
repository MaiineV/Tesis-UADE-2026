using System;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Un step autoral dentro de una secuencia. TECHNICAL.md §10.8.
    /// Los campos condicionales se muestran en el inspector según <see cref="Source"/>,
    /// <see cref="StartMode"/> y <see cref="EndMode"/>.
    /// </summary>
    [Serializable]
    public class FeedbackSequenceStep
    {
        [Tooltip("Origen del step — ref al DB o inline.")]
        public StepSource Source = StepSource.FeedbackRef;

        [ShowIf(nameof(IsFeedbackRef))]
        [Tooltip("Id de la entry en el FeedbackDBSO cuando Source = FeedbackRef.")]
        public string FeedbackRefId;

        [ShowIf(nameof(IsInlineWait))]
        [Tooltip("Segundos de espera cuando Source = InlineWait.")]
        public float WaitDuration = 0.25f;

        [ShowIf(nameof(IsInlineAnimation))]
        [Tooltip("Trigger del Animator cuando Source = InlineAnimation.")]
        public string InlineAnimTrigger;

        [ShowIf(nameof(IsInlineAnimation))]
        [Tooltip("Si true, dispara el trigger en el pawn source; si false, en el target.")]
        public bool InlineAnimOnSource = true;

        [ShowIf(nameof(IsInlineBehaviorValue))]
        public BehaviorValueKey InlineBehaviorValueKey = BehaviorValueKey.None;

        [ShowIf(nameof(IsInlineBehaviorValue))]
        public BehaviorValueTarget InlineBehaviorValueTarget = BehaviorValueTarget.Target;

        [Title("Timing")]
        [Tooltip("Cuándo arranca este step.")]
        public StepStartMode StartMode = StepStartMode.Immediate;

        [ShowIf(nameof(StartNeedsStepIndex))]
        [Tooltip("Índice del step del que depende (0-based).")]
        public int StartDependsOnStepIndex;

        [ShowIf(nameof(StartNeedsEventKey))]
        [Tooltip("Key del event bus que destraba el start.")]
        public string StartOnEventKey;

        [Tooltip("Delay (segundos) después del trigger de start.")]
        public float StartDelay;

        [Tooltip("Cuándo se considera terminado el step.")]
        public StepEndMode EndMode = StepEndMode.OnDuration;

        [ShowIf(nameof(EndNeedsDuration))]
        [Tooltip("Override del Duration de la entry. 0 = usar el de la entry.")]
        public float DurationOverride;

        [ShowIf(nameof(EndNeedsEventKey))]
        [Tooltip("Key del event bus que marca el end.")]
        public string EndOnEventKey;

        [Title("Blocking")]
        [Tooltip("Si false, la secuencia global no espera a este step para reportar complete.")]
        public bool BlockSequence = true;

        // --- ShowIf helpers ------------------------------------------------
        private bool IsFeedbackRef() => Source == StepSource.FeedbackRef;
        private bool IsInlineWait() => Source == StepSource.InlineWait;
        private bool IsInlineAnimation() => Source == StepSource.InlineAnimation;
        private bool IsInlineBehaviorValue() => Source == StepSource.InlineBehaviorValue;
        private bool StartNeedsStepIndex() => StartMode == StepStartMode.AfterStep;
        private bool StartNeedsEventKey() => StartMode == StepStartMode.OnEvent;
        private bool EndNeedsDuration() => EndMode == StepEndMode.OnDuration;
        private bool EndNeedsEventKey() => EndMode == StepEndMode.OnEvent;
    }
}
