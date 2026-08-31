namespace Rollgeon.Items
{
    /// <summary>
    /// Motivo por el que un item activo no se puede usar en este momento. Lo devuelve
    /// <see cref="IInventoryService.CanActivateItem"/> para que el HUD pinte el slot y
    /// muestre el rechazo <b>antes</b> del click, en vez de que
    /// <see cref="IInventoryService.ActivateItem"/> falle en silencio.
    /// </summary>
    /// <remarks>
    /// Enum y no string: el texto se localiza en la vista (<c>UiTextKeys</c>), así los
    /// mensajes en inglés de <c>TurnManager.CanExecute</c> no se filtran a la UI.
    /// </remarks>
    public enum ItemActivationBlock
    {
        /// <summary>Se puede usar.</summary>
        None = 0,

        /// <summary>Indice fuera de rango de <c>ActiveItems</c>.</summary>
        InvalidSlot = 1,

        /// <summary>El slot esta vacio o el item no es <see cref="ItemType.Active"/>.</summary>
        NotActiveItem = 2,

        /// <summary>Todavia en cooldown tras un uso previo.</summary>
        OnCooldown = 3,

        /// <summary>Consume accion y el pool de rolls esta vacio (solo en combate).</summary>
        NotEnoughRolls = 4,

        /// <summary>El ruleset activo prohibe la accion, o falta el <c>TurnManager</c>.</summary>
        ForbiddenByRuleset = 5,

        /// <summary>Alguna precondicion del <c>OnActivate</c> no se cumple.</summary>
        PreconditionFailed = 6,

        /// <summary>
        /// Consume accion y no es el turno del jugador. Solo aplica a items con
        /// <c>ConsumesAction</c>: los gratis se pueden usar en el turno enemigo.
        /// </summary>
        NotYourTurn = 7,

        /// <summary>
        /// Otro item con el mismo <c>ActionId</c> ya se uso en este turno.
        /// </summary>
        ActionAlreadyUsed = 8,

        /// <summary>El jugador cancelo la seleccion de objetivo.</summary>
        SelectionCancelled = 9,
    }
}
