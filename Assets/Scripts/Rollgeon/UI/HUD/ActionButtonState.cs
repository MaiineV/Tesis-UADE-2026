namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Estado visual/funcional de un boton de behavior en el HUD de combate.
    /// Un solo <see cref="PlayerActionButtonsView"/> orquesta los 4 botones
    /// (Movement, BaseAttack, ClassSkill, Healing) recomputando el estado
    /// segun fase del turno, seleccion actual, y uso previo.
    /// </summary>
    public enum ActionButtonState
    {
        /// <summary>No es turno del jugador, hay un chain corriendo, o las
        /// precondiciones (range, gate del tutorial, etc.) no se cumplen. La falta
        /// de energia NO entra aca — tiene su propio <see cref="Unaffordable"/>.</summary>
        Locked,

        /// <summary>Listo para ser clickeado.</summary>
        Available,

        /// <summary>El slot seleccionado actualmente — el jugador esta rolleando
        /// o eligiendo target con esta accion. Visual: scale + glow.</summary>
        Selected,

        /// <summary>Todo lo demas esta listo (es tu turno, no hay chain, esta en
        /// rango, no la usaste) pero no alcanza la energia. Se separa de
        /// <see cref="Locked"/> para poder decirle al jugador POR QUE no puede:
        /// costo en rojo persistente + shake al intentar usarla. Igual que Locked
        /// a nivel funcional — no arranca drag ni responde al hotkey.</summary>
        Unaffordable,
    }
}
