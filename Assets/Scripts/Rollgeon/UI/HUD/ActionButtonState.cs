namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Estado visual/funcional de un boton de behavior en el HUD de combate.
    /// Un solo <see cref="PlayerActionButtonsView"/> orquesta los 4 botones
    /// (Movement, BaseAttack, SpecialAttack, Healing) recomputando el estado
    /// segun fase del turno, seleccion actual, y uso previo.
    /// </summary>
    public enum ActionButtonState
    {
        /// <summary>No es turno del jugador, hay un chain corriendo, o las
        /// precondiciones (energia, range, etc.) no se cumplen.</summary>
        Locked,

        /// <summary>Listo para ser clickeado.</summary>
        Available,

        /// <summary>El slot seleccionado actualmente — el jugador esta rolleando
        /// o eligiendo target con esta accion. Visual: scale + glow.</summary>
        Selected,

        /// <summary>Ya se ejecuto en este turno y tiene BlockOnRepeat=true.</summary>
        Used,
    }
}
