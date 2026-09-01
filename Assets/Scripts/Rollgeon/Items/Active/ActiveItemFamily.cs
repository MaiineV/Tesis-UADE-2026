namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Familia conceptual de un item activo: define <b>que busca el jugador</b> en la
    /// tirada, y por lo tanto cual de las tres bandas es su mejor resultado.
    /// GDD "Ítems Activos" §24.
    /// </summary>
    /// <remarks>
    /// Las bandas se calculan siempre por tercios proporcionales sobre el rango del dado
    /// (<see cref="ActiveItemBands"/>), <b>salvo</b> <see cref="Precision"/> y
    /// <see cref="Control"/>, que tienen mecanismos propios. Esos dos quedan fuera de la
    /// fase 1 y hoy caen en el calculo por tercios — ver <see cref="ActiveItemBands"/>.
    /// </remarks>
    public enum ActiveItemFamily
    {
        /// <summary>Numero alto. Su mejor resultado es la banda positiva.</summary>
        Potencia = 0,

        /// <summary>Numero medio. La banda buena la define el item.</summary>
        Estabilidad = 1,

        /// <summary>
        /// Extremos: negativa y positiva son ambas buenas, la mixta es la banda debil.
        /// </summary>
        Riesgo = 2,

        /// <summary>
        /// Un valor exacto. <b>Mecanismo propio</b> (distancia al valor objetivo), fuera
        /// de la fase 1.
        /// </summary>
        Precision = 3,

        /// <summary>
        /// Paridad. <b>Mecanismo propio</b> (paridad objetivo + mitad superior del dado),
        /// fuera de la fase 1.
        /// </summary>
        Control = 4,

        /// <summary>Numero alto, como Potencia, pero con contrapartida obligatoria.</summary>
        Sacrificio = 5,
    }

    public static class ActiveItemFamilyExt
    {
        /// <summary>
        /// <c>true</c> si la familia resuelve sus bandas con un mecanismo propio en vez
        /// de los tercios proporcionales. Hoy solo lo usa el editor/validacion: ambas
        /// estan pendientes de implementar y caen en el calculo por tercios.
        /// </summary>
        public static bool HasCustomBandMechanism(this ActiveItemFamily family)
            => family == ActiveItemFamily.Precision || family == ActiveItemFamily.Control;
    }
}
