# System#0203 — Entity Visual Spawn

> **Sprint 04 / FP closure.** Instancia GameObjects para hero y enemigos y los
> mantiene sincronizados con la grilla lógica. Cierra el último gap visual del FP.

---

## 0. Compile check + tests

1. Unity abre sin errores. Test Runner → EditMode → Run All.
2. Esperar ~11 tests nuevos en `Rollgeon.Entities.Visuals.Tests`.

---

## 1. SO a crear

### `EntityVisualServiceBootstrap.asset`

- Path: `Assets/Rollgeon/Bootstrap/EntityVisualServiceBootstrap.asset`
- Menu: `Rollgeon → Entities → Visuals → Entity Visual Service Bootstrap`
- Campos opcionales:
  - `_heroPrefab` — prefab del hero (ver §2).
  - `_enemyPrefab` — prefab default para enemigos (BaseHP &lt; 80).
  - `_bossPrefab` — prefab para bosses (BaseHP ≥ 80). Null = fallback al enemyPrefab.

Si los tres quedan null, el servicio genera primitives al vuelo:
- Hero = Capsule cyan
- Enemy = Capsule red
- Boss = Cube magenta

Prioridad 85 (después de grid + movement).

### Registrar en `ServiceBootstrap.asset`

En **Extra Runtime Services**, agregar `EntityVisualServiceBootstrap` (priority 85).
El orden de carga quedaría:

```
70  RerollBudgetServiceBootstrap
72  DiceRollerBootstrap
75  GridManagerBootstrap
77  EnemyAIRegistryBootstrap
78  MovementServiceBootstrap
80  RoomGridLoaderBootstrap
85  EntityVisualServiceBootstrap   ← nuevo
(…resto)
```

---

## 2. Prefabs placeholder

Para el FP basta con los primitives generados por el servicio. Si querés
prefabs custom, crealos así:

1. `GameObject → 3D Object → Capsule` → renombrar `Hero_Warrior.prefab`.
2. Agregar componente `Rollgeon/Entities/Entity Pawn` (ya lo agrega el servicio
   si falta, pero es más limpio tenerlo serializado).
3. Opcional: agregar Canvas hijo con HP bar (se wirea post-FP — hoy solo el
   pawn se mueve, no hay HP bar on-world).
4. Drag al campo `_heroPrefab` del bootstrap SO.
5. Repetir para `Enemy_Goblin.prefab` (Capsule roja), `Enemy_Auditor.prefab`
   (Capsule verde), `Enemy_Boss.prefab` (Cube magenta).

Guardar los prefabs en `Assets/Rollgeon/Prefabs/Entities/`.

---

## 3. Wiring automático

El flujo ya se encadena por sí solo:

- **Hero:** `GameplayBootstrapper.Start()` — después de `RunBootstrapper.StartRun` — llama
  `IEntityVisualService.SpawnHero(playerGuid, hero, room.PlayerSpawn)` y
  `IGridManager.Register` con esa coord.
- **Enemies:** `DefaultEnemySpawnResolver.Resolve()` — invocado por
  `CombatHandoffService` al entrar a una sala de combate — clona el AIRoot,
  elige `room.EnemySpawnPoints[i]` (con wrap-around), y llama
  `SpawnEnemy` + `IGridManager.Register`.
- **Movimiento:** `EntityVisualService` se suscribe a `IMovementService.OnEntityMoved`
  y teleporta el pawn a la nueva coord cuando un enemy mueve.

---

## 4. Despawn

- Run-end: `ServiceLocator.ClearScope(Run)` llama `Dispose()` del servicio, que
  destruye todos los pawns.
- Per-enemy defeat: TODO post-FP (no hay listener de `OnEntityDestroyed` por ahora).
  Hasta entonces, el pawn queda flotando con HP=0 hasta que termine la run.

---

## 5. Smoke test

1. Setup (una sola vez): crear bootstrap SO + ponerlo en `ServiceBootstrap.ExtraServices`.
2. Play desde `00_Bootstrap` → llegar a `02_Gameplay`.
3. Hero debe aparecer en la escena en `PlayerSpawn` de la primera sala.
4. Proceed a combat room → enemy pawns aparecen en `EnemySpawnPoints`.
5. Si un enemy tiene `AIRoot` con `AINode_Move`, al tocarle el turno el pawn
   debería reposicionarse hacia el hero (teleport, no tween por ahora).

---

*Generado 2026-04-21. Worktree C de `_SPRINT04_FP_CLOSURE`.*
