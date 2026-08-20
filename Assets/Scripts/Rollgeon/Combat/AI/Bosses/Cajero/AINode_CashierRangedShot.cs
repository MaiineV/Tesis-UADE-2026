using System;
using Rollgeon.Feedback;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// El disparo del Cajero: <see cref="AINode_RangedShot.Damage"/> directos al jugador a
    /// distancia <see cref="AINode_RangedShot.Range"/> o menos, sin área y sin telegráfico. Es lo
    /// que hace en los turnos en que no marca columna. Ficha de diseño "El Cajero" (piso 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Thin subclass de <see cref="AINode_RangedShot"/>.</b> La lógica de rango/daño ya era
    /// genérica —no dependía de tiers de oro, columnas ni de <c>CashierCounterTollService</c>—, así
    /// que se extrajo tal cual. Lo único hardcodeado eran los tres feedback ref ids del disparo.
    /// </para>
    /// <para>
    /// Quedan acá como fallback de <c>ED_Boss_Cajero.asset</c>: ese asset ya serializado no tiene
    /// los campos <c>AnimFeedbackId</c>/<c>ImpactVfxFeedbackId</c>/<c>ImpactFeelFeedbackId</c>
    /// (nacieron recién con este split) y Odin no corre field initializers al deserializar — sin
    /// este fallback el disparo del Cajero se quedaría mudo. Mismo idiom que
    /// <c>AINode_TahurPoke.AnimFeedbackIdOverride</c>, con los roles de "vacío" invertidos: para el
    /// Cajero vacío significa "usá el id canónico", no "sin presentación".
    /// </para>
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
