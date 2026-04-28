---
title: HeroActionBehavior
type: behavior
domain: 06-Heroes
status: done
tags: [heroes, behavior, action]
---

# HeroActionBehavior

> Authored [[BaseBehavior]] that drives a hero action button — energy
> cost, dice/reroll budget, show-conditions, and an effect pipeline.

## Overview

Declarative description of an action the player can trigger from the
combat HUD. Each behavior owns its energy cost, dice-roll requirement,
reroll rules, show-conditions (when the button appears), and an
ordered list of `EffectData` groups (preconditions + effects with
short-circuit semantics).

## Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class HeroActionBehavior : BaseBehavior {
    public string ActionName;
    public bool   IsBaseBehavior;
    public HeroBehaviorSlot Slot;       // when IsBaseBehavior

    public int  EnergyCost;
    public bool BlockOnRepeat = true;

    public bool NeedsDiceRoll = true;
    public int  FreeRollCount = 1;       // total rolls including initial
    public bool AllowsReroll = true;
    public bool AllowsEnergyReroll = true;

    public List<BasePreCondition> ShowConditions;
    public List<EffectData>       Effects;

    public bool ShouldShow(PreConditionContext);
    public bool HasEffectsWithSelection();
    public bool HasEffectsWithSelectionAt(SelectionTiming);
    public bool HasUsableEffectGroup(Guid owner, Guid opponent, out string reason);

    public override void Execute(BehaviorContext ctx);
}
```

## Behaviour

- `Execute` builds an `EffectContext` from a [[HeroBehaviorContext]]
  (dice result, matched combo, target guid, selection result) and
  iterates `Effects` groups, short-circuiting on the first failed
  group.
- `HasUsableEffectGroup` is the gate the UI uses to grey out the
  button when no group has both passing preconditions and at least one
  valid selection target.

## Dependencies

- **Uses:** [[BaseBehavior]], [[HeroBehaviorContext]],
  [[HeroBehaviorSlot]], `BasePreCondition`, `EffectData`,
  `EffectContext`, `IGridManager`, [[ServiceLocator]].
- **Used by:** hero behavior set in [[ClassHeroSO]] (via
  `HeroBehaviorSetTests`), combat action buttons.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Heroes/HeroActionBehavior.cs`
- Tests: `.../Tests/HeroBehaviorSetTests.cs`

## External references

- TECHNICAL.md: §4.3 Hero action behaviors
