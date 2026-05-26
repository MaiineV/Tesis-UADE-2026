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
   PlayerTurnState → sub-FSM (Idle / Selecting / Executing)
                                 │
   TurnManager — enforces action economy (repeat / ruleset / energy)
   EnergyService — Spend / Regen via [[EnergyRegenPolicy]]
   TurnOrderService — BuildForCombat via [[DefaultInitiativeProvider]]
   DamagePipeline → weakness (WeaknessChecker) → Health write
   HealPipeline  → Health write clamped to target cap
   EnemyAIRegistry → resolves AI handler per enemy → AIResult
   TreeDrivenEnemyAI → AICondition / AIDecisionNode trees
   CombatReturnService ← OnCombatEnd → Victory / Defeat / Exploration
```

## Notes

- **FSM:** [[CombatTurnFSM]] · [[CombatContext]] · [[CombatInput]] ·
  [[CombatOutcome]] · [[CombatController]] · [[CombatEnterState]] ·
  [[PlayerTurnState]] · [[EnemyTurnState]] · [[CombatExitState]]
- **PlayerTurn substates:** [[PlayerTurnSubContext]] ·
  [[PlayerTurnSubInput]] · [[PlayerIdleSubState]] ·
  [[PlayerSelectingSubState]] · [[PlayerExecutingSubState]]
- **Pipelines:** [[DamagePipeline]] · [[IDamagePipeline]] ·
  [[DamageContext]] · [[HealPipeline]] · [[IHealPipeline]] ·
  [[HealContext]] · [[AttackKind]]
- **Actions:** [[TurnManager]] · [[ActionDefinitionSO]] ·
  [[ActionCatalogSO]] · [[ActionType]]
- **Energy:** [[EnergyService]] · [[EnergyRegenPolicy]]
- **Weakness:** [[WeaknessRegistry]] · [[WeaknessChecker]] ·
  [[IWeaknessChecker]] · [[IWeaknessRegistry]]
- **Initiative:** [[DefaultInitiativeProvider]] ·
  [[InMemoryEntityRegistry]] · [[TurnOrderService]] ·
  [[IInitiativeProvider]] · [[IEntityRegistry]] · [[IInitiativeRng]]
- **First roll:** [[IFirstRollTracker]]
- **Death watch:** [[ICombatDeathWatcher]]
- **Handoff:** [[CombatHandoffService]] · [[CombatControllerAdapter]] ·
  [[DefaultEnemySpawnResolver]] · [[CombatReturnService]] ·
  [[ICombatHandoffService]] · [[ICombatReturnService]] ·
  [[ICombatStarter]] · [[IPlayerCombatActions]] ·
  [[IEnemyAIHandler]] · [[IEnemySpawnResolver]]
- **AI & blocking:** [[BasicEnemyAI]] · [[ComboBlockService]] ·
  [[IComboBlockService]] · [[EnemyAIRegistry]] · [[IEnemyAIRegistry]] ·
  [[ICombatSignaller]] · [[AIContext]] · [[AIResult]] ·
  [[TreeDrivenEnemyAI]]
- **AI Tree:** [[AICondition]] · [[AICond_AllyAlive]] ·
  [[AICond_And]] · [[AICond_HPBelow]] · [[AICond_Not]] ·
  [[AICond_Or]] · [[AICond_PlayerInRange]] · [[AICond_RoundNumber]] ·
  [[AIDecisionNode]] · [[AIQuestionNode]] · [[AIActionNode]] ·
  [[AINode_Attack]] · [[AINode_If]] · [[AINode_Move]] ·
  [[AINode_Random]] · [[AINode_Selector]] · [[AINode_Sequence]] ·
  [[AINode_Wait]]

## Cross-domain edges

- Consumes [[Attributes-MOC|Attributes]] (Health, Attack, Speed,
  Energy).
- Consumes [[Combos-MOC|Combos]] (matches + counters).
- Consumes [[Dice-MOC|Dice]] (reroll budget) on action execution.
- Consumes [[PreConditions-MOC|PreConditions]] for action gating.
- Reads selection on [[Grid-MOC|Grid]] for ranged actions /
  movement-aware effects (see [[Movement-MOC|Movement]]).
- Pushes [[CombatHUDView]] and [[VictoryScreen]] / [[DefeatScreen]]
  through [[UI-MOC|UI]].
- Plays hit / heal / spawn pulses through [[Feedback-MOC|Feedback]].
- Drops gold on enemy death through [[Economy-MOC|Economy]]
  ([[EnemyGoldDropService]]).
- Reads balance from [[RulesetSO]].
