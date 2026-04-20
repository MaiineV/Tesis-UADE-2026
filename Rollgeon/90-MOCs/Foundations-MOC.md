---
title: Foundations-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, foundation]
---

# 00-Foundations — Map of Content

> The skeleton everything else plugs into: service registration, the
> event bus, FSM primitives, the bootstrap pipeline, and the save
> contract.

## Relationships

```
 Bootstrap ─ loads → ServiceLocator (Global | Run scoped)
    │              └ registers → catalogs, settings, services
    │
    ├─ EventManager (untyped, string-keyed via EventName)
    │    └ complemented by TypedEvent<T> (struct payloads)
    │
    ├─ FSM framework: IState ← BaseState ← StateMachine<TC,TI>
    │
    └─ ISaveable (stub → future SaveSystem, see [[SaveSystem]])
```

## Notes

- **Services & events:** [[ServiceLocator]] · [[ServiceScope]] ·
  [[EventManager]] · [[EventName]] · [[TypedEvent]]
- **FSM:** [[IState]] · [[BaseState]] · [[StateMachine]]
- **Bootstrap:** [[Bootstrap]] · [[ServiceBootstrapSO]] ·
  [[BootstrapRunner]] · [[IPreloadableService]]
- **Save (stub):** [[ISaveable]]

## Cross-domain edges

- [[Bootstrap]] registers every catalog / settings / service in every
  other MOC.
- [[EventManager]] + [[TypedEvent]] are consumed by virtually every
  domain.
- [[StateMachine]] is the base of [[CombatTurnFSM]] (see
  [[Combat-MOC]]).
