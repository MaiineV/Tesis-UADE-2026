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
    /// abierta &gt; deseleccionar dados &gt; nada. Fuera del HUD de combate o con el
    /// click ya claimeado por un presenter (cancel de agarre de dados), no-op.
    /// </summary>
    public static class RightClickCancelPolicy
    {
        public static RightClickAction Decide(
            bool combatHudActive,
            bool claimedByDiceGrab,
            bool hasCancellableSelection,
            bool anyDieSelected)
        {
            if (!combatHudActive || claimedByDiceGrab) return RightClickAction.None;
            if (hasCancellableSelection) return RightClickAction.CancelSelection;
            if (anyDieSelected) return RightClickAction.DeselectAllDice;
            return RightClickAction.None;
        }
    }
}
