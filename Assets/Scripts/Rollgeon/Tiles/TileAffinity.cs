namespace Rollgeon.Tiles
{
    /// <summary>
    /// A quién afecta la casilla (GDD, sección 11). Regla: Ground es afectada por todo;
    /// Flying ignora únicamente las casillas marcadas <see cref="GroundOnly"/>.
    /// </summary>
    public enum TileAffinity
    {
        /// <summary>Afecta a terrestres y voladoras (Fuego, Portal, Curación...).</summary>
        All = 0,

        /// <summary>Solo terrestres: una unidad Flying la ignora (Pinchos, Hielo, Veneno).</summary>
        GroundOnly = 1,
    }
}
