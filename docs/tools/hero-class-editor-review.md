# Hero Class Editor — review vs CardDatabase tool (Bot-Game)

> Comparación de `Tools/Hero Class Editor` (`Assets/Scripts/Editor/Tools/HeroClassEditorWindow.cs`)
> contra la CardDatabase tool de Bot-Game (`D:\GitHub\Bot-Game\Assets\Tools\CardManager\Editor\`),
> con el detalle de qué se portó en esta pasada y qué queda como backlog.

## Referencia: qué tiene la CardDatabase tool (~2.900 líneas)

| Pieza | Rol |
|---|---|
| `CardDatabaseEditorWindow` (1.537 líneas) | Master-detail: lista con búsqueda/filtros + editor de detalle |
| `CardRepository` | Cache singleton de assets con dirty-flag (`MarkDirty()` + lazy refresh) |
| `CardValidationService` + `CardValidationReportWindow` | Issues con severidad (Error/Warning/Info), Ping/Select por issue |
| `CardBulkOperationsWindow` | Multi-select (Ctrl/Shift) + operaciones batch con undo agrupado |

Sus patrones de UX destacables: búsqueda live sin submit, botones CRUD contextuales
(deshabilitados sin selección), color-coding de toolbar, secciones colapsables por
categoría, persistencia de estado de UI en EditorPrefs, validación accionable.

## Estado del Hero Class Editor

### Antes de esta pasada

3 paneles IMGUI (lista / identity+behaviors / effect pipeline) con Odin `PropertyTree`
y Undo. Sin búsqueda, sin create/duplicate/delete de assets, sin validación en UI, y
**sin el campo `BaseAttack`** (el `dmg_base_PJ` del Spec de Daño v2 no era editable
desde la tool).

### Portado en esta pasada (quick wins)

1. **Toolbar** con búsqueda live (filtra por asset name / DisplayName / EntityId),
   Refresh, Create (defaults que respetan `BaseAttack > 0`), Duplicate
   (`AssetDatabase.CopyAsset`), Delete (confirm dialog) y Validate. Botones
   deshabilitados sin selección — patrón CardDatabase.
2. **Campo `BaseAttack`** en Base Stats. La `BaseDamageTable` por clase
   (Spec Daño v2) aparece dentro de `Sheet` sin trabajo extra del editor.
3. **Validación mínima** (versión inline del CardValidationService): EntityId /
   DisplayName vacíos, `BaseAttack <= 0` (error, Spec Daño v2), `Sheet.Validate`
   (8 combos, sin duplicados, Generala al final), y sanidad de `BaseDamageTable`
   (ComboId vacío/duplicado, entradas que no están en el contrato, valores <= 0).
   Issues como HelpBox con severidad + botón Ping.

### Backlog (no portado — priorizado)

1. **Ventana de reporte de validación** sobre *todas* las clases (hoy valida solo la
   seleccionada). Vale la pena recién con 2+ clases.
2. **Repository con cache + dirty-flag** — hoy la lista se rescanea con
   `AssetDatabase.FindAssets` en cada `OnProjectChange`; con pocas clases no duele.
3. **EditorPrefs** para búsqueda/selección/foldouts entre sesiones.
4. **Bulk operations** (multi-select + batch edit) — YAGNI con una clase.
5. **Filtros estructurados** (por pasiva, por dice pool) y badges visuales en la lista
   (color por clase, indicador de validación).
6. **Preview del portrait** en el panel de detalle (la CardDatabase muestra sprite
   90×120 + warning de sprite faltante).

## Nota de arquitectura

La CardDatabase separa UI / repositorio / validación en clases distintas; el Hero
Editor mantiene todo en la ventana. Con el volumen actual (1 clase, ~500 líneas) es
correcto; si el juego escala a 4+ clases, extraer `HeroRepository` y
`HeroValidationService` siguiendo el layout de Bot-Game es el refactor natural.
