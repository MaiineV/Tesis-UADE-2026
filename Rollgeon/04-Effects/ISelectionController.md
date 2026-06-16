---
title: ISelectionController
type: interface
domain: 04-Effects
status: done
tags: [effects, selection, interface]
---

# ISelectionController

> Runtime contract for the player-facing target-selection loop. The
> dispatcher calls `BeginSelection` with a [[SelectionRequest]], the UI
> drives `OnTargetClicked` (and possibly `CancelSelection`), and the
> controller fires `OnSelectionCompleted` with a
> [[TargetSelectionResult]].

## API / Shape

```csharp
public interface ISelectionController {
    void BeginSelection(SelectionRequest request);
    void OnTargetClicked(TargetRef target);
    void CancelSelection();
    event Action<TargetSelectionResult> OnSelectionCompleted;
    bool IsSelecting { get; }
}
```

## Dependencies
**Uses:** [[SelectionRequest]], [[TargetRef]], [[TargetSelectionResult]].
**Used by:** [[BaseEffect]] dispatch, [[BossAttackBehavior]],
[[SupportHealBehavior]], any effect with `RequiresSelection`.

## Code
`Assets/Scripts/Rollgeon/Effects/Selection/ISelectionController.cs`
