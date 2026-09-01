namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Banda de resultado de la tirada del dado propio de un item activo.
    /// GDD "Ítems Activos" §15.
    /// </summary>
    /// <remarks>
    /// <b>Ninguna banda es "no pasa nada".</b> El GDD es explicito: "la accion elegida
    /// siempre ocurre, lo que cambia es su calidad o consecuencia. No existe una rama de
    /// 'no pasa nada'". Un item sin efecto autorado para una banda es un item incompleto,
    /// no un caso valido.
    /// <para>
    /// <b>Alto no siempre es mejor.</b> Que banda representa el mejor resultado lo define
    /// la <see cref="ActiveItemFamily"/> del item, no el orden de este enum — Riesgo, por
    /// ejemplo, considera buenas la negativa y la positiva, y debil la mixta.
    /// </para>
    /// </remarks>
    public enum ActiveItemBand
    {
        /// <summary>
        /// Negativo suave. Nunca nulo: efecto reducido, retrasado, o con una
        /// contrapartida leve.
        /// </summary>
        Negative = 0,

        /// <summary>Mixto / inestable.</summary>
        Mixed = 1,

        /// <summary>
        /// El efecto mas fuerte del item, no necesariamente sin contrapartida.
        /// </summary>
        Positive = 2,
    }
}
