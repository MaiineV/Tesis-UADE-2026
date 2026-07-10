using Rollgeon.Heroes;

namespace Rollgeon.Tutorial
{
    /// <summary>
    /// Gate de acciones del tutorial: qué <see cref="HeroBehaviorSlot"/>s están
    /// bloqueados hasta que el paso correspondiente los desbloquee. Registrado en
    /// <c>ServiceScope.Run</c> SOLO durante el tutorial — ausente del locator,
    /// todo está desbloqueado (los consumidores degradan con TryGetService).
    /// <para>
    /// Consumidores: <c>TurnManager.IsForbiddenByRuleset</c> (backstop de ejecución),
    /// <c>PlayerActionButtonsView</c> / <c>ExplorationActionButtonsView</c> (visual
    /// Locked). Cada unlock dispara <c>EventName.OnTutorialActionUnlocked(slot)</c>.
    /// </para>
    /// </summary>
    public interface ITutorialActionGateService
    {
        bool IsSlotLocked(HeroBehaviorSlot slot);

        /// <summary>
        /// ¿El actionId (=<c>HeroActionBehavior.ActionName</c>) corresponde a un slot
        /// bloqueado del héroe del tutorial? ActionIds desconocidos nunca bloquean.
        /// </summary>
        bool IsActionLocked(string actionId);

        /// <summary>Desbloquea el slot y dispara <c>OnTutorialActionUnlocked</c>. Idempotente.</summary>
        void Unlock(HeroBehaviorSlot slot);
    }
}
