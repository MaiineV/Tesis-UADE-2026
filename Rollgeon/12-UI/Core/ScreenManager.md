---
title: ScreenManager
type: service
domain: 12-UI/Core
status: done
tags: [ui, screen, manager]
---

# ScreenManager

> Manages the stack of UI screens (menus, HUDs, overlays). Single entry
> point for every push / pop in the game. Registered globally in
> [[Bootstrap]].

## API

```csharp
public interface IScreenManager {
    void PushByStringId(string screenId, IScreenPayload payload = null);
    void Pop();
    void Replace(string screenId, IScreenPayload payload = null);
    BaseScreen Current { get; }
    event Action<BaseScreen> OnScreenPushed;
    event Action<BaseScreen> OnScreenPopped;
}
```

## Registration model

Screens are looked up by string id (matches the prefab name /
asset tag) so call sites stay decoupled from type references.
`Push/Replace` accept an [[IScreenPayload]] subclass (e.g.
`CombatHUDPayload`) carrying screen-specific context.

## Dependencies

- **Uses:** [[BaseScreen]], [[IScreenPayload]], Unity prefab instantiation.
- **Used by:** [[CombatHandoffService]], [[CombatReturnService]],
  [[ExplorationController]], main menu flow, pause handlers.

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/ScreenManager.cs`
- Interface: `.../IScreenManager.cs`
- Host: `.../ScreenHost.cs`
- Tests: `.../Tests/ScreenManagerTests.cs`

## External references

- TECHNICAL.md: §17.UI Screen manager
