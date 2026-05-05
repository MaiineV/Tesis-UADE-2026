---
title: IExplorationBehaviorService
type: interface
domain: 25-Exploration
status: done
tags: [exploration, behaviors, service, interface]
---

# IExplorationBehaviorService

> Contract for the service that turns exploration-phase behavior-bar input (an index click) into a hero `HeroActionBehavior` execution, including target-selection mid-flight cancellation.

## API / Shape

```csharp
public interface IExplorationBehaviorService {
    bool IsActive { get; }
    void OnBehaviorSelected(int index);
    void CancelSelection();
}
```

## Dependencies

**Used by:** [[ExplorationBehaviorService]] (impl), exploration HUD / behavior bar, [[GamePhase]]`.Exploration` input handlers.

## Code

`Assets/Scripts/Rollgeon/Exploration/IExplorationBehaviorService.cs`
