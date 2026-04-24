namespace Rollgeon.Combat.FSM.States.PlayerTurn
{
    public enum PlayerTurnSubInput
    {
        None = 0,
        ActionRequiresSelection,
        ActionDirect,
        SelectionCompleted,
        ActionExecuted,
    }
}
