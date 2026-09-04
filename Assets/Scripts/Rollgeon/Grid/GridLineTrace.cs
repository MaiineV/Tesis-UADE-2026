using System;
using System.Collections.Generic;

namespace Rollgeon.Grid
{
    /// <summary>Por qué terminó un <see cref="GridLineTrace.Trace"/>.</summary>
    public enum LineTraceStop
    {
        /// <summary>Recorrió las <c>maxTiles</c> pedidas sin chocar con nada.</summary>
        MaxReached,

        /// <summary>Celda fuera de bounds o no transitable.</summary>
        Wall,

        /// <summary>Celda ocupada por otra entidad (distinta de <c>ignore</c>).</summary>
        Occupant,
    }

    /// <summary>Resultado de <see cref="GridLineTrace.Trace"/>.</summary>
    public readonly struct LineTraceResult
    {
        /// <summary>Celdas walkable y libres recorridas antes del corte, en orden desde el origen.</summary>
        public readonly IReadOnlyList<GridCoord> FreeCells;

        public readonly LineTraceStop Stop;

        /// <summary>Celda que cortó la línea. Solo significativa con <see cref="LineTraceStop.Wall"/>
        /// o <see cref="LineTraceStop.Occupant"/> — con <see cref="LineTraceStop.MaxReached"/> es la
        /// última celda libre recorrida.</summary>
        public readonly GridCoord HitCoord;

        /// <summary>Ocupante de <see cref="HitCoord"/>. <see cref="Guid.Empty"/> salvo
        /// <see cref="LineTraceStop.Occupant"/>.</summary>
        public readonly Guid Occupant;

        public LineTraceResult(IReadOnlyList<GridCoord> freeCells, LineTraceStop stop, GridCoord hitCoord, Guid occupant)
        {
            FreeCells = freeCells;
            Stop = stop;
            HitCoord = hitCoord;
            Occupant = occupant;
        }
    }

    /// <summary>
    /// Trazado en línea recta sobre la grilla (cargas, garfios, empujes en cadena): camina desde
    /// <c>from</c> hacia <c>dir</c> hasta <c>maxTiles</c>, una pared, o un ocupante.
    /// </summary>
    public static class GridLineTrace
    {
        /// <param name="ignore">Entidad a ignorar como ocupante (ej. el propio caster si estuviera
        /// en la línea por un footprint raro). Default = ninguna.</param>
        public static LineTraceResult Trace(IGridManager grid, GridCoord from, Cardinal dir, int maxTiles,
            Guid ignore = default)
        {
            var free = new List<GridCoord>();

            if (grid == null || maxTiles <= 0)
                return new LineTraceResult(free, LineTraceStop.MaxReached, from, Guid.Empty);

            var current = from;
            for (int i = 0; i < maxTiles; i++)
            {
                var next = dir.Step(current);

                if (!grid.InBounds(next) || !grid.IsWalkable(next))
                    return new LineTraceResult(free, LineTraceStop.Wall, next, Guid.Empty);

                if (grid.TryGetOccupant(next, out var occupant) && occupant != Guid.Empty && occupant != ignore)
                    return new LineTraceResult(free, LineTraceStop.Occupant, next, occupant);

                free.Add(next);
                current = next;
            }

            return new LineTraceResult(free, LineTraceStop.MaxReached, current, Guid.Empty);
        }
    }
}
