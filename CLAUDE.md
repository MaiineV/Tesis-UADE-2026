# CLAUDE.md — Convenciones del proyecto

> Guía corta para cualquier agente (humano o IA) que toque este repo.
> Empezó con Sprint 03 FP (abril 2026). Si agregás una convención nueva,
> sumala acá.

---

## Git — Conventional Commits

Desde abril 2026 todos los commits nuevos siguen **[Conventional Commits
v1.0.0](https://www.conventionalcommits.org/en/v1.0.0/)**.

### Formato

```
<type>(<scope>)!: <short description>

<optional body>

<optional footer>
```

- **`<type>`** obligatorio, minúscula, uno de la tabla de abajo.
- **`(<scope>)`** opcional, entre paréntesis. Usar el área del repo:
  `runtime`, `tests`, `build`, `technical`, `setup`, `combat`, `ui`, etc.
- **`!`** opcional, marca **breaking change** (también puede ir como
  `BREAKING CHANGE:` en el footer).
- **`<short description>`** imperativo, minúscula, sin punto final,
  ≤72 caracteres.
- **Body** opcional pero recomendado cuando el *why* no es obvio.
- **Footer** opcional: `Refs: #123`, `Closes: #456`, `BREAKING CHANGE: …`.

### Types permitidos

| Type       | Cuándo usarlo                                           |
|------------|---------------------------------------------------------|
| `feat`     | Nueva funcionalidad visible al usuario/API              |
| `fix`      | Bug fix                                                 |
| `docs`     | Solo documentación (README, `TECHNICAL.md`, `docs/…`)   |
| `style`    | Formato, espacios — sin cambio de lógica                |
| `refactor` | Reestructura sin cambiar comportamiento ni API          |
| `perf`     | Mejora de rendimiento                                   |
| `test`     | Agregar o corregir tests                                |
| `build`    | Asmdefs, package.json, CI config, tooling de build      |
| `chore`    | Housekeeping (gitignore, meta files, upgrades menores)  |
| `revert`   | Revert de un commit previo                              |

### Ejemplos válidos

```
feat(combat): add combo counters (Balatro-style)
fix(runtime): add missing using UnityEngine in TurnOrderConfig
build: modernize test asmdefs for Unity 6
docs(technical): defer §5.6 Strike combos as TBD
test(combos): cover priority-desc matching when sheet has duplicates
refactor(fsm)!: rename BaseState.Exit field to ExitRef

BREAKING CHANGE: external wiring must use ExitRef instead of Exit.
```

### Lo que NO va

- Commits con mensaje `WIP`, `asdf`, `fix`, `update` sueltos.
- Mezclar scopes en un commit (ej. cambios de UI + combat en uno solo).
  Partilo en dos commits.
- `--amend` sobre commits ya publicados en `origin/*`.

### Merge commits

Para merges de feature branches a `develop` o `main`, usar:

```
merge: <short summary of branch>
```

Y si hay conflicts resueltos manualmente, detallar en el body qué
archivos y cómo.

---

## Flujo de ramas

- `main` — producción / entregables de sprint aprobados.
- `develop` — integración continua, default branch para PRs.
- `Fix#NNNN_PascalCaseName` — ramas de bugfix, ej.
  `Fix#0007_HealServiceOnRunRestart`. **`NNNN` es un contador secuencial
  de ramas de fix (la siguiente a `Fix#0006` es `Fix#0007`), NO el ID del
  bug.** Un fix para BUG-016 puede vivir en `Fix#0007` si es la séptima
  rama de fix. Se ramifican desde `develop`.
- `sprint<NN>/<type>/<issue>-<name>` — ramas de feature, ej.
  `sprint03/feature/0104-energy-reroll`.
- Worktrees aislados por tarea cuando hay múltiples agents corriendo en
  paralelo (ver orquestación del Sprint 03).

---

## Pushes

**Nunca pushear** sin autorización explícita del usuario. El orquestador
debe mergear a `develop` local y avisar — el push final queda a mano.

---

## Archivos que NO se commitean

Ver `.gitignore`. Destacados:

- `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`.
- `/plan.md` — artifact de orquestación, per-worktree.
- `.vs/`, `*.csproj`, `*.sln`, `*.user`.
- Nunca commitear `.env`, credenciales, ni binarios pesados (> 5 MB).

---

## Código

- **Namespace root**: `Rollgeon.*` (ver `TECHNICAL.md §0`).
- **Odin**: disponible. Usar `SerializedScriptableObject` cuando se
  necesite polimorfismo.
- **Tests**: EditMode por defecto. `Sirenix.*` DLLs como
  `precompiledReferences` cuando el test cree `SerializedScriptableObject`.
- **Comentarios**: solo el *why*. El *what* lo dicen los nombres.
- **Unity MCP**: el paquete `com.coplaydev.unity-mcp` está disponible en
  el proyecto (ver `Packages/manifest.json`). El servidor se levanta desde
  Unity en `Window → MCP for Unity`.
  - **Antes de usarlo**, verificar que el MCP esté conectado en la sesión
    (ej. `claude mcp list`). **Si NO está conectado, avisar al usuario** y
    no asumir que las operaciones de engine se aplicaron.
  - Cuando el MCP no esté disponible, el setup de engine (scenes, SOs,
    prefabs, UI wiring) lo hace el usuario siguiendo `docs/setup/*.md` y
    los agents escriben solo C#.
