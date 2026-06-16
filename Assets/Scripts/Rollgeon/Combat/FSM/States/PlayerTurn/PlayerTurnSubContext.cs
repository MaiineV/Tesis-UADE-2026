using System;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Heroes;

namespace Rollgeon.Combat.FSM.States.PlayerTurn
{
    public sealed class PlayerTurnSubContext
    {
        public CombatContext CombatContext;
        public Guid ActingGuid;
        public HeroActionBehavior PendingAction;
        public BehaviorContext PendingBehaviorContext;
        public TargetSelectionResult SelectionResult;

        /// <summary>
        /// Callback opcional que el sub-FSM invoca cuando la accion pedida via
        /// <see cref="States.PlayerTurnState.RequestAction"/> terminó de ejecutarse
        /// (tras resolver la selección de target si la requería). El handoff lo usa
        /// para postergar el <c>OnBehaviorExecuted</c> y la liberación del slot hasta
        /// que la acción realmente corre — sin esto la UI se desbloquea antes de tiempo
        /// y el jugador puede disparar otra acción en paralelo (BUG-013).
        /// </summary>
        public System.Action OnActionComplete;
    }
}
