---
title: HeroBehaviorSlot
type: enum
domain: 06-Heroes
status: done
tags: [heroes, behavior, enum]
---

# HeroBehaviorSlot

> Enum mapping a [[HeroActionBehavior]] to one of the four base action
> slots that every hero exposes on the combat HUD.

## Values

| Member          | Value | Notes                                      |
|-----------------|-------|--------------------------------------------|
| `Movement`      | 0     | Movement / step action.                    |
| `BaseAttack`    | 1     | Default basic attack.                      |
| `SpecialAttack` | 2     | Class-specific special.                    |
| `Healing`       | 3     | Heal action (potion, channel, etc.).       |

## Usage

Set on `HeroActionBehavior` only when `IsBaseBehavior == true`. Drives
the wiring of the four canonical hero buttons.

## Dependencies

- **Used by:** [[HeroActionBehavior]]`.Slot`,
  `HeroBehaviorSetTests`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Heroes/HeroBehaviorSlot.cs`

## External references

- TECHNICAL.md: §4.3 Hero action behaviors
