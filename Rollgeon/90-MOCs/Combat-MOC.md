---
title: Combat-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, combat]
---

# 02-Combat — Map of Content

> Combat turn FSM, damage / heal pipelines, action economy, turn order,
> weakness resolution, and the exploration → combat handoff.

## Relationships

```
 ExplorationController ─ OnCombatTriggered → CombatHandoffService
                                         │
         DefaultEnemySpawnResolver ← samples EnemyPoolSO
                                         │
         CombatControllerAdapter → CombatController
                                         │
   CombatController → CombatTurnFSM → [Enter|Player|Enemy|Exit] states
                                 │
                      CombatContext (TurnOrder, TurnManager, Energy, PlayerId)
                                 │
   TurnManager — enforces action economy (repeat / ruleset / energy)
   EnergyService — Spend / Regen via [[EnergyRegenPolicy]]
   TurnOrderService — BuildForCombat via [[DefaultInitiativeProvider]]
   DamagePipeline → weakness (WeaknessChecker) → Health write
   HealPipeline  → Health write clamped to target cap
   BasicEnemyAI  → Attack stat → DamagePipeline
   CombatReturnService ← OnCombatEnd → Victory / Defeat / Exploration
```

## Notes

- **FSM:** [[CombatTurnFSM]] · [[CombatContext]] · [[CombatInput]] ·
  [[CombatOutcome]] · [[CombatController]] · [[CombatEnterState]] ·
  [[PlayerTurnState]] · [[EnemyTurnState]] · [[CombatExitState]]
- **Pipelines:** [[DamagePipeline]] · [[DamageContext]] ·
  [[HealPipeline]] · [[AttackKind]]
- **Actions:** [[TurnManager]] · [[ActionDefinitionSO]] ·
  [[ActionCatalogSO]] · [[ActionType]]
- **Energy:** [[EnergyService]] · [[EnergyRegenPolicy]]
- **Weakness:** [[WeaknessRegistry]] · [[WeaknessChecker]]
- **Initiative:** [[DefaultInitiativeProvider]] ·
  [[InMemoryEntityRegistry]] · [[TurnOrderService]]
- **Handoff:** [[CombatHandoffService]] · [[CombatControllerAdapter]] ·
  [[DefaultEnemySpawnResolver]] · [[CombatReturnService]]
- **AI & blocking:** [[BasicEnemyAI]] · [[ComboBlockService]]

## Cross-domain edges

- Consumes [[Attributes-MOC|Attributes]] (Health, Attack, Speed,
  Energy).
- Consumes [[Combos-MOC|Combos]] (matches + counters).
- Consumes [[Dice-MOC|Dice]] (reroll budget) on action execution.
- Pushes [[CombatHUDView]] and [[VictoryScreen]] / [[DefeatScreen]]
  through [[UI-MOC|UI]].
- Reads balance from [[RulesetSO]].
