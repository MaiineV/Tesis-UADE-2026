namespace Rollgeon.Exploration
{
    public interface IExplorationController
    {
        bool IsExploring { get; }
        void BeginExploration();
        bool AdvanceRoom();
        void ResumeAfterCombat();
    }
}
