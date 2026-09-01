using System.Collections.Generic;
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

        // ==================================================================
        // Mecanismos propios de familia (GDD §24, TBD-22 resuelto)
        // ==================================================================

        /// <summary>
        /// Banda segun la familia del item. <see cref="ActiveItemFamily.Precision"/> y
        /// <see cref="ActiveItemFamily.Control"/> no usan tercios: tienen mecanismo
        /// propio. El resto cae en <see cref="Resolve(int,int)"/>.
        /// </summary>
        public static ActiveItemBand Resolve(int roll, ItemSO item)
        {
            if (item == null) return ActiveItemBand.Negative;

            int faces = item.ActiveDie.MaxFace();
            switch (item.ActiveFamily)
            {
                case ActiveItemFamily.Precision:
                    return ResolvePrecision(roll, faces, item.PrecisionTarget);
                case ActiveItemFamily.Control:
                    return ResolveControl(roll, faces, item.ControlParity);
                default:
                    return Resolve(roll, faces);
            }
        }

        /// <summary>
        /// Precision: la banda la da la distancia al valor objetivo del item.
        /// <code>
        /// Distancia = |resultado - objetivo|
        /// Positiva  si Distancia == 0   (acierto exacto)
        /// Mixta     si Distancia == 1   (casi)
        /// Negativa  si Distancia >= 2
        /// </code>
        /// </summary>
        /// <remarks>
        /// Con el objetivo en un extremo del dado (cara 1 o N) la mixta queda con una
        /// sola cara de margen en vez de dos. El GDD lo nombra como consecuencia natural
        /// de la formula, no como caso especial: acertar de casualidad igual tiene banda
        /// de consuelo, asi no se vuelve todo-o-nada.
        /// </remarks>
        public static ActiveItemBand ResolvePrecision(int roll, int faces, int target)
        {
            if (faces < 1) return ActiveItemBand.Negative;

            int clamped = Clamp(roll, faces);
            int clampedTarget = Clamp(target, faces);
            int distance = clamped > clampedTarget ? clamped - clampedTarget : clampedTarget - clamped;

            if (distance == 0) return ActiveItemBand.Positive;
            if (distance == 1) return ActiveItemBand.Mixed;
            return ActiveItemBand.Negative;
        }

        /// <summary>
        /// Control: cruza dos condiciones, la paridad objetivo y la mitad superior del
        /// dado.
        /// <code>
        /// Positiva  si coincide la paridad Y cae en la mitad superior
        /// Mixta     si se cumple exactamente una de las dos
        /// Negativa  si no se cumple ninguna
        /// </code>
        /// </summary>
        /// <remarks>
        /// Asume dados de caras pares (D4..D20, los estandar del juego) para que "mitad
        /// superior" quede sin resto.
        /// </remarks>
        public static ActiveItemBand ResolveControl(int roll, int faces, ActiveItemParity parity)
        {
            if (faces < 1) return ActiveItemBand.Negative;

            int clamped = Clamp(roll, faces);
            bool matchesParity = (clamped % 2 == 0) == (parity == ActiveItemParity.Even);
            bool upperHalf = clamped > faces / 2;

            if (matchesParity && upperHalf) return ActiveItemBand.Positive;
            if (matchesParity || upperHalf) return ActiveItemBand.Mixed;
            return ActiveItemBand.Negative;
        }

        private static int Clamp(int value, int faces)
            => value < 1 ? 1 : (value > faces ? faces : value);

        // ==================================================================
        // Caras por banda — para el tooltip
        // ==================================================================

        /// <summary>
        /// Caras del dado que caen en <paramref name="band"/>, para el item dado.
        /// A diferencia de <see cref="RangeOf(ActiveItemBand,int)"/> no asume que la
        /// banda sea un rango contiguo: en Precision y Control no lo es (Control con
        /// paridad par sobre D6 da mixta en 2 y 5, por ejemplo).
        /// </summary>
        public static List<int> FacesOf(ActiveItemBand band, ItemSO item)
        {
            var faces = new List<int>();
            if (item == null) return faces;

            int max = item.ActiveDie.MaxFace();
            for (int roll = 1; roll <= max; roll++)
            {
                if (Resolve(roll, item) == band) faces.Add(roll);
            }
            return faces;
        }

        /// <summary>
        /// Las caras de una banda como texto compacto: los tramos contiguos se colapsan
        /// ("1-2"), los sueltos se listan ("2, 5"). Vacio si la banda no tiene caras.
        /// </summary>
        public static string DescribeFaces(ActiveItemBand band, ItemSO item)
        {
            var faces = FacesOf(band, item);
            if (faces.Count == 0) return "—";

            var parts = new List<string>();
            int runStart = faces[0];
            int prev = faces[0];

            for (int i = 1; i <= faces.Count; i++)
            {
                bool contiguous = i < faces.Count && faces[i] == prev + 1;
                if (contiguous) { prev = faces[i]; continue; }

                parts.Add(runStart == prev ? runStart.ToString() : $"{runStart}-{prev}");
                if (i < faces.Count) { runStart = faces[i]; prev = faces[i]; }
            }

            return string.Join(", ", parts);
        }
    }
}
