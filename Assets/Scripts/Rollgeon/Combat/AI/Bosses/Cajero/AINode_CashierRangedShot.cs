using System;
using Rollgeon.Feedback;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Un id de feedback vacío significa "usá el canónico", no "sin presentación": Odin no corre
    /// field initializers, así que <c>ED_Boss_Cajero.asset</c> los deserializa vacíos.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CashierRangedShot : AINode_RangedShot
    {
        public override string NodeName => $"Cajero — Disparo ({Damage} a ≤ {Range})";

        protected override string ResolvedAnimFeedbackId =>
            string.IsNullOrEmpty(AnimFeedbackId) ? BossFeedbackIds.CajeroShotAnim : AnimFeedbackId;

        protected override string ResolvedImpactVfxFeedbackId =>
            string.IsNullOrEmpty(ImpactVfxFeedbackId) ? BossFeedbackIds.CajeroShotImpactVfx : ImpactVfxFeedbackId;

        protected override string ResolvedImpactFeelFeedbackId =>
            string.IsNullOrEmpty(ImpactFeelFeedbackId) ? BossFeedbackIds.CajeroShotImpactFeel : ImpactFeelFeedbackId;
    }
}
