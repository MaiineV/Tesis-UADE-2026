---
title: BaseScreen
type: system
domain: 12-UI/Core
status: done
tags: [ui, screen, base, monobehaviour]
---

# BaseScreen

> `MonoBehaviour` base for every screen prefab. Defines the lifecycle
> hooks [[ScreenManager]] invokes on push / pop.

## API

```csharp
public interface IBaseScreen {
    string ScreenId { get; }
    void OnShow(IScreenPayload payload);
    void OnHide();
}

public abstract class BaseScreen : MonoBehaviour, IBaseScreen {
    public virtual string ScreenId => GetType().Name;
    public virtual void OnShow(IScreenPayload payload) { }
    public virtual void OnHide() { }
}
```

## Contract

- `OnShow` runs once per push, after the screen is instantiated.
- `OnHide` runs before the screen is destroyed or pooled.
- Screens are **event-driven**: they subscribe to [[EventManager]] /
  [[TypedEvent]] in `OnShow` and unsubscribe in `OnHide`. Never write
  gameplay logic from inside a screen.

## Dependencies

- **Uses:** [[IScreenPayload]], [[EventManager]].
- **Used by:** every screen & HUD view (see [[MainMenuScreen]],
  [[CombatHUDView]], …).

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/BaseScreen.cs`
- Interface: `.../IBaseScreen.cs`

## External references

- TECHNICAL.md: §17.UI Screen base
