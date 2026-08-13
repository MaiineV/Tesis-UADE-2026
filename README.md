# Rollgeon

> Dungeon crawler por turnos basado en **dados**. Tesis UADE 2026.

Rollgeon es un dungeon crawler donde el combate y la exploración giran en torno a
una *build* de dados: cada cara habilita comportamientos (ataque, movimiento,
escudo, heal), los **combos de dados retenidos** potencian acciones y los
**encantamientos** modifican las reglas según la cara que sale. El jugador
desciende por pisos generados con salas, enemigos por tier y un jefe por piso.

- **Motor:** Unity **6000.3.11f1** (Unity 6)
- **Lenguaje:** C# · namespace raíz `Rollgeon.*`
- **Versión actual:** `v0.0.5` (ver [`CHANGELOG.md`](./CHANGELOG.md))

---

## Cómo abrir el proyecto

1. Instalar **Unity 6000.3.11f1** (vía Unity Hub; respetar la versión de
   `ProjectSettings/ProjectVersion.txt`).
2. Clonar el repo y abrir la carpeta raíz con Unity Hub.
3. Dejar que Unity reconstruya `Library/` (no está versionada) y compile.
4. Escena de entrada: ver `Assets/Scenes/`.

> Dependencias clave (ver `Packages/manifest.json`): Input System, Odin
> (Sirenix) para `SerializedScriptableObject`, y `com.coplaydev.unity-mcp`
> (servidor MCP opcional, se levanta desde **Window → MCP for Unity**).

---

## Estructura del repo

```
Assets/            # Código, arte, escenas, prefabs, SOs del juego
  Rollgeon/        #   Código de gameplay (namespace Rollgeon.*)
  Scripts/         #   Scripts varios
  Editor/          #   Tools de editor (Room Tile Painter, bake, etc.)
  Art/ Shaders/ Materials/ Sounds/ Settings/
Packages/          # manifest.json + paquetes
ProjectSettings/   # Configuración de Unity
docs/
  setup/           # Guías de wiring de engine (escenas, SOs, prefabs)
  audits/          # Auditorías doc-vs-código
production/
  session-state/   # Estado de sesión de trabajo
Rollgeon/          # Notas/diseño (MOCs, glosario, índices)
TECHNICAL.md       # Especificación técnica completa
CLAUDE.md          # Convenciones del proyecto (git, ramas, código)
```

---

## Convenciones

El detalle vive en [`CLAUDE.md`](./CLAUDE.md). Resumen:

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/)
  (`feat`, `fix`, `docs`, `refactor`, `test`, `build`, `chore`, …) en imperativo.
- **Ramas:**
  - `main` — producción / releases aprobados.
  - `develop` — integración continua (default para PRs).
  - `Fix#NNNN_PascalCase` / `Feature#NNNN_PascalCase` — bugfix/feature
    (contador secuencial compartido), ramificadas desde `develop`.
- **Pushes:** nunca pushear sin autorización explícita.
- **Código:** comentarios solo para el *why*; Odin disponible; tests EditMode
  por defecto.

---

## Documentación

- **Spec técnica:** [`TECHNICAL.md`](./TECHNICAL.md)
- **Setup de engine:** [`docs/setup/`](./docs/setup/)
- **Auditorías:** [`docs/audits/`](./docs/audits/)
- **Historial de cambios:** [`CHANGELOG.md`](./CHANGELOG.md)

---

## Contribuidores

Ignacio Martinez · Franco Delocca · Gabriel Omar Guerrero ·
Santiago Bocco · Sebastian Luser

---

_Proyecto de Tesis — Universidad Argentina de la Empresa (UADE), 2026._
