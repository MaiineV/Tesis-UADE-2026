# Canvas split de 02_Gameplay — un canvas por hub

> Estado al 2026-07-15. El Canvas único de `02_Gameplay` se subdividió en
> 12 canvases (uno por hub) para poder prefabearlos por separado y que
> varias personas toquen UI sin pisarse en la escena.

## Estructura

Todos los canvases son hijos de `ScreenHost` (obligatorio: `ScreenHost`
descubre las screens con `GetComponentsInChildren<BaseScreen>`). Todos
comparten settings: Screen Space Overlay, CanvasScaler 1920×1080
match 0.5. El orden de dibujo lo da el `sortingOrder` — replica el orden
de siblings que tenía el Canvas único:

| Canvas | Contenido | Sorting |
|---|---|---|
| `Canvas_Display` | PixelArt (RawImage) — preexistente, no se tocó | 0 |
| `Canvas_ExplorationHUD` | ExplorationHUDView (GoldCounter, Minimap, RoomNavigation, ExplorationActions) | 10 |
| `Canvas_CombatHUD` | CombatHUDView (TurnQueue, ShieldBar, PlayerActionButtons, EndTurnButton, FloatingDamageOverlay, DamageFlashGroup, ChainPassIndicator, ActiveChipAnchor, PassiveBadgeView) | 20 |
| `Canvas_PlayerStatus` | HealthBarView, EnergyBarView, ActiveItemsView — **siempre activo** | 30 |
| `Canvas_FloorTransition` | FloorTransitionScreen | 40 |
| `Canvas_PauseMenu` | PauseMenuOverlay | 50 |
| `Canvas_Tooltip` | TooltipController (su popup usa overrideSorting 30000) | 60 |
| `Canvas_ActionRoll` | DiceZoneView, DamageFormulaView, ConfirmButton, RerollCountView | 70 |
| `Canvas_Victory` | VictoryScreen | 80 |
| `Canvas_Defeat` | DefeatScreen | 90 |
| `Canvas_EnchantmentAltar` | EnchantmentAltarRoot | 100 |
| `Canvas_Toast` | ToastCanvas (UnlockToastView) | 110 |
| `Canvas_DiceThrow` | DiceThrow2DPresenter, DiceThrowLayer, DiceImpactParticles, DiceThrowJuice | 120 |

## Reglas

1. **Vida/energía/items viven SOLO en `Canvas_PlayerStatus`.** Antes había
   copias duplicadas bajo ExplorationHUDView y CombatHUDView; se
   centralizaron y se borraron los duplicados. `ExplorationHUDView._healthBar`
   y `CombatHUDView._healthBar` (etc.) apuntan a las MISMAS instancias.
   No volver a crear copias por-HUD: el rebind compartido ya está resuelto
   (`ExplorationHUDView.OnGainFocus` re-bindea al volver de combate, mismo
   mecanismo que DiceZone/RerollCount/DamageFormula).
2. **Canvas nuevo = hijo de `ScreenHost`**, settings copiadas de cualquiera
   de los existentes, y `sortingOrder` en el hueco que corresponda de la
   tabla (por eso los saltos de a 10).
3. `Canvas_PlayerStatus` queda siempre visible — durante pausa/transición/
   victoria/derrota los overlays lo tapan por sorting (cambio de
   comportamiento aceptado: antes las barras se ocultaban con su screen).

## Gotchas conocidos

- **Drag-drop de acciones (hoy desactivado):** `ActionDragController`
  crea el drag layer en el canvas padre (`Canvas_CombatHUD`, sorting 20).
  Si se reactiva, el ghost quedaría debajo de `Canvas_ActionRoll` al pasar
  por encima — wirear `_dragLayer` a un canvas alto en ese momento.
- **Docs anteriores** (dice-feel-setup, warrior-passive-rework-setup,
  drag-and-drop-actions) dicen "hijo del Canvas" — el path cambió según la
  tabla de arriba; esta doc es la referencia.
- `Assets/Prefabs/UI/Canvas.prefab` sigue huérfano/divergido (nada lo
  instancia). Se resuelve cuando se prefabee canvas por canvas.
- El sprite del ícono de poción: la copia buena (PotionSprite) era la de
  exploración y es la que quedó; la copia de combate borrada usaba un ícono
  builtin del editor que no funcionaba en build.
