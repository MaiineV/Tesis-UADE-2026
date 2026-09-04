using System.Collections.Generic;
using Rollgeon.Dice;

namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Reparte el rango de un dado en las bandas de resultado de un item activo, segun
    /// su <see cref="ActiveItemResolution"/>. GDD "Ítems Activos" §20 (modelo original,
    /// tercios) y Feature#0085 §A2 (cortes custom, binario, gradiente, jerarquia).
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
    /// ajustable en balance, no como bug. Un item puede fijar sus propios cortes
    /// (<c>ItemSO.NegativeMaxFace</c>/<c>MixedMaxFace</c>, 0 = tercios) para expresar
    /// rangos como el D4 1/2-3/4 de Probability Drive.
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
        /// <paramref name="faces"/> caras, por tercios proporcionales. Valores fuera de
        /// <c>[1, faces]</c> se clampean — un encantamiento no puede empujar el resultado
        /// fuera del dado (GDD §20, "Clamps").
        /// </summary>
        public static ActiveItemBand Resolve(int roll, int faces)
        {
            if (faces < 1) return ActiveItemBand.Negative;

            int clamped = Clamp(roll, faces);

            if (clamped <= NegativeMax(faces)) return ActiveItemBand.Negative;
            if (clamped <= MixedMax(faces)) return ActiveItemBand.Mixed;
            return ActiveItemBand.Positive;
        }

        /// <inheritdoc cref="Resolve(int,int)"/>
        public static ActiveItemBand Resolve(int roll, DiceType die) => Resolve(roll, die.MaxFace());

        /// <summary>Cara mas alta que todavia cae en la banda negativa (<c>Corte1</c>), por tercios.</summary>
        public static int NegativeMax(int faces) => faces / 3;

        /// <summary>Cara mas alta que todavia cae en la banda mixta (<c>Corte2</c>), por tercios.</summary>
        public static int MixedMax(int faces) => 2 * faces / 3;

        /// <summary>
        /// Rango inclusivo <c>[min, max]</c> de una banda por tercios, para el tooltip del
        /// HUD (el GDD pide mostrar la tabla de bandas del dado del item antes de
        /// activarlo, §18). Una banda puede quedar vacia si <c>min &gt; max</c>. No conoce
        /// cortes custom — para eso ver <see cref="DescribeStructure"/>.
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
        // Cortes custom (Feature#0085 §A2) — solo estructura Bands
        // ==================================================================

        /// <summary>
        /// Banda de <paramref name="roll"/> segun cortes explicitos. <c>negMaxFace</c>/
        /// <c>mixedMaxFace</c> en <c>0</c> caen a los tercios proporcionales
        /// (<see cref="Resolve(int,int)"/>) — es el mismo contrato que
        /// <c>ItemSO.NegativeMaxFace</c>/<c>MixedMaxFace</c>.
        /// </summary>
        public static ActiveItemBand ResolveCuts(int roll, int faces, int negMaxFace, int mixedMaxFace)
        {
            if (faces < 1) return ActiveItemBand.Negative;

            int clamped = Clamp(roll, faces);
            int negMax = negMaxFace > 0 ? negMaxFace : NegativeMax(faces);
            int mixedMax = mixedMaxFace > 0 ? mixedMaxFace : MixedMax(faces);

            if (clamped <= negMax) return ActiveItemBand.Negative;
            if (clamped <= mixedMax) return ActiveItemBand.Mixed;
            return ActiveItemBand.Positive;
        }

        // ==================================================================
        // Binario (Feature#0085 §A2) — estructura Binary
        // ==================================================================

        /// <summary>
        /// Binario por paridad: <c>Positive</c> si la paridad de <paramref name="roll"/>
        /// coincide con <paramref name="positiveParity"/>, <c>Negative</c> si no. Nunca
        /// devuelve <see cref="ActiveItemBand.Mixed"/> — un item Binary no tiene banda
        /// mixta (2 grupos de efectos, no 3).
        /// </summary>
        public static ActiveItemBand ResolveBinary(int roll, int faces, ActiveItemParity positiveParity)
        {
            if (faces < 1) return ActiveItemBand.Negative;

            int clamped = Clamp(roll, faces);
            bool isEven = clamped % 2 == 0;
            bool matchesPositive = isEven == (positiveParity == ActiveItemParity.Even);
            return matchesPositive ? ActiveItemBand.Positive : ActiveItemBand.Negative;
        }

        // ==================================================================
        // Mecanismos propios de familia (GDD §24, TBD-22 resuelto) — dentro de Bands
        // ==================================================================

        /// <summary>
        /// Banda segun la estructura y (dentro de <see cref="ActiveItemResolution.Bands"/>)
        /// la familia del item. <see cref="ActiveItemFamily.Precision"/> y
        /// <see cref="ActiveItemFamily.Control"/> no usan tercios/cortes: tienen mecanismo
        /// propio. <see cref="ActiveItemResolution.Binary"/> resuelve por paridad.
        /// <see cref="ActiveItemResolution.Gradient"/>/<see cref="ActiveItemResolution.Hierarchy"/>
        /// usan tercios solo como "banda de feel" (color/intensidad del HUD) — el grupo de
        /// efectos que corre siempre es <c>OnPositiveBand</c>, ver <see cref="ItemSO.GetEffectsFor"/>.
        /// </summary>
        public static ActiveItemBand Resolve(int roll, ItemSO item)
        {
            if (item == null) return ActiveItemBand.Negative;

            int faces = item.ActiveDie.MaxFace();
            switch (item.ActiveResolution)
            {
                case ActiveItemResolution.Binary:
                    return ResolveBinary(roll, faces, item.BinaryPositiveParity);

                case ActiveItemResolution.Gradient:
                case ActiveItemResolution.Hierarchy:
                    return Resolve(roll, faces);

                default:
                    switch (item.ActiveFamily)
                    {
                        case ActiveItemFamily.Precision:
                            return ResolvePrecision(roll, faces, item.PrecisionTarget);
                        case ActiveItemFamily.Control:
                            return ResolveControl(roll, faces, item.ControlParity);
                        default:
                            return ResolveCuts(roll, faces, item.NegativeMaxFace, item.MixedMaxFace);
                    }
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
        // Resolucion completa (Feature#0085 §A2)
        // ==================================================================

        /// <summary>
        /// Resuelve <paramref name="roll"/> (ya con el ajuste del encantamiento aplicado)
        /// contra <paramref name="item"/> y arma el <see cref="ActiveItemRollResolution"/>
        /// completo: cara, banda y magnitud. <c>Face</c> y <c>RawFace</c> quedan iguales —
        /// para diferenciarlos (cara cruda vs. post-encantamiento) usar el overload de 3
        /// argumentos.
        /// </summary>
        public static ActiveItemRollResolution ResolveRoll(int roll, ItemSO item)
            => ResolveRoll(roll, roll, item);

        /// <summary>
        /// <inheritdoc cref="ResolveRoll(int,ItemSO)"/> <paramref name="rawRoll"/> es la
        /// cara cruda (antes del encantamiento); <paramref name="roll"/> es la que decide
        /// la banda y la magnitud.
        /// </summary>
        public static ActiveItemRollResolution ResolveRoll(int rawRoll, int roll, ItemSO item)
        {
            int faces = item != null ? item.ActiveDie.MaxFace() : 6;
            int face = Clamp(roll, faces);
            int rawFace = Clamp(rawRoll, faces);
            var band = Resolve(roll, item);
            var structure = item != null ? item.ActiveResolution : ActiveItemResolution.Bands;
            bool isMagnitude = structure == ActiveItemResolution.Gradient
                                || structure == ActiveItemResolution.Hierarchy;
            int magnitude = isMagnitude ? face : 0;

            return new ActiveItemRollResolution(face, rawFace, faces, band, structure, magnitude);
        }

        // ==================================================================
        // Validacion (Feature#0085 §A1)
        // ==================================================================

        /// <summary>
        /// Valida la configuracion de resolucion del item. <c>false</c> con
        /// <paramref name="error"/> describiendo el primer problema encontrado.
        /// </summary>
        /// <remarks>
        /// Bands: si hay cortes custom (alguno de los dos &gt; 0) tienen que cumplir
        /// <c>1 &lt;= NegativeMaxFace &lt; MixedMaxFace &lt; Faces</c>; ademas ninguna
        /// banda puede quedar sin caras (el GDD prohibe "no pasa nada"). Binary: el dado
        /// tiene que tener caras pares (si no, la paridad no reparte parejo). Gradient/
        /// Hierarchy: sin restricciones propias — un solo grupo siempre corre.
        /// </remarks>
        public static bool Validate(ItemSO item, out string error)
        {
            error = null;
            if (item == null)
            {
                error = "item nulo.";
                return false;
            }

            int faces = item.ActiveDie.MaxFace();

            switch (item.ActiveResolution)
            {
                case ActiveItemResolution.Binary:
                    if (faces % 2 != 0)
                    {
                        error = $"Binary requiere un dado de caras pares (d{faces} no sirve).";
                        return false;
                    }
                    return true;

                case ActiveItemResolution.Gradient:
                case ActiveItemResolution.Hierarchy:
                    return true;

                default: // Bands
                    if (item.NegativeMaxFace > 0 || item.MixedMaxFace > 0)
                    {
                        bool valid = item.NegativeMaxFace >= 1
                                     && item.NegativeMaxFace < item.MixedMaxFace
                                     && item.MixedMaxFace < faces;
                        if (!valid)
                        {
                            error = "cortes invalidos: se requiere 1 <= NegativeMaxFace < MixedMaxFace < Faces "
                                    + $"(NegativeMaxFace={item.NegativeMaxFace}, MixedMaxFace={item.MixedMaxFace}, Faces={faces}).";
                            return false;
                        }
                    }

                    if (FacesOf(ActiveItemBand.Negative, item).Count == 0
                        || FacesOf(ActiveItemBand.Mixed, item).Count == 0
                        || FacesOf(ActiveItemBand.Positive, item).Count == 0)
                    {
                        error = "hay una banda sin caras — el GDD prohibe la rama de 'no pasa nada'.";
                        return false;
                    }
                    return true;
            }
        }

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

        /// <summary>
        /// Filas <c>(Label, Faces)</c> para el tooltip del HUD y el comando de consola
        /// <c>activeitem</c> — una fila por grupo de efectos real del item, con las caras
        /// que lo disparan como texto compacto. Reemplaza mostrar 3 bandas fijas: un
        /// Binary tiene 2, un Gradient/Hierarchy 1 sola ("Al resolver").
        /// </summary>
        public static IReadOnlyList<(string Label, string Faces)> DescribeStructure(ItemSO item)
        {
            var rows = new List<(string, string)>();
            if (item == null) return rows;

            int faces = item.ActiveDie.MaxFace();

            switch (item.ActiveResolution)
            {
                case ActiveItemResolution.Binary:
                    bool positiveIsEven = item.BinaryPositiveParity == ActiveItemParity.Even;
                    rows.Add(("negativa", DescribeParityFaces(faces, !positiveIsEven)));
                    rows.Add(("positiva", DescribeParityFaces(faces, positiveIsEven)));
                    return rows;

                case ActiveItemResolution.Gradient:
                    rows.Add(("al resolver — magnitud = cara", $"1-{faces}"));
                    return rows;

                case ActiveItemResolution.Hierarchy:
                    rows.Add(("al resolver — niveles = cara", $"1-{faces}"));
                    return rows;

                default:
                    rows.Add(("negativa", DescribeFaces(ActiveItemBand.Negative, item)));
                    rows.Add(("mixta", DescribeFaces(ActiveItemBand.Mixed, item)));
                    rows.Add(("positiva", DescribeFaces(ActiveItemBand.Positive, item)));
                    return rows;
            }
        }

        private static string DescribeParityFaces(int faces, bool even)
        {
            var list = new List<int>();
            for (int i = 1; i <= faces; i++)
                if ((i % 2 == 0) == even) list.Add(i);

            if (list.Count == 0) return "—";
            return string.Join(", ", list);
        }
    }
}
