---
title: ComboCountersServiceBootstrap
type: so
domain: 03-Combos
status: done
tags: [combos, counters, bootstrap, so]
---

# ComboCountersServiceBootstrap

> `ScriptableObject` [[IPreloadableService]] wrapper that instantiates
> [[ComboCountersService]] and delegates `Register` to it. Dragged into
> `ServiceBootstrapSO.ExtraServices` in the inspector.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Bootstrap/Combo Counters Service")]
public sealed class ComboCountersServiceBootstrap : ScriptableObject, IPreloadableService {
    public int Priority => ComboCountersService.DefaultPriority;  // 80
    public void Register();
}
```

## Behaviour

- `Priority = 80` — after Energy (50), TurnManager (60), RerollBudget
  (70). Same pattern as `RerollBudgetServiceBootstrap` /
  `TurnManagerBootstrap`.
- Thin: only owns the lifetime of the `ComboCountersService` instance.

## Dependencies

- **Uses:** [[ComboCountersService]], [[IPreloadableService]].
- **Used by:** [[ServiceBootstrapSO]].

## Code

`Assets/Scripts/Rollgeon/Combos/Counters/ComboCountersServiceBootstrap.cs`

## External references

- TECHNICAL.md: §5.5 Combo counters bootstrap
