using System;
using Patterns;
using Rollgeon.Combat.Threat;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si el owner tiene una marca de <see cref="AINode_TelegraphMark"/> pendiente de
    /// cobrar (todavía no pasó por <see cref="AINode_ExecuteTelegraph"/>). Deja ramificar el
    /// presupuesto de acciones del turno: si hay algo que resolver, ese golpe/whiff cuenta como
    /// una acción y solo queda lugar para una más (mover O re-marcar); si no hay nada pendiente,
    /// el turno queda libre para mover Y marcar.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcOwnerHasPendingTelegraph : BasePreCondition
    {
        public override string ConditionName => "Owner tiene telegraph pendiente";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null) return false;

            return threat.TryPeek(context.OwnerGuid, out _);
        }
    }
}
