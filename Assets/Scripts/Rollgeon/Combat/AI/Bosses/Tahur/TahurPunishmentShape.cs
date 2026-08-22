using System;
using System.Collections.Generic;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// Una forma del Castigo del Tahúr: la forma dice cuánto le faltó al jugador para armar el
    /// canto.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class TahurPunishmentShape
    {
        [Tooltip("Forma del área. Column/Row se centran en donde estaba el jugador; " +
                 "ScatteredSquares reparte Count cuadrados de Size×Size por la sala.")]
        public ThreatShape Shape = ThreatShape.Column;

        [Tooltip("Ancho de la franja (Row/Column: 1 ⇒ la línea del jugador, 3 ⇒ ±1) o " +
                 "lado de cada cuadrado en ScatteredSquares (2 ⇒ 2×2).")]
        [MinValue(1)]
        public int Size = 1;

        [Tooltip("Cantidad de cuadrados independientes. Solo para ScatteredSquares.")]
        [MinValue(1)]
        [ShowIf(nameof(Shape), ThreatShape.ScatteredSquares)]
        public int Count = 4;

        /// <summary>Etiqueta corta para inspector / logs de debug del árbol.</summary>
        public string Label => Shape == ThreatShape.ScatteredSquares
            ? $"Scattered {Count}×{Size}"
            : $"{Shape} {Size}";

        /// <summary>
        /// Casillas del Castigo. <paramref name="playerCoord"/> es donde estaba el jugador al
        /// liquidar — el Castigo se centra ahí, nunca en la posición final del jefe.
        /// </summary>
        public HashSet<GridCoord> Compute(IGridManager grid, GridCoord playerCoord, System.Random rng)
        {
            if (grid == null) return new HashSet<GridCoord>();

            if (Shape == ThreatShape.ScatteredSquares)
                return ThreatAreaShape.ComputeScatteredSquares(grid, rng ?? new System.Random(), Count, Size);

            if (Shape == ThreatShape.DirectionalBand)
            {
                // La banda direccional necesita origen + destino y el Castigo no sale del jefe:
                // degrada a la columna del jugador.
                return ThreatAreaShape.Compute(grid, playerCoord, ThreatShape.Column, Size, HalfRoomAxis.Vertical);
            }

            return ThreatAreaShape.Compute(grid, playerCoord, Shape, Size, HalfRoomAxis.Vertical);
        }
    }
}
