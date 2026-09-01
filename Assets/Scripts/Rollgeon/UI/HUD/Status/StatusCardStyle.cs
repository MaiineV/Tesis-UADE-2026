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

        /// <summary>
        /// Habla de lo que la unidad <b>es</b> — su punto débil, su kit — no de algo que le está
        /// pasando. La fila sobre la cabeza la saltea: ahí viven los estados transitorios, y un
        /// rasgo permanente flotando todo el combate es ruido. En el panel sale como slot,
        /// igual que todo.
        /// </summary>
        Trait = 2,
    }
}
