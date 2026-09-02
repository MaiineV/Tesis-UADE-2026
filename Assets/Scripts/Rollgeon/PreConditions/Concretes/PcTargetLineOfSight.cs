using System;
using Patterns;
using Rollgeon.Grid;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si el owner tiene línea de visión libre hacia el opponent, en CUALQUIER ángulo
    /// (Bresenham vía <see cref="GridLineOfSight"/>) — a diferencia del <c>RequireLineOfSight</c>
    /// de <see cref="PcTargetInRange"/>, que solo camina líneas ortogonales o diagonales de 45°
    /// (correcto para un atacante que dispara en línea recta, como el Sniper; insuficiente para
    /// uno omnidireccional).
    /// </summary>
    /// <remarks>
    /// Atómica y componible a propósito, mismo criterio que <see cref="PcOwnerHasPendingTelegraph"/>:
    /// no reemplaza a <see cref="PcTargetInRange"/> ni le agrega un modo — se suma como condición
    /// AND aparte en el mismo <c>If.Conditions</c> (ej. junto a <c>PcTargetInRange(Alignment=Any,
    /// RequireLineOfSight=false)</c> para un atacante AoE en rango que igual necesita ver la
    /// celda puntual del target). No toca la semántica de línea recta que ya usan otras fichas.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcTargetLineOfSight : BasePreCondition
    {
        public override string ConditionName => "Target con línea de visión libre (cualquier ángulo)";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty || context.OpponentGuid == Guid.Empty)
                return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;

            if (!grid.TryGetPosition(context.OwnerGuid, out var ownerCoord)) return false;
            if (!grid.TryGetPosition(context.OpponentGuid, out var opponentCoord)) return false;

            return GridLineOfSight.HasClearLine(grid, ownerCoord, opponentCoord, context.OwnerGuid, context.OpponentGuid);
        }
    }
}
