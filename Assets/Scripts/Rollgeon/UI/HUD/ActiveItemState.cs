namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Estado visual de un slot de item activo en el HUD.
    /// Plan §4.6.
    /// </summary>
    public enum ActiveItemState
    {
        /// <summary>El jugador no posee el item. Icono gris o invisible.</summary>
        Inactive = 0,

        /// <summary>El jugador lo posee y esta disponible para usar.</summary>
        Active = 1,

        /// <summary>Usado (ej: pocion consumida). Visualmente distinto de Inactive.</summary>
        Depleted = 2,
    }
}
