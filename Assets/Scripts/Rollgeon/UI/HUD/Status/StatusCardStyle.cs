namespace Rollgeon.UI.HUD.Status
{
    /// <summary>Cómo se dibuja la tarjeta de un estado.</summary>
    public enum StatusCardStyle
    {
        /// <summary>Habla de la unidad.</summary>
        Unit = 0,

        /// <summary>
        /// Habla del <b>suelo</b> y no de la unidad, y por eso la fila que flota sobre la cabeza la
        /// saltea: un ícono de fuego encima del jefe se lee como que el jefe se está quemando, y lo
        /// que arde es el piso — que ya se ve, en el piso.
        /// </summary>
        /// <remarks>
        /// No decide la forma de la tarjeta. Con arte lleva su ícono igual que cualquier otra; el
        /// estilo dice de QUÉ habla, no cómo se dibuja.
        /// </remarks>
        Terrain = 1,
    }
}
