using System;
using Patterns;
using Rollgeon.Items.Active.Blood;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si <c>OwnerGuid</c> NO tiene una carga de Blood D6 pendiente (Feature#0084:
    /// "ningún Blood D6 pendiente" es prerrequisito de activación). Sin servicio registrado:
    /// permisivo (true) — mismo criterio que <c>PcOwnerStatCompare</c>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcBloodD6Ready : BasePreCondition
    {
        public override string ConditionName => "Blood D6 ready (sin carga pendiente)";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return true;
            if (!ServiceLocator.TryGetService<IBloodD6Service>(out var service) || service == null) return true;

            return !service.HasPending(context.OwnerGuid);
        }
    }
}
