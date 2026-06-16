using System;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Una entry autoral del <see cref="FeedbackDBSO"/>. El <see cref="Type"/> controla
    /// qué campos condicionales son visibles en el inspector. TECHNICAL.md §10.2.
    /// </summary>
    [Serializable]
    public class FeedbackEntry
    {
        [Title("Identity")]
        [Tooltip("Id único. Lo referencian EffPlayFeedback y FeedbackSequenceStep.FeedbackRefId.")]
        public string FeedbackId;

        public FeedbackType Type;

        [Title("Positioning")]
        public SpawnPosition Position = SpawnPosition.AtTarget;

        [ShowIf(nameof(UsesPositionReader))]
        public ScriptableObject PositionReaderSO;

        [ShowIf(nameof(UsesPositionReader))]
        public FeedbackPlayer PlayerTarget = FeedbackPlayer.Player;

        public Vector3 PositionOffset;

        [Title("Completion")]
        [Tooltip("Fallback timer. Usado siempre que no haya listener natural.")]
        [MinValue(0f)]
        public float Duration = 1f;

        public FeedbackCompletionMode CompletionMode = FeedbackCompletionMode.Timer;

        // ================================================================
        // VFX
        // ================================================================
        [Title("VFX"), ShowIf(nameof(IsVFX))]
        public GameObject VfxPrefab;

        [ShowIf(nameof(IsVFX))]
        public bool ShouldDestroyOnParticleEnd = true;

        // ================================================================
        // SFX
        // ================================================================
        [Title("SFX"), ShowIf(nameof(IsSFX))]
        public AudioClip AudioClip;

        [ShowIf(nameof(IsSFX)), Range(0f, 1f)]
        public float Volume = 1f;

        // ================================================================
        // Animation
        // ================================================================
        [Title("Animation"), ShowIf(nameof(IsAnimation))]
        public string AnimTrigger;

        [ShowIf(nameof(IsAnimation))]
        [Tooltip("Si true, se aplica el trigger al pawn source; si false, al target.")]
        public bool TargetSourcePawn = true;

        // ================================================================
        // BehaviorValue
        // ================================================================
        [Title("Behavior Value"), ShowIf(nameof(IsBehaviorValue))]
        public BehaviorValueKey BehaviorValueKey = BehaviorValueKey.None;

        [ShowIf(nameof(IsBehaviorValue))]
        public BehaviorValueTarget ValueTarget = BehaviorValueTarget.Target;

        // ================================================================
        // Floating number
        // ================================================================
        [Title("Floating Number"), ShowIf(nameof(IsFloatingNumber))]
        public BehaviorValueKey FloatingNumberSourceKey = BehaviorValueKey.FloatingDamage;

        // --- ShowIf helpers ------------------------------------------------
        private bool IsVFX() => Type == FeedbackType.VFX;
        private bool IsSFX() => Type == FeedbackType.SFX;
        private bool IsAnimation() => Type == FeedbackType.Animation;
        private bool IsBehaviorValue() => Type == FeedbackType.BehaviorValue;
        private bool IsFloatingNumber() => Type == FeedbackType.FloatingNumber;
        private bool UsesPositionReader() => Position == SpawnPosition.FromReader;
    }
}
