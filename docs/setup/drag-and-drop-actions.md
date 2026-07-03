# Setup — Drag-and-drop de acciones de combate

> Feature: activar acciones de combate arrastrando el botón sobre el tablero
> (en lugar de click-botón + click-celda / Confirm). Espejo del patrón de Bot-Game.
> Estado: código + wiring listos; **falta playtest manual**.

## Qué se agregó

**Código nuevo** (`Assets/Scripts/Rollgeon/`):

| Archivo | Rol |
|---------|-----|
| `Grid/RenderTextureCursor.cs` | Math pura de escalado cursor→RenderTexture (extraída de `TileClickHandler`). |
| `UI/HUD/DragDrop/ActionDragPolicy.cs` | Reglas puras: `CanBeginDrag`, `IsValidDrop`, `RequiresTileDrop`. |
| `UI/HUD/DragDrop/ActionDragHandle.cs` | Hace arrastrable un `ActionButton` (espejo de `CardViewUI`). |
| `UI/HUD/DragDrop/ActionDragController.cs` | Controller central: ghost, highlight de celdas, raycast tile (espejo de `CardDragControllerUI`). |
| `UI/HUD/DragDrop/ActionPlayDispatcher.cs` | Bridge drop→combate (espejo de `CardPlaySelectionDispatcher`). |

**Tests EditMode** (verdes): `Grid/Tests/RenderTextureCursorTests.cs` (4),
`UI/Tests/ActionDragPolicyTests.cs` (10).

**Refactor mínimo:** `Grid/TileClickHandler.cs` usa `RenderTextureCursor.ScreenToRt`
en sus 2 spots de escalado (misma implementación, ahora testeada).

**Cero ediciones** a `CombatHandoffService` / `CombatHUDView` / `PlayerActionButtonsView`.

## Wiring (ya aplicado por Unity MCP)

Se agregaron **dos componentes** al GameObject `Canvas/CombatHUDView` del prefab
`Assets/Prefabs/UI/Canvas.prefab`:

- `Rollgeon.UI.HUD.DragDrop.ActionDragController`
- `Rollgeon.UI.HUD.DragDrop.ActionPlayDispatcher`

**No hace falta configurar campos.** El controller se auto-cablea en `OnEnable`:

- `_autoAttachHandles = true` → agrega un `ActionDragHandle` a cada `ActionButton`
  de la escena (Move / BaseAttack / SpecialAttack / Heal / ForceDoor).
- `_tileLayer` vacío → se resuelve solo a la capa **"Tile"** (layer 6, la misma de
  `TileClickHandler`).
- `_dragLayer` null → se crea un overlay para el ghost bajo el primer Canvas.
- `_camera` null → `Camera.main`.

Campos opcionales por si se quieren tunear en el Inspector: `_ghostAlpha` (0.85),
`_ghostScale` (1), `_highlightStyle` ("move"), `_buttonsRoot`, `_camera`, `_dragLayer`.

## Cómo funciona (resumen)

1. Arrastrás un botón (sólo si está **Available**) → el controller resalta las celdas
   válidas de esa acción (reuso de `TileHighlightService`) y crea un ghost que sigue
   el cursor.
2. Soltás:
   - **Sobre la HUD** o sobre una **celda inválida** → cancela (nada se ejecuta).
   - **Sobre una celda válida** → el dispatcher hace `ActionButton.OnClicked` (= click)
     y, si la acción abre selección de tile (Movimiento), la autocompleta con esa celda.
     Todo el pipeline de combate corre **síncrono** dentro del drop.
3. **Ataques con dados:** soltarlos sobre el tablero los **selecciona** (auto-target
   actual preservado); el sub-flujo **Roll → hold → Confirm** sigue igual (es la
   mecánica de dados, no el "doble click").

## Verificación / playtest (manual — requiere Play)

> El MCP no arranca la run; hay que apretar **Play** a mano y entrar a un combate.

1. **Movimiento (hito):** en tu turno, arrastrá **MoveButton** sobre una celda
   resaltada → el héroe se mueve ahí y se cobra energía una vez.
2. **Cancelación:** arrastrá MoveButton y soltá sobre una celda no resaltada o sobre
   la HUD → no pasa nada, el botón sigue disponible.
3. **No-regresión:** el **click** simple en un botón sigue seleccionando la acción
   (fallback); ataques (Roll→hold→Confirm), Confirm y End Turn andan igual.
4. **Ataque por drag (extra):** arrastrá BaseAttack sobre el enemigo → selecciona el
   ataque; seguí con dados normal.

## Alcance / limitaciones conocidas (v1)

- El hito validado es **Movimiento por drag**. Ataque/heal/forzar-puerta reusan la
  misma infra (drop = seleccionar), pero su flujo posterior no cambió.
- **Multi-enemigo:** los ataques auto-targetean al primer enemigo; soltar sobre el
  enemigo B igual pega al A (limitación heredada del backend; fuera de scope).
- **Ghost:** al soltar se destruye sin tween de "volver a lugar" (polish para
  follow-up; se puede sumar con PrimeTween).

## Rollback

Quitar los dos componentes de `Canvas/CombatHUDView` (o borrar la carpeta
`UI/HUD/DragDrop/` + `Grid/RenderTextureCursor.cs` y revertir los 2 spots de
`TileClickHandler.cs`). Nada del backend de combate depende de este feature.
