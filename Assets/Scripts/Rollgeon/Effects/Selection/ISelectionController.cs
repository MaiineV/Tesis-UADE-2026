using System;

namespace Rollgeon.Effects.Selection
{
    public interface ISelectionController
    {
        void BeginSelection(SelectionRequest request);
        void OnTargetClicked(TargetRef target);
        void CancelSelection();
        event Action<TargetSelectionResult> OnSelectionCompleted;
        bool IsSelecting { get; }
    }
}
