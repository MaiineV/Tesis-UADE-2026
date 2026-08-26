namespace Rollgeon.UI.HUD.Status
{
    /// <summary>Cómo se dibuja la tarjeta de un estado.</summary>
    public enum StatusCardStyle
    {
        /// <summary>Habla de la unidad. Ícono a la izquierda y título alineado a él.</summary>
        Unit = 0,

        /// <summary>
        /// Habla del <b>suelo</b> y no de la unidad: va sin ícono y con el título centrado, y la
        /// fila que flota sobre la cabeza la saltea. Es el único cambio de forma de la columna, y
        /// es lo que hace que se lea de un vistazo que esa tarjeta es de la casilla.
        /// </summary>
        Terrain = 1,
    }
}
