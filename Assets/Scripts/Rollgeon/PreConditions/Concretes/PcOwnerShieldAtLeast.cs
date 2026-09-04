using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si <c>OwnerGuid</c> tiene al menos <see cref="Min"/> de escudo (Feature#0085,
    /// Coin Shield: "no puede usarse con 0 de escudo").
    /// </summary>
    /// <remarks>
    /// <see cref="PcOwnerStatCompare"/> ya cubre este caso genéricamente
    /// (<c>StatType.Shield</c> + <c>GreaterOrEqual</c>), pero un Pc dedicado con nombre
    /// propio y default <c>Min = 1</c> es más legible en el inspector del item (mismo
    /// criterio que <see cref="PcEligibleEnemyExists"/> al lado de <c>PcAllyAliveExists</c>).
    /// Reporta ambigüedad resuelta a favor de la claridad de autoría.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcOwnerShieldAtLeast : BasePreCondition
    {
        [MinValue(0)]
        public int Min = 1;

        public override string ConditionName => $"Owner.Shield >= {Min}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return false;

            int shield = attrs.GetAttributeModifiedValue<Shield, int>(context.OwnerGuid);
            return shield >= Min;
        }
    }
}
