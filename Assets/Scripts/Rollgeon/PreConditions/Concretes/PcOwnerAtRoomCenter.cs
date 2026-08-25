using System;
using Patterns;
using Rollgeon.Grid;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si el owner está parado en la casilla que <see cref="RoomCenterResolver"/> resuelve
    /// como centro de la sala — la misma a la que reubica
    /// <see cref="Rollgeon.Combat.AI.Decisions.AINode_TeleportToRoomCenter"/>. Pensada para ir
    /// negada dentro de un <see cref="PCComposite"/> (Mode = Not): el ataque sorpresa del Croupier
    /// necesita "HP bajo Y no estar ya en el centro", para no repetir el salto si el jefe llegó ahí
    /// por otro motivo (ya no hay sorpresa que dar).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcOwnerAtRoomCenter : BasePreCondition
    {
        public override string ConditionName => "Owner at room center";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)) return false;
            if (!grid.TryGetPosition(context.OwnerGuid, out var ownerCoord)) return false;

            if (!RoomCenterResolver.TryResolve(grid, context.OwnerGuid, ownerCoord, out var center))
                return false;

            return center == ownerCoord;
        }
    }
}
