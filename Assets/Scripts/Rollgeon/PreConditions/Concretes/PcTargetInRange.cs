using System;
using Patterns;
using Rollgeon.Attributes.Stats;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si el opponent del context está a ≤ <see cref="Range"/> del owner según
    /// <see cref="Metric"/>. Reemplaza a <c>AICond_PlayerInRange</c> generalizado al
    /// target ya resuelto por el contenedor (no asume que sea siempre el player).
    /// </summary>
    /// <remarks>
    /// Distinto de <see cref="PCEntityInRange"/> en intención: este es el equivalente
    /// directo del viejo AI-side check. Misma matemática, mismo fallback false si owner
    /// u opponent no están en grid.
    /// </remarks>
    /// <summary>Restricción de alineación entre owner y target (fichas GDD: Skirmisher solo
    /// diagonales, Sniper misma fila/columna).</summary>
    public enum TargetAlignment
    {
        Any = 0,
        /// <summary>Misma fila o columna exacta (dx == 0 xor dy == 0).</summary>
        SameRowOrColumn = 1,
        /// <summary>Diagonal exacta (|dx| == |dy|, ambos ≠ 0).</summary>
        DiagonalOnly = 2,
    }

    [Serializable, HideReferenceObjectPicker]
    public sealed class PcTargetInRange : BasePreCondition
    {
        [MinValue(0)]
        public int Range = 1;

        [Tooltip("Usa el rango de ataque de la ficha del owner (atributo AttackRange, resuelto " +
                 "por tier y modificable por buffs) en vez de Range. Sin atributo (ej. el owner " +
                 "es el jugador, o una ficha vieja con rango 0) cae a Range.")]
        public bool UseOwnerAttackRange;

        public DistanceMetric Metric = DistanceMetric.Manhattan;

        [Tooltip("Restricción de alineación: solo diagonales (Skirmisher) o misma fila/columna (Sniper). " +
                 "Se evalúa sobre el par de celdas más cercano entre ambos footprints.")]
        public TargetAlignment Alignment = TargetAlignment.Any;

        [Tooltip("Si true, el gate mide s\u00f3lo distancia y alineaci\u00f3n \u2014 para ataques que " +
                 "arquean o atraviesan (mismo campo que AINode_TelegraphMark y AINode_RangedShot). " +
                 "Default false: ver al target es parte de estar a tiro.")]
        public bool IgnoreLineOfSight;

        // NOTA: "estar a tiro" incluye VER al target — la línea de visión (Bresenham, cualquier
        // ángulo, GridLineOfSight) es parte del gate, siempre. Reemplaza al viejo flag
        // RequireLineOfSight (línea recta que fallaba sin alineación): los assets que lo
        // serializaron lo dropean en silencio al deserializar. Alcanza también al gate de heal
        // del Healer, a propósito: se cura a quien se ve.

        public override string ConditionName
        {
            get
            {
                var name = UseOwnerAttackRange
                    ? $"Target in {Metric} range ≤ AttackRange de la ficha (fallback {Range})"
                    : $"Target in {Metric} range ≤ {Range}";
                if (Alignment == TargetAlignment.SameRowOrColumn) name += " (fila/columna)";
                else if (Alignment == TargetAlignment.DiagonalOnly) name += " (diagonal)";
                return IgnoreLineOfSight ? name : name + " +LoS";
            }
        }

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null) return false;
            if (context.OwnerGuid == Guid.Empty || context.OpponentGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)) return false;

            if (!grid.TryGetPosition(context.OwnerGuid, out var ownerCoord)) return false;
            if (!grid.TryGetPosition(context.OpponentGuid, out var opponentCoord)) return false;

            // Rect-a-rect (Fase B): la distancia se mide desde la celda más cercana de cada
            // footprint — un 2×2 melee pegado por una celda no-ancla está "adyacente".
            // Para dos 1×1 equivale a la matemática de siempre.
            var ownerFp = grid.GetFootprint(context.OwnerGuid);
            var opponentFp = grid.GetFootprint(context.OpponentGuid);
            int dist = Metric == DistanceMetric.Manhattan
                ? GridFootprint.ManhattanDistance(ownerCoord, ownerFp, opponentCoord, opponentFp)
                : GridFootprint.ChebyshevDistance(ownerCoord, ownerFp, opponentCoord, opponentFp);

            // Rango efectivo: la ficha del owner si el designer lo pidió y existe; si no,
            // el campo Range de siempre (back-compat con árboles ya autorados y PCs de héroe).
            int range = Range;
            if (UseOwnerAttackRange && context.Attributes != null)
            {
                int fromSheet = context.Attributes
                    .GetAttributeModifiedValue<AttackRange, int>(context.OwnerGuid);
                if (fromSheet > 0) range = fromSheet;
            }

            if (dist > range) return false;

            // Par de celdas más cercano entre ambos footprints (desempate: orden de iteración,
            // determinista — row-major desde el ancla). Para dos 1×1 son sus celdas de siempre.
            // Se computa SIEMPRE: la alineación y la línea de visión se miden sobre este par.
            var a = ownerCoord;
            var b = opponentCoord;
            int bestPair = int.MaxValue;
            foreach (var oc in grid.OccupiedCells(context.OwnerGuid))
            {
                foreach (var tc in grid.OccupiedCells(context.OpponentGuid))
                {
                    int d = oc.Manhattan(tc);
                    if (d < bestPair) { bestPair = d; a = oc; b = tc; }
                }
            }

            int dx = b.X - a.X;
            int dy = b.Y - a.Y;
            bool orthoAligned = (dx == 0) != (dy == 0); // misma fila o columna, no la misma celda
            bool diagAligned = dx != 0 && Math.Abs(dx) == Math.Abs(dy);

            if (Alignment == TargetAlignment.SameRowOrColumn && !orthoAligned) return false;
            if (Alignment == TargetAlignment.DiagonalOnly && !diagAligned) return false;

            // Detrás de algo no hay tiro (ni cura): Bresenham a cualquier ángulo. Sobre pares
            // alineados camina exactamente la línea recta del viejo RequireLineOfSight.
            return IgnoreLineOfSight
                   || GridLineOfSight.HasClearLine(grid, a, b, context.OwnerGuid, context.OpponentGuid);
        }
    }
}
