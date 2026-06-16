---
title: Player-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, player]
---

# 11-Player — Map of Content

> Identity of the active player (class, run id, GUID).

## Relationships

```
 PlayerService : IPlayerService  (Global scope)
   ├─ PlayerGuid (minted on SetPlayer)
   ├─ CurrentHero : ClassHeroSO
   ├─ RunId
   └─ events: OnPlayerSet / OnPlayerCleared

 RunBootstrapper.StartRun → SetPlayer(hero, runId)
 RunBootstrapper.EndRun   → ClearPlayer()
```

## Notes

- [[PlayerService]] · [[IPlayerService]]

## Cross-domain edges

- [[CombatHandoffService]] reads `PlayerGuid` to seed combat
  participants.
- [[BasicEnemyAI]] targets `PlayerGuid` as the damage recipient.
- HUD views subscribe to `OnAttributeChanged` filtered on `PlayerGuid`
  to render player stats.
