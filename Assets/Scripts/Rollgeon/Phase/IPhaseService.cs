namespace Rollgeon.Phase
{
    public interface IPhaseService
    {
        GamePhase CurrentBase { get; }
        PhaseOverlay CurrentOverlay { get; }
        void ReplacePhase(GamePhase next);
        void PushOverlay(PhaseOverlay overlay);
        void PopOverlay();
    }
}
