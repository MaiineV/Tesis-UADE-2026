namespace Rollgeon.Exploration
{
    public interface IExplorationBehaviorService
    {
        bool IsActive { get; }
        void OnBehaviorSelected(int index);
        void CancelSelection();
    }
}
