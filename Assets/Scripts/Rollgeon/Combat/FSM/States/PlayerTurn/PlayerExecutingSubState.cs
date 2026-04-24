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
            if (action == null)
            {
                UnityEngine.Debug.LogWarning("[PlayerExecutingSubState] PendingAction is null — skipping");
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

            if (ServiceLocator.TryGetService<TurnManager>(out var tm))
                tm.TryExecute(action, Context.ActingGuid, behaviorCtx);
            else
                action.Execute(behaviorCtx);

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
