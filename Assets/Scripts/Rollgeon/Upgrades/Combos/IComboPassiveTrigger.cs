namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// Marker base de los triggers de combo passive (pasivas de combo que viven
    /// en la run y reaccionan al matcheo de un combo específico). Los concretos
    /// implementan uno o más de los <c>IOn*</c> hooks de este archivo. El service
    /// dispatcha los hooks vía cast por tipo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Triggers vs flat bonus.</b> El campo <c>FlatDamageBonus</c> del
    /// <see cref="ComboPassiveSO"/> cubre el caso simple (+X daño plano). Los
    /// triggers de este archivo cubren condiciones más ricas: "+3 oro cada vez
    /// que matchea escalera", "shield si el combo se activa con dado par", etc.
    /// </para>
    /// <para>
    /// <b>Readers cross-system.</b> Mismo patrón que enchantments — los concretos
    /// usan <c>EffectIntReader</c> para todos los valores numéricos.
    /// </para>
    /// </remarks>
    public interface IComboPassiveTrigger
    {
    }

    /// <summary>
    /// Dispara cuando el combo target del passive matchea sobre el roll final.
    /// El <c>ComboPassiveContext.Effect</c> contiene <c>DiceResult</c> +
    /// <c>ComboResult</c>; <c>ComboPassiveContext.ComboId</c> es el id del combo
    /// matched. Side effects van al <c>Scratch</c>.
    /// </summary>
    public interface IOnComboPassiveMatchedTrigger : IComboPassiveTrigger
    {
        void OnComboMatched(ComboPassiveContext ctx);
    }

    // ========================================================================
    // Hooks genéricos (no-combo) — mismo esquema que IEnchantmentTriggerHooks
    // del canal dice. Dispatchean a TODAS las pasivas de la run, sin importar
    // TargetComboId (que solo scopea OnComboMatched + FlatDamageBonus). Sufijo
    // "Passive" para no chocar con los hooks homónimos del canal dice.
    // Todos los contexts garantizan Effect + Scratch; ComboId es null.
    // ========================================================================

    /// <summary>
    /// Dispara al inicio del turno DEL PLAYER (los turnos de enemigos se filtran,
    /// mismo criterio que DiceEnchantmentService).
    /// </summary>
    public interface IOnTurnStartedPassiveTrigger : IComboPassiveTrigger
    {
        void OnTurnStarted(ComboPassiveContext ctx);
    }

    /// <summary>Dispara al finalizar el turno del player.</summary>
    public interface IOnTurnFinishedPassiveTrigger : IComboPassiveTrigger
    {
        void OnTurnFinished(ComboPassiveContext ctx);
    }

    /// <summary>
    /// Dispara al entrar a una sala. <c>ctx.RoomInstanceId</c> y <c>ctx.RoomId</c>
    /// populados (RoomId puede ser vacío).
    /// </summary>
    public interface IOnRoomEnteredPassiveTrigger : IComboPassiveTrigger
    {
        void OnRoomEntered(ComboPassiveContext ctx);
    }

    /// <summary>Dispara al iniciar un combate. <c>ctx.RoomInstanceId</c> populado.</summary>
    public interface IOnCombatStartPassiveTrigger : IComboPassiveTrigger
    {
        void OnCombatStart(ComboPassiveContext ctx);
    }

    /// <summary>
    /// Dispara al terminar un combate. <c>ctx.Outcome</c> populado —
    /// <c>CombatOutcome.None</c> = sentinel si el publisher no pasó outcome.
    /// </summary>
    public interface IOnCombatEndPassiveTrigger : IComboPassiveTrigger
    {
        void OnCombatEnd(ComboPassiveContext ctx);
    }

    /// <summary>
    /// Dispara cuando cambia el oro. <c>ctx.GoldTotal</c> / <c>ctx.GoldDelta</c>
    /// populados. Cambios de oro producidos por la aplicación del scratch de una
    /// pasiva NO re-disparan este hook (guard de reentrada del service).
    /// </summary>
    public interface IOnGoldChangedPassiveTrigger : IComboPassiveTrigger
    {
        void OnGoldChanged(ComboPassiveContext ctx);
    }

    /// <summary>Dispara con el roll crudo (post-dice, pre-reroll). <c>ctx.Effect.DiceResult</c> populado.</summary>
    public interface IOnDiceRolledPassiveTrigger : IComboPassiveTrigger
    {
        void OnDiceRolled(ComboPassiveContext ctx);
    }

    /// <summary>Dispara con el roll final lockeado tras los rerolls. <c>ctx.Effect.DiceResult</c> es la versión final.</summary>
    public interface IOnRollResolvedPassiveTrigger : IComboPassiveTrigger
    {
        void OnRollResolved(ComboPassiveContext ctx);
    }

    /// <summary>
    /// Dispara con cada daño resuelto (incluye enemigo→player y player→enemigo).
    /// <c>ctx.Damage</c> trae el payload completo; el trigger filtra por
    /// Source/Target vs player guid según su semántica.
    /// </summary>
    public interface IOnDamageResolvedPassiveTrigger : IComboPassiveTrigger
    {
        void OnDamageResolved(ComboPassiveContext ctx);
    }

    /// <summary>
    /// Dispara cuando el combo se JUEGA: la acción con combo matcheado se está
    /// ejecutando, ANTES de resolver el daño — a diferencia de
    /// <see cref="IOnComboPassiveMatchedTrigger"/>, que dispara en cada preview de
    /// hold. Scopeado por <c>TargetComboId</c> (vacío = cualquier combo).
    /// <c>ctx.Scratch</c> es el play scratch de la ventana: lo que se escriba ahí
    /// llega a <c>bono_combo</c> de ESTA resolución de daño.
    /// </summary>
    public interface IOnComboPlayedPassiveTrigger : IComboPassiveTrigger
    {
        void OnComboPlayed(ComboPassiveContext ctx);
    }
}
