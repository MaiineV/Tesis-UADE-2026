# Sprint 04 — First Playable Closure

> Guía maestra para cerrar el **First Playable** después del smoke test de Sprint 03.
> Ejecuta este setup en orden. Cada worktree ya tiene código + tests en `develop`.

---

## Qué se agregó (commits recientes en `develop`)

| Worktree | Scope | Commit |
|---|---|---|
| A | Grid foundation + Movement + RoomLoader + RoomSO fields | `feat(grid)` §0201 |
| B | AI decision trees + conditions + EnemyAIRegistry + TreeDrivenEnemyAI | `feat(ai)` §0202 |
| C | EntityPawn + EntityVisualService + hero/enemy spawn wiring | `feat(entities)` §0203 |
| D | DiceRoller real (ya mergeado pre-Sprint04) | `feat(dice)` Fase 1 |

**Tests nuevos:** ~74 EditMode. Ejecutar `Window → Test Runner → EditMode → Run All`
y esperar ~325+ verdes (baseline 250 + los nuevos).

---

## Orden de setup

### Paso 0 — Compile check

1. Abrir Unity.
2. Esperar recompile completa.
3. Console sin errores.
4. Test Runner → EditMode → Run All → todo verde.

Si algo rompe en este paso → bug code, fixear antes de seguir.

### Paso 1 — Crear los bootstrap SOs nuevos

Asset menu: `Rollgeon → …` — crear cada uno en `Assets/Rollgeon/Bootstrap/`.

| Asset | Menu path | Priority |
|---|---|---|
| `GridManagerBootstrap.asset` | `Grid → Grid Manager Bootstrap` | 75 |
| `EnemyAIRegistryBootstrap.asset` | `Combat → AI → Enemy AI Registry Bootstrap` | 77 |
| `MovementServiceBootstrap.asset` | `Movement → Movement Service Bootstrap` | 78 |
| `RoomGridLoaderBootstrap.asset` | `Dungeon → Room Grid Loader Bootstrap` | 80 |
| `EntityVisualServiceBootstrap.asset` | `Entities → Visuals → Entity Visual Service Bootstrap` | 85 |

### Paso 2 — Registrar en `ServiceBootstrap.asset`

`Assets/Rollgeon/Bootstrap/ServiceBootstrap.asset` → **Extra Runtime Services**.
Agregar los 5 assets nuevos (el orden en la lista NO importa — el bootstrap ordena
por `Priority`). El orden final efectivo de registro será:

```
50  EnergyServiceBootstrap
60  TurnManagerBootstrap
70  RerollBudgetServiceBootstrap
72  DiceRollerBootstrap
75  GridManagerBootstrap         ← nuevo
77  EnemyAIRegistryBootstrap     ← nuevo
78  MovementServiceBootstrap     ← nuevo
80  RoomGridLoaderBootstrap      ← nuevo
85  EntityVisualServiceBootstrap ← nuevo
100 TurnOrderServiceBootstrap
(…)
```

### Paso 3 — Configurar `RoomSO` assets

Para cada `Room_Combat01/02/03.asset` en `Assets/Rollgeon/Dungeon/`:

- **GridLayout**: dejar vacío (IsEmpty) por ahora — el grid manager trata la sala
  como rectangular ilimitada. Para FP es suficiente.
- **PlayerSpawn**: `(0, 2)` (o lo que prefieras para que el hero aparezca a la izquierda).
- **EnemySpawnPoints**: lista con 2-3 entradas — por ejemplo:
  ```
  (5, 1)
  (5, 3)
  (7, 2)
  ```

El boss room se genera en runtime (ver `DungeonManager.GenerateFloor`). Por ahora
no tiene layout autorado — cae al path "grilla ilimitada" del manager.

### Paso 4 — Autorar `AIRoot` en los `EnemyDataSO`

Ver `System#0202_AIDecisionTrees.md` para sintaxis. Recomendación FP:

**`EnemyData_Goblin.asset`:**
```
AIRoot: AINode_Selector
  Children:
    - AINode_If
        Condition: AICond_PlayerInRange
          Range: 1
          Metric: Manhattan
        Then: AINode_Attack (DamageMultiplier=1.0)
        Else: AINode_Move (MaxSteps=3, StopAdjacent=true)
    - AINode_Wait
```

**`EnemyData_Boss.asset`:**
```
AIRoot: AINode_If
  Condition: AICond_RoundNumber (Mode=Multiple, Value=3)
  Then: AINode_Wait                                     # placeholder ComboBlock tick
  Else: AINode_If
    Condition: AICond_PlayerInRange (Range=1)
    Then: AINode_Attack (DamageMultiplier=2.0)
    Else: AINode_Move (MaxSteps=2)
```

**`EnemyData_Auditor.asset`** (support):
Dejarlo con `AIRoot = null` — va a caer a BasicEnemyAI (que skip-turn si Attack=0,
perfecto para support). El `SupportHealBehavior` sigue corriendo por trigger.

