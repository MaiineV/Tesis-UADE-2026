using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Targeting
{
    /// <summary>
    /// Selector default — siempre devuelve <see cref="AIContext.PlayerGuid"/>. Sirve como
    /// fallback cuando un nodo / behavior / effect data no especifica selector propio.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class TargetSelector_AlwaysPlayer : BaseEnemyTargetSelector
    {
        public override string SelectorName => "Always Player";

        public override Guid PickTarget(AIContext ctx, Guid ownerGuid)
        {
            return ctx?.PlayerGuid ?? Guid.Empty;
        }
    }
}
