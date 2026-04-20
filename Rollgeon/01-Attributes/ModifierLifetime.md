---
title: ModifierLifetime
type: concept
domain: 01-Attributes
status: done
tags: [attributes, modifier, enum]
---

# ModifierLifetime

> Decides which event [[Modifier]] auto-subscribes to in order to tick
> down or self-remove.

## Shape

```csharp
public enum ModifierLifetime {
    Turns,       // decrements Duration on TickEvent; removes at 0
    Permanent,   // never auto-removes; needs explicit EffRemoveModifier
    Run,         // removed on OnRunEnd
    Encounter,   // removed on OnCombatEnd
}
```

## Use cases

- `Turns` → "+2 Attack for 3 turns" buffs.
- `Permanent` → shop purchases, always-on passives, unlock rewards.
- `Run` → run-scoped buffs from strike combos, meta items.
- `Encounter` → "during this combat, fire combos deal +20 %".

## Dependencies

- **Used by:** [[Modifier]] (switches subscription path in `OnLoad`).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/Modifiers/ModifierLifetime.cs`

## External references

- TECHNICAL.md: §3.1 Modifier lifetime
