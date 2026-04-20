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
    }
}
