using UnityEngine;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Las 4 direcciones cardinales en la grilla del juego. Convención del proyecto:
    /// <c>GridCoord.X</c> ↔ world X, <c>GridCoord.Y</c> ↔ world Z. North = +Z (= +Y de grid),
    /// East = +X, South = -Z, West = -X.
    /// </summary>
    public enum Cardinal
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    public static class CardinalExtensions
    {
        /// <summary>
        /// Rotación Y (en grados) que orienta el forward (+Z local) hacia esta dirección
        /// en world space. Usar como <c>Quaternion.Euler(0, dir.YawDegrees(), 0)</c>.
        /// </summary>
        public static float YawDegrees(this Cardinal dir) => dir switch
        {
            Cardinal.North => 0f,
            Cardinal.East  => 90f,
            Cardinal.South => 180f,
            Cardinal.West  => 270f,
            _              => 0f,
        };

        /// <summary>
        /// Quaternion world equivalente a <see cref="YawDegrees"/>. Conveniencia para
        /// asignar directamente a <c>transform.rotation</c>.
        /// </summary>
        public static Quaternion ToRotation(this Cardinal dir) =>
            Quaternion.Euler(0f, dir.YawDegrees(), 0f);

        /// <summary>
        /// Deriva la dirección cardinal dominante desde un delta entre dos coords. Si el
        /// delta es <c>(0,0)</c>, devuelve <paramref name="fallback"/> — útil para mantener
        /// el facing previo cuando el "movimiento" es no-op.
        /// </summary>
        /// <remarks>
        /// El eje con mayor magnitud absoluta gana. En empate (movimiento diagonal) gana X
        /// — arbitrario, pero consistente; el FP es 4-conexo así que no debería darse en
        /// movimiento normal, sí en facing hacia un target a distancia.
        /// </remarks>
        public static Cardinal FromDelta(GridCoord from, GridCoord to, Cardinal fallback = Cardinal.South)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (dx == 0 && dy == 0) return fallback;

            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
                return dx >= 0 ? Cardinal.East : Cardinal.West;
            return dy >= 0 ? Cardinal.North : Cardinal.South;
        }
    }
}
