using Patterns;
using Patterns.FSM;
using Rollgeon.Combat.Actions;
using Rollgeon.Effects.Selection;
using Rollgeon.Heroes;

namespace Rollgeon.Combat.FSM.States.PlayerTurn
{
    public sealed class PlayerExecutingSubState : BaseState<PlayerTurnSubContext, PlayerTurnSubInput>
    {
        internal PlayerIdleSubState Idle;

        private StateMachine<PlayerTurnSubContext, PlayerTurnSubInput> _ownerFSM;

        public PlayerExecutingSubState(PlayerTurnSubContext context) : base(context) { }

        public void SetOwnerFSM(StateMachine<PlayerTurnSubContext, PlayerTurnSubInput> fsm)
        {
            _ownerFSM = fsm;
        }

        public override void Enter(PlayerTurnSubInput input)
        {
            var action = Context.PendingAction;

            // Capturamos el callback antes de ejecutar: la transición a Idle (que dispara
            // ActionExecuted) limpia el contexto, así que lo guardamos local para invocarlo
            // tras la ejecución. Postergar este callback hasta acá es lo que evita que el
            // handoff desbloquee la UI antes de que la acción realmente corra (BUG-013).
            var onComplete = Context.OnActionComplete;
            Context.OnActionComplete = null;

            // Selección cancelada (ej. el jugador abortó el movimiento): no ejecutamos nada
            // — no se mueve ni se marca usado. El reembolso de la energía lo hace el caller
            // (CombatHandoffService.CancelPlayerSelection), BUG-013.
            bool cancelled = Context.SelectionResult != null && Context.SelectionResult.WasCancelled;

            if (action == null || cancelled)
            {
                if (action == null)
                    UnityEngine.Debug.LogWarning("[PlayerExecutingSubState] PendingAction is null — skipping");
                onComplete?.Invoke();
                _ownerFSM?.SendInput(PlayerTurnSubInput.ActionExecuted);
                return;
            }

            var behaviorCtx = Context.PendingBehaviorContext;
            if (behaviorCtx != null && behaviorCtx.SourceEntity == null)
                behaviorCtx.SourceEntity = new Rollgeon.Entities.Entity { Guid = Context.ActingGuid };

            if (behaviorCtx is HeroBehaviorContext heroCtx)
            {
                heroCtx.SelectionResult = Context.SelectionResult;
                UnityEngine.Debug.Log($"[PlayerExecutingSubState] Executing '{action.BehaviorName}' selectionCoord={heroCtx.SelectionResult?.FirstSelectedCoord} sourceGuid={Context.ActingGuid}");
            }
            else
            {
                UnityEngine.Debug.Log($"[PlayerExecutingSubState] Executing '{action.BehaviorName}' (no HeroBehaviorContext)");
            }

            bool energyPrepaid = behaviorCtx is HeroBehaviorContext hbc && hbc.EnergyPrepaid;
            if (ServiceLocator.TryGetService<TurnManager>(out var tm))
            {
                if (energyPrepaid)
                    tm.TryExecuteEnergyPrepaid(action, Context.ActingGuid, behaviorCtx);
                else
                    tm.TryExecute(action, Context.ActingGuid, behaviorCtx);
            }
            else
                action.Execute(behaviorCtx);

            // La acción ya corrió (movió, atacó, etc.) — recién ahora notificamos al
            // caller para que libere el lock de la UI y emita OnBehaviorExecuted.
            onComplete?.Invoke();

            _ownerFSM?.SendInput(PlayerTurnSubInput.ActionExecuted);
        }

        public override bool CheckInput(PlayerTurnSubInput input,
            out BaseState<PlayerTurnSubContext, PlayerTurnSubInput> next)
        {
            if (input == PlayerTurnSubInput.ActionExecuted)
            {
                next = Idle;
                return true;
            }

            next = null;
            return false;
        }
    }
}
