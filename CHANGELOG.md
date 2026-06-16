# Changelog — Rollgeon

Todos los cambios notables del proyecto se documentan en este archivo.

El formato se basa en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/)
y el proyecto adhiere a [Versionado Semántico](https://semver.org/lang/es/).
Los commits siguen [Conventional Commits](https://www.conventionalcommits.org/)
desde abril 2026 (ver [`CLAUDE.md`](./CLAUDE.md)).

---

## [0.0.5] — 2026-06-16

Primer release etiquetado. `main` quedó anclada a `develop`: consolida todo el
trabajo desde el `Initial commit` (10 mar 2026) hasta la integración de Sprint 04.
**505 commits**, **7 contribuidores**.

> Repo: <https://github.com/MaiineV/Tesis-UADE-2026> ·
> Tag: [`v0.0.5`](https://github.com/MaiineV/Tesis-UADE-2026/releases/tag/v0.0.5)

### Nueva funcionalidad (148 commits `feat`)

**Combate (29)** — el sistema más desarrollado del release:
- `DamagePipeline` y `HealPipeline` reales (reemplazan stubs).
- Sistema de **enchantments** (encantamientos): +30 nuevos triggers/SOs para la
  expansión del GDD, `EnchantmentPool`, `ModifyResourceTrigger` genérico,
  `IFaceFilter` y filtros por cara de dado.
- **Shield system**, chain actions y evaluación de combos por dados retenidos
  (kept-dice), prepago de energía y fórmula de daño.
- Handoff combate↔exploración: `ICombatHandoffService`, `CombatReturnService`,
  `CombatDeathWatcher`, eventos `OnChainStarted` / `OnBehaviorExecuted` /
  `OnPlayerDefeated`.
- IA de enemigos de producción: `BasicEnemyAI`, `IEnemyAIHandler`, tier model
  por enemigo y probabilidad de tier por spawner.

**Dungeon & exploración (20)** — carga de pisos, `NavGraph`, bake tool,
`SpawnPointConfig`, `ExplorationController`, transición de pisos.

**Bosses (6)** — 3 jefes (uno por piso), boss pools, boss ranged y prefabs.

**UI (17)** — pantallas de victoria/derrota, transición de piso, selección de
build, botones de acción del jugador, layout de gameplay, sprites de pociones.

**Cámara (5)** — `CameraDirector` (pan/zoom/rotate/occlusion), billboard de
iconos en floor view, yaw isométrico diagonal, zoom inicial.

**Grid (8)**, **Heroes (3)**, **Meta-progresión (3)**, **Run (4)**,
**Rendering/Shaders (5)**, **Editor tools (5+)** — Room Tile Painter, enemy
editor, room editor.

### Correcciones (69 commits `fix`)

- **Combate (13)** — carga de energía en ejecución, movimiento cancelable
  (BUG-013), combos, behaviors, Heal UI.
- **Runtime (7)** — `using` faltantes, servicios run-scoped, bootstrap guards.
- **Grid (5)**, **Dungeon (4)**, **UI (5)**, **Cámara (3)**, **Build (3)**,
  **Rooms (2)**, **Scene (2)**, selection, effects, meta, upgrades.
- **Tests (5)** — aislamiento de asmdefs, fugas de NUnit al player build (Fix#0014).

### Refactorización (13 commits `refactor`)

- Unificación del comportamiento de enemigos (Sprint 04).
- Reestructura de FSM y servicios.

### Performance (incluida en build/combat)

- Aislamiento de tests para evitar fuga de NUnit a `Rollgeon.dll` y romper el
  player build.

### Documentación (42 commits `docs`)

- `TECHNICAL.md` (spec técnica completa), `docs/setup/*` (guías de wiring de
  engine), `docs/audits/*` (auditorías doc-vs-código).

### Tests (20 commits `test`)

- Cobertura EditMode de combos, pipelines de daño/heal, prioridad de matching.

### Mantenimiento y build (33 commits `chore` + `build`)

- Modernización de asmdefs para Unity 6, imports de assets, `.gitignore`,
  pipeline de tests.

---

### Notas de versión

- **Unity:** 6000.3.11f1 (Unity 6).
- `main` ahora es idéntica en contenido a `develop` en este punto; los commits
  previos de `main` (upgrades/revert #8/#9) quedan superados.
- Trabajo pendiente conocido: wiring de escenas de Meta-progresión (#164) y
  Floor System (#158), autoría de feedback sequences (#0012), submódulos de
  `RulesetSO`. Ver `production/session-state/active.md`.

---

[0.0.5]: https://github.com/MaiineV/Tesis-UADE-2026/releases/tag/v0.0.5
