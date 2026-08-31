namespace Rollgeon.Input
{
    /// <summary>
    /// Qué debe hacer el router global con el click derecho de este frame.
    /// </summary>
    public enum RightClickAction
    {
        None,
        /// <summary>Cancelar la selección de acción en curso (targeting de chain o tile de Movement).</summary>
        CancelSelection,
        /// <summary>Deseleccionar todos los dados holdeados (Balatro-style).</summary>
        DeselectAllDice,
    }

    /// <summary>
    /// Decisión pura del click derecho en combate. Prioridad: selección de acción
    /// abierta &gt; deseleccionar dados &gt; nada. Fuera del HUD de combate, con el
    /// click ya claimeado por un presenter (cancel de agarre de dados) o con una
    /// secuencia de UI en vuelo (breakdown N×M / outro de dados), no-op.
    /// </summary>
    /// <remarks>
    /// BUG-070: el botón derecho también es RotateModifier del map Camera — rotar
    /// la cámara durante la suma N×M disparaba CancelSelection sobre la fase del
    /// chain (los chips quedaban en alpha 0 sin OnBehaviorExecuted que los
    /// restaure) o DeselectAllDice (borraba los "+N" en plena animación). El gate
    /// cubre solo la ventana de la secuencia: un right-click sin drag durante el
    /// targeting normal sigue cancelando.
    /// </remarks>
    public static class RightClickCancelPolicy
    {
        public static RightClickAction Decide(
            bool combatHudActive,
            bool claimedByDiceGrab,
            bool hasCancellableSelection,
            bool anyDieSelected,
            bool uiSequencePending)
        {
            if (!combatHudActive || claimedByDiceGrab) return RightClickAction.None;
            if (uiSequencePending) return RightClickAction.None;
            if (hasCancellableSelection) return RightClickAction.CancelSelection;
            if (anyDieSelected) return RightClickAction.DeselectAllDice;
            return RightClickAction.None;
        }
    }
}
