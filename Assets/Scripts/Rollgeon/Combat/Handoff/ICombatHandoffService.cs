using System;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Orchestrates the transition from exploration into combat: spawns enemies,
    /// pushes the CombatHUD screen, and starts the combat FSM. Subscribes to
    /// <see cref="Patterns.EventName.OnCombatTriggered"/> automatically.
    /// </summary>
    public interface ICombatHandoffService : IDisposable
    {
        bool IsHandoffInProgress { get; }

        /// <summary>
        /// Agenda un relanzamiento COMPLETO y gratuito de la mano activa del jugador
        /// (encantamiento "Torpe", BUG-030). No consume budget ni energía. Devuelve
        /// <c>false</c> si no hay mano principal activa (sin behavior con tirada,
        /// sin faces reveladas, throw en vuelo o ya hay un forced reroll pendiente).
        /// </summary>
        bool TryScheduleForcedFullHandReroll(Guid playerGuid, float delaySeconds = 0.35f);
    }
}
