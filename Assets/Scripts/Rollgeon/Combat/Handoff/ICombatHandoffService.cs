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

        /// <summary>
        /// True si hay una selección de acción en curso que el click derecho puede
        /// cancelar limpiamente: targeting de chain (fase 0 pre-roll, o AfterRoll
        /// esperando target) o un Movement esperando su tile. False con dados en el
        /// aire o forced reroll pendiente.
        /// </summary>
        bool HasCancellableSelection { get; }

        /// <summary>
        /// Cancel rápido por click derecho (QoL): prioridad targeting de chain, después
        /// selección de tile pendiente. Nunca cuesta energía (los paths cancelables son
        /// pre-cobro). Devuelve <c>true</c> si canceló algo.
        /// </summary>
        bool TryCancelFromRightClick();
    }
}
