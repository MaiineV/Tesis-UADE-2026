using System;
using Rollgeon.Feedback;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// El disparo del Cajero: <see cref="AINode_RangedShot.Damage"/> directos al jugador a
    /// distancia <see cref="AINode_RangedShot.Range"/> o menos, sin área y sin telegráfico.
    /// </summary>
    /// <remarks>
    /// Un id de feedback vacío significa "usá el canónico", no "sin presentación": Odin no corre
    /// field initializers, así que <c>ED_Boss_Cajero.asset</c> los deserializa vacíos y sin este
    /// fallback el disparo se quedaría mudo.
    /// </remarks>
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
