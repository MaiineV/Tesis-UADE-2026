using Rollgeon.Dice;

namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Reparte el rango de un dado en las tres bandas de resultado de un item activo,
    /// por tercios proporcionales. GDD "Ítems Activos" §20.
    /// </summary>
    /// <remarks>
    /// <code>
    /// N      = caras del dado propio del item
    /// Corte1 = floor(N / 3)
    /// Corte2 = floor(2 * N / 3)
    ///
    /// Negativa  [1, Corte1]
    /// Mixta     [Corte1 + 1, Corte2]
    /// Positiva  [Corte2 + 1, N]
    /// </code>
    /// <para>
    /// Con <c>floor</c>, en dados donde N no es multiplo de 3 el remanente cae en la
    /// banda positiva (D4 reparte 1/1/2). El GDD lo marca como consecuencia conocida y
    /// ajustable en balance, no como bug.
    /// </para>
    /// <para>
    /// Clase pura, sin dependencias de Unity ni de servicios: es la unica fuente de
    /// verdad del reparto, y tanto la resolucion como el tooltip de rangos del HUD leen
    /// de aca para que no puedan divergir.
    /// </para>
    /// </remarks>
    public static class ActiveItemBands
    {
        /// <summary>
        /// Banda a la que pertenece <paramref name="roll"/> en un dado de
        /// <paramref name="faces"/> caras. Valores fuera de <c>[1, faces]</c> se
        /// clampean — un encantamiento no puede empujar el resultado fuera del dado
        /// (GDD §20, "Clamps").
        /// </summary>
        public static ActiveItemBand Resolve(int roll, int faces)
        {
            if (faces < 1) return ActiveItemBand.Negative;

            int clamped = roll < 1 ? 1 : (roll > faces ? faces : roll);

            if (clamped <= NegativeMax(faces)) return ActiveItemBand.Negative;
            if (clamped <= MixedMax(faces)) return ActiveItemBand.Mixed;
            return ActiveItemBand.Positive;
        }

        /// <inheritdoc cref="Resolve(int,int)"/>
        public static ActiveItemBand Resolve(int roll, DiceType die) => Resolve(roll, die.MaxFace());

        /// <summary>Cara mas alta que todavia cae en la banda negativa (<c>Corte1</c>).</summary>
        public static int NegativeMax(int faces) => faces / 3;

        /// <summary>Cara mas alta que todavia cae en la banda mixta (<c>Corte2</c>).</summary>
        public static int MixedMax(int faces) => 2 * faces / 3;

        /// <summary>
        /// Rango inclusivo <c>[min, max]</c> de una banda, para el tooltip del HUD (el
        /// GDD pide mostrar la tabla de bandas del dado del item antes de activarlo, §18).
        /// Una banda puede quedar vacia si <c>min &gt; max</c>.
        /// </summary>
        public static (int Min, int Max) RangeOf(ActiveItemBand band, int faces)
        {
            switch (band)
            {
                case ActiveItemBand.Negative:
                    return (1, NegativeMax(faces));
                case ActiveItemBand.Mixed:
                    return (NegativeMax(faces) + 1, MixedMax(faces));
                default:
                    return (MixedMax(faces) + 1, faces);
            }
        }

        /// <inheritdoc cref="RangeOf(ActiveItemBand,int)"/>
        public static (int Min, int Max) RangeOf(ActiveItemBand band, DiceType die)
            => RangeOf(band, die.MaxFace());
    }
}
