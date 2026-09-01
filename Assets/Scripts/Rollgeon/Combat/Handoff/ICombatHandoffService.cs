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
        /// esperando target). False con dados en el aire o forced reroll pendiente.
        /// El Movement pendiente de tile NO entra acá — su cancel es la tecla X
        /// (<see cref="TryCancelMovementSelection"/>), igual que en exploración.
        /// </summary>
        bool HasCancellableSelection { get; }

        /// <summary>
        /// Cancel rápido por click derecho (QoL): targeting de chain. Nunca cuesta
        /// energía (los paths cancelables son pre-cobro). Devuelve <c>true</c> si
        /// canceló algo.
        /// </summary>
        bool TryCancelFromRightClick();

        /// <summary>
        /// True si hay un Movement esperando su tile destino que se puede cancelar
        /// (ver <see cref="TryCancelMovementSelection"/>). False con dados en el
        /// aire o forced reroll pendiente — ahí la UI debe lockear los slots en vez
        /// de prometer un switch que el handoff va a ignorar.
        /// </summary>
        bool IsMovementSelectionCancellable { get; }

        /// <summary>
        /// Cancela un Movement esperando su tile destino (hotkey <c>X</c>, mismo
        /// gesto que el cancel de caminata en exploración). Con dado de Movimiento
        /// ya tirado el roll pagado NO se reembolsa (§6.6 revertido); en el path
        /// legacy el roll nunca se cobró. Devuelve <c>true</c> si canceló.
        /// </summary>
        bool TryCancelMovementSelection();
    }
}
