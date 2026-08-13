namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Cómo se interpreta <see cref="SelectionSettings.Range"/> al resolver tiles válidos.
    /// Manhattan debe quedar en 0: assets serializados sin el campo caen acá y conservan
    /// el comportamiento previo (ataques, enemigos).
    /// </summary>
    public enum RangeMode
    {
        /// <summary>Distancia Manhattan pura — ignora paredes y conectividad (ataques).</summary>
        Manhattan = 0,

        /// <summary>BFS real por celdas caminables no ocupadas — solo tiles alcanzables (movimiento).</summary>
        PathReachable = 1,
    }
}
