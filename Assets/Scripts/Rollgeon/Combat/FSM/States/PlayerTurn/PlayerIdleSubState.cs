using Patterns.FSM;

namespace Rollgeon.Combat.FSM.States.PlayerTurn
{
    public sealed class PlayerIdleSubState : BaseState<PlayerTurnSubContext, PlayerTurnSubInput>
    {
        internal PlayerSelectingSubState Selecting;
        internal PlayerExecutingSubState Executing;

        public PlayerIdleSubState(PlayerTurnSubContext context) : base(context) { }

        public override void Enter(PlayerTurnSubInput input)
        {
            Context.PendingAction = null;
            Context.PendingBehaviorContext = null;
            Context.SelectionResult = null;
        }

        public override bool CheckInput(PlayerTurnSubInput input,
            out BaseState<PlayerTurnSubContext, PlayerTurnSubInput> next)
        {
            switch (input)
            {
                case PlayerTurnSubInput.ActionRequiresSelection:
                    next = Selecting;
                    return true;

                case PlayerTurnSubInput.ActionDirect:
                    next = Executing;
                    return true;

                default:
                    next = null;
                    return false;
            }
        }
    }
}
