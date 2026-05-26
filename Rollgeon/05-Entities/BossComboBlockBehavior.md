---
title: BossComboBlockBehavior
type: system
domain: 05-Entities
status: done
tags: [entities, behavior, boss, combo-block]
---

# BossComboBlockBehavior

> Boss behavior that blocks a configured combo id via
> [[ComboBlockService]] for the duration of the encounter — forcing the
> player to improvise around their strongest combo.

## Pattern

- `Trigger = OnSpawn` → `ComboBlockService.Block(ComboId)`.
- `Trigger = OnDeath` → `ComboBlockService.Unblock(ComboId)`.
- Fires [[EventName]] `OnComboCrossed` so [[ContractSheet]] updates UI
  live.

## Design note

This is one end of a paired mechanic with [[ContractSheet]]'s
`IsCrossed` check inside `MatchBest`: when the boss blocks a combo the
contract's own evaluation skips it, so the player sees greyed-out
buttons in the HUD.

## Dependencies

- **Uses:** [[BaseBehavior]], [[ComboBlockService]], [[EventManager]],
  [[EventName]], [[ContractSheet]] (indirectly via event).
- **Used by:** the floor-1 boss `EnemyDataSO`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BossComboBlockBehavior.cs`
- Tests: `Entities/Bosses/Tests/BossComboBlockBehaviorTests.cs`

## External references

- Setup: `docs/setup/Content#0103_BossFloorManager.md`
- TECHNICAL.md: §5.5 / §7.2 Boss combo block