### Paso 5 — (Opcional) Prefabs de entity

Para art mejor que los primitives:

1. Crear Capsule/Cube → renombrar → agregar `Entity Pawn` component → prefab.
2. Assignarlos en `EntityVisualServiceBootstrap.asset` (`_heroPrefab`, `_enemyPrefab`, `_bossPrefab`).

Si los dejás null, el servicio genera primitives coloreados al vuelo.

---

## Smoke test end-to-end

Play desde `00_Bootstrap.unity`:

1. **Bootstrap logs:** `Registered X catalogs, Y settings, Z extra services`.
   Debería incluir los 5 nuevos. 0 warnings rojos.
2. **Main Menu** → Jugar → Class Selection (Warrior) → Confirm → Build Selection → Confirm.
3. **Gameplay scene carga.** ExplorationHUD visible.
4. **Nuevo A:** hero pawn aparece en la escena (capsule cyan si prefab default)
   posicionado en `PlayerSpawn` de la primera sala.
5. Click **Proceed** → entra a combat room.
6. **Nuevo B:** enemy pawns spawnean en `EnemySpawnPoints`.
7. Combat: roll dice → attack → enemy turn:
   - **Nuevo C:** el goblin evalúa el AIRoot. Si está adyacente al hero, ataca;
     si no, se mueve (pawn teleporta al nuevo tile).
8. Matar enemy → Proceed → boss room → similar pero con ComboBlock cada 3 rondas.
9. Victoria → Return to Menu → Main Menu limpio.

---

## Troubleshooting

| Síntoma | Causa probable | Fix |
|---|---|---|
| `[EntityVisualServiceBootstrap] IGridManager no registrado` | Priority de `EntityVisualServiceBootstrap` < 75 | Verificar que el SO hardcodea 85 (inspeccionable via Debug mode del Inspector). |
| Hero pawn no aparece | `GameplayBootstrapper` no encuentra `IEntityVisualService` | Verificar que `EntityVisualServiceBootstrap` está en `ServiceBootstrap.ExtraServices`. Log: `[GameplayBootstrapper] IEntityVisualService no registrado`. |
| Enemies se spawnean en (0,0) apilados | `RoomSO.EnemySpawnPoints` vacío | Llenar en inspector (ver Paso 3). |
| `[RunController] IEnemyAIRegistry not registered` | Registry bootstrap missing | Verificar `EnemyAIRegistryBootstrap` en la lista. |
| Enemy turn siempre ataca aunque autoré AIRoot con Move | `EnemyDataSO.AIRoot` quedó null tras editar. | Re-verificar que el campo `AIRoot` aparece populado en el asset con la clase polimórfica correcta (Odin dropdown). |
| Grid siempre walkable unbounded | `RoomSO.GridLayout` quedó vacío. | Es el default para FP. Bakear un `GridSnapshot` real es post-FP. |
| Enemies "se teleportan" — no se ven animados | Por diseño del FP. | Tweens post-FP con PrimeTween cuando se agregue al proyecto. |

---

## Qué queda post-FP

No bloquea el FP pero está diferido:

- **Feedback DBSO catalog** + auto-wiring de `OnDamageOutgoing/Incoming` a
  `FloatingDamageSpawner`. Hoy hay que triggerear `OnFloatingNumberRequested` a mano.
- **AudioService** + SFX mínimos (dado, attack, damage, footstep).
- **CameraDirector** — cam scripteada para combat (overhead/orbit/follow).
- **Despawn por kill** — hoy los pawns muertos quedan flotando hasta ClearScope(Run).
- **Tweens visuales** — PrimeTween al package manifest cuando esté disponible.
- **DamagePipeline shield + multipliers** — §12.2 placeholders siguen ahí.
- **SaveSystem** (§15) — interfaces, no servicio real.
- **Strike combos** (§5.6) — TBD.
- **Grid bake tool** — editor tool para bakear `GridSnapshot` desde prefabs.

---

## Checklist rápido

- [ ] 5 bootstrap SOs creados en `Assets/Rollgeon/Bootstrap/`
- [ ] Los 5 agregados a `ServiceBootstrap.ExtraServices`
- [ ] `RoomSO` combat rooms tienen `PlayerSpawn` + `EnemySpawnPoints`
- [ ] `EnemyData_Goblin.AIRoot` con AINode_Selector autorado
- [ ] `EnemyData_Boss.AIRoot` con AINode_If de rondas
- [ ] Play desde `00_Bootstrap` — menú → gameplay sin errores rojos
- [ ] Hero visible, enemigos visibles, combate juga end-to-end
- [ ] Volver al menu y empezar otra run funciona

Cuando los 8 ticks estén verdes, el FP está cerrado. 🎲

---

*Generado 2026-04-21. Sprint 04, plan /gds-orchestrate.*
