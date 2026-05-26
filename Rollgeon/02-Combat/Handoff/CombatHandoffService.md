---
title: CombatHandoffService
type: service
domain: 02-Combat/Handoff
status: done
tags: [combat, handoff, exploration-bridge]
---

# CombatHandoffService

> Bridges the exploration scene to combat: listens for
> `OnCombatTriggered`, resolves enemy spawns, pushes the Combat HUD,
> and kicks off the combat FSM via [[ICombatStarter]].

## Flow

```
OnCombatTriggered(roomInstanceId, roomId, RoomType)
  └─ spawnCount = (RoomType == Boss) ? 1 : 2
     ├─ IEnemySpawnResolver.Resolve(room, spawnCount, rng)
     ├─ participants = [player, enemies...]
     ├─ IScreenManager.PushByStringId("CombatHUD", CombatHUDPayload)
     └─ ICombatStarter.StartCombat(playerGuid, participants,
                                   roomInstanceId, aiHandler)
```

## Construction

`CreateAndRegister()` resolves every dep from [[ServiceLocator]]
(`IDungeonService`, [[IPlayerService]], [[IEnemySpawnResolver]],
[[IEnemyAIHandler]], [[IScreenManager]], [[ICombatStarter]]) and
registers itself in `ServiceScope.Run`.

## Dependencies

- **Uses:** [[EventManager]], [[ServiceLocator]], [[DungeonManager]]
  (via `IDungeonService`), [[IPlayerService]],
  [[DefaultEnemySpawnResolver]], [[BasicEnemyAI]] (as `IEnemyAIHandler`),
  [[ScreenManager]] (as `IScreenManager`).
- **Used by:** [[ExplorationController]] (fires
  `OnCombatTriggered`), [[RunController]] (lifecycle).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Handoff/CombatHandoffService.cs`
- Interface: `.../ICombatHandoffService.cs`
- Tests: `.../Tests/CombatHandoffServiceTests.cs`

## External references

- Setup: `docs/setup/System#0012a_CombatScreenAndHandoff.md`
- TECHNICAL.md: §12.0 Exploration → Combat handoff
