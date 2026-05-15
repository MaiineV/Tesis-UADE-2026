namespace Rollgeon.ActionRolls
{
    /// <summary>Fases del flujo de tirada de accion. La UI driveea por estas transiciones.</summary>
    public enum ActionRollPhase
    {
        Inactive = 0,
        AwaitingConfirm = 1,
        Rolling = 2,
        AwaitingRerollDecision = 3,
        Resolved = 4,
        Cancelled = 5,
    }
}
