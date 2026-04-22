# System#0202 — AI Decision Trees (§7.5)

> **Sprint 04 / FP closure.** Sistema de comportamiento enemigo basado en árboles
> polimórficos autorables desde el inspector de `EnemyDataSO`. Cumple TECHNICAL.md §7.5.

---

## 0. Compile check + tests

1. Abrir Unity. Esperar recompile.
2. Test Runner → EditMode → Run All. Esperar ~28 tests nuevos verdes
   (Decisions + Conditions + Registry + TreeDrivenEnemyAI).
3. BasicEnemyAITests siguen pasando — es fallback, no se eliminó.

---

## 1. SO a crear

### `EnemyAIRegistryBootstrap.asset`

- Path: `Assets/Rollgeon/Bootstrap/EnemyAIRegistryBootstrap.asset`
- Menu: `Rollgeon → Combat → AI → Enemy AI Registry Bootstrap`
- Sin campos que configurar. Priority = 77.

### Registrar en `ServiceBootstrap.asset`

En la lista **Extra Runtime Services**, agregar `EnemyAIRegistryBootstrap` con
priority 77 (entre `DiceRollerBootstrap` 72 y `GridManagerBootstrap` 75). Tras
esto, `DefaultEnemySpawnResolver` automáticamente clonará y registrará el
`AIRoot` de cada enemigo spawneado.

---

## 2. Autorar `AIRoot` en `EnemyDataSO`

`EnemyDataSO` tiene un nuevo campo `AIRoot` (abajo de `Behaviors`). El tipo es
`AIDecisionNode` polimórfico: Odin Inspector muestra un dropdown con todos los
concretes disponibles.

### 2.1 Ejemplo minimalista — Goblin (`EnemyData_Goblin.asset`)

```
AIRoot: AINode_Attack
  DamageMultiplier: 1.0
```

Un nodo hoja simple: ataca al player cada turno con Attack base del SO.

### 2.2 Ejemplo intermedio — Boss con ComboBlock cada 3 rondas

```
AIRoot: AINode_If
  Condition: AICond_RoundNumber
    Mode: Multiple
    Value: 3
  Then: AINode_Wait      # Placeholder — reemplazar por BossComboBlockBehavior trigger
  Else: AINode_Attack
    DamageMultiplier: 1.0
```

> Nota: el hookup ComboBlock dispara vía `BossComboBlockBehavior` con trigger
> `OnTurnStart`. El AI tree puede ignorar ese tick (reserva) o usar un
> `AINode_Wait` como turno de setup. Pulido fino post-FP.

### 2.3 Ejemplo extendido — Enemigo "cauteloso"

```
AIRoot: AINode_Selector
  Children:
    - AINode_If
        Condition: AICond_HPBelow
          Percent: 0.3
        Then: AINode_Wait    # HP bajo → reservarse (placeholder para heal/retirar)
        Else: null
    - AINode_If
        Condition: AICond_PlayerInRange
          Range: 1
          Metric: Manhattan
        Then: AINode_Attack
        Else: AINode_Move
          MaxSteps: 3
          StopAdjacent: true
    - AINode_Wait   # fallback
```

Flujo: si HP &lt; 30%, pasa turno. Si no, si el player está adyacente → ataca;
sino avanza hacia el player. Último recurso: espera.

---

## 3. Nodos disponibles

### Action (leaves)
| Clase | Qué hace |
|---|---|
| `AINode_Attack` | Ataca al player usando stat Attack * multiplier via `IDamagePipeline`. |
| `AINode_Move` | Mueve hacia el player hasta `MaxSteps` tiles via `IMovementService`. `StopAdjacent` evita pisar el tile del player. |
| `AINode_Wait` | No-op. Siempre Succeeded. |

### Compuestos (control flow)
| Clase | Qué hace |
|---|---|
| `AINode_Sequence` | Ejecuta hijos en orden; Failed si alguno falla. |
| `AINode_Selector` | Ejecuta hijos; Succeeded al primero que sucede. |
| `AINode_If` | Ramifica Then/Else según una `AICondition`. |
| `AINode_Random` | Escoge uno al azar según `Options[].Weight`. |

---

## 4. Condiciones disponibles

| Clase | Config | Qué evalúa |
|---|---|---|
| `AICond_HPBelow` | Percent (0..1) | Self.Health.ModifiedValue / SelfMaxHp &lt; Percent |
| `AICond_PlayerInRange` | Range, Metric (Manhattan/Chebyshev) | Distancia Self→Player ≤ Range |
| `AICond_AllyAlive` | — | Algún otro entity (no Self, no Player) con Health.ModifiedValue &gt; 0 |
| `AICond_RoundNumber` | Mode (Equal/≥/≤/Multiple), Value | `AIContext.RoundIndex` matcheando el modo |
| `AICond_And`, `AICond_Or`, `AICond_Not` | Lista / Inner | Combinadores |

---

## 5. Fallback

Si un `EnemyDataSO` tiene `AIRoot = null`, `TreeDrivenEnemyAI` cae automáticamente
a `BasicEnemyAI` (que siempre ataca al player con el stat Attack). Es compatible
hacia atrás con enemigos ya autorados antes de este sprint.

Si `IEnemyAIRegistry` NO está registrado (ej. olvidaste el bootstrap SO),
`RunController` imprime un warning y usa `BasicEnemyAI` para TODOS los enemigos.

---

## 6. Smoke test manual

1. Configurar `EnemyData_Goblin` con `AIRoot = AINode_Attack`.
2. Play desde `00_Bootstrap` → entrar a combat con un goblin → el turno enemigo
   debe disparar damage al player y log del pipeline.
3. Setear `AIRoot = null` en `EnemyData_Goblin`, volver a play → mismo comportamiento
   (fallback a BasicEnemyAI). Validación: el sistema sigue funcionando sin tree autorado.

---

## 7. Cambios de código aplicados

- **Agregado:** `Assets/Scripts/Rollgeon/Combat/AI/{AIContext, AIResult, IEnemyAIRegistry,
  EnemyAIRegistry, EnemyAIRegistryBootstrap, TreeDrivenEnemyAI}.cs`
- **Agregado:** `Assets/Scripts/Rollgeon/Combat/AI/Decisions/` — 10 archivos con jerarquía
  de nodos.
- **Agregado:** `Assets/Scripts/Rollgeon/Combat/AI/Conditions/` — 8 archivos con
  condiciones.
- **Modificado:** `EnemyDataSO.cs` agrega `AIRoot` + `CreateRuntimeAIRoot()`.
- **Modificado:** `DefaultEnemySpawnResolver.cs` acepta `IEnemyAIRegistry` opcional
  y registra el AIRoot clonado.
- **Modificado:** `RunController.cs` instancia `TreeDrivenEnemyAI` con fallback a
  `BasicEnemyAI`.
- **Modificado:** `AttributesManager.cs` expone `EnumerateEntries()` público.
- **Eliminado:** `Assets/Scripts/Rollgeon/Combat/Handoff/StubEnemyAIHandler.cs`
  (dead code — reemplazado por `BasicEnemyAI`).

---

*Generado 2026-04-21. Worktree B de `_SPRINT04_FP_CLOSURE`.*
