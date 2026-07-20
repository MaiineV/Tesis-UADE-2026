# Skin del tablero de dados por tipo de tirada

> Estado al 2026-07-17. El "tablero" donde se giran los dados en classic mode
> (el rectángulo negro semitransparente de `DiceZoneView`, dentro de
> `Canvas_ActionRoll`) ahora puede mostrar un sprite distinto según el tipo de
> tirada: **Default**, **Attack**, **Defense**. El swap se hace **sobre el
> mismo Image existente** — su posición/tamaño se siguen editando a mano.

El código ya está (`Rollgeon.UI.HUD.DiceBoardType`,
`DiceBoardSkinCatalogSO`, `DiceBoardSkinView`, campo `BoardType` en
`HeroActionBehavior` y `ActionRollSpec`). Falta el wiring de engine.

## Arte

Sprites ya sliceados en `Assets/Art/UI/Board/Board-Sheet.png` (9-slice,
4 sub-sprites: `Board-Sheet_0..3`). Elegí uno para cada tipo.

## Pasos

### 1. Crear el catálogo de skins

- Project → botón derecho → `Create → Rollgeon → UI → Dice Board Skin Catalog`.
- Nombralo `DiceBoardSkinCatalog` (sugerido:
  `Assets/Rollgeon/UI/DiceBoardSkinCatalog.asset`).
- En `Skins`, agregá 3 entradas y por cada una:
  - `Type`: `Default` / `Attack` / `Defense`.
  - `Sprite`: el sub-sprite de `Board-Sheet` correspondiente.
  - `Tint`: **blanco** (se autocorrige a blanco al agregar la entry; si querés
    tintar, cambialo después).
  - `Image Type`: `Sliced` (respeta los bordes 9-slice) — o `Simple` si el
    sprite no tiene bordes.

> Un tipo sin entrada degrada a `Default`. Sin catálogo/entrada, el tablero
> queda como está (nunca se borra el look actual).

### 2. Componente sobre el tablero (`DiceZoneView`)

- Abrí `Assets/Prefabs/UI/Canvas/Canvas_ActionRoll.prefab`.
- Seleccioná el GameObject **`DiceZoneView`** (el que tiene el `Image` negro
  del tablero, size 600×200 bottom-center).
- `Add Component → Rollgeon/UI/HUD/Dice Board Skin View`.
  - `_boardImage`: arrastrá el **mismo `Image`** del rectángulo negro (está en
    ese GameObject).
  - `_catalog`: arrastrá el `DiceBoardSkinCatalog` del paso 1.

En reposo el view aplica `Default`, así que el rectángulo negro pasa a mostrar
el sprite Default apenas entrás a la escena.

### 3. Driver de combate (`CombatHUDView`)

En el prefab del Combat HUD (o donde viva `CombatHUDView`):

- Campo nuevo `Board Skin` (`_boardSkin`): arrastrá el `DiceBoardSkinView` del
  paso 2. Es una referencia cross-canvas (Canvas_CombatHUD → Canvas_ActionRoll),
  igual que `_diceZone` / `_damageFormula` — ver `gameplay-canvas-split.md`.

Si queda null, el combate no swappea el skin (los action rolls de exploración
igual funcionan, porque el view se subscribe solo a `IActionRollService`).

### 4. Declarar el tipo en los behaviors

- En cada `HeroActionBehavior` de **ataque** (BaseAttack, SpecialAttack, etc.):
  sección `Dice → Board Type = Attack`.
- En los de **defensa/shield**: `Board Type = Defense`.
- El resto queda en `Default`.

Heal (poción) y Forzar Puerta ya piden `Default` desde código
(`EffHeal.TryGetRollSpec` / `EffForceDoor.TryGetRollSpec`); si querés un skin
propio para esas acciones, cambiá el `BoardType` del spec ahí.

## Verificación

Play → combate:
- Tirada de ataque → sprite Attack en el tablero.
- Behavior de defensa → sprite Defense.
- Heal con poción / Forzar Puerta → sprite Default.
- Cambiá un sprite en `DiceBoardSkinCatalog` y re-entrá: el tablero refleja el
  cambio sin tocar código.

---

## Logo por tipo + juice de transición (2026-07-20)

El `DiceBoardLogo` (hijo de `DiceZoneView`, 40×40 sobre el tablero) ahora
también swappea por tipo, y el cambio de tipo dispara feedback
(`DiceBoardSkinJuice`: flash de tint + fade/punch del logo con PrimeTween,
más `MMF_Player`s autorables con Feel).

**Wiring ya aplicado vía MCP** (no hace falta repetirlo):

- Catalog: `LogoSprite`/`LogoTint` por entry — Attack = `Board-Sheet2_3`
  (espada), Defense = `Board-Sheet2_2` (escudo), Default sin logo (el logo se
  **esconde** en tipos sin sprite — a diferencia del board, que degrada al
  look actual).
- Prefab: `_logoImage` del `DiceBoardSkinView` → Image de `DiceBoardLogo`;
  `DiceBoardSkinJuice` agregado al mismo GameObject con `_boardImage` y
  `_logoImage` wireados. El logo quedó con `Raycast Target` off (decorativo).

**Autorado aplicado vía MCP (2026-07-20)** — hijos de
`DiceZoneView/BoardSkinJuice/`, clonados de los springs ya tuneados del
`ZoneJuice` y wireados al `DiceBoardSkinJuice`:

- `Juice_BoardTransition` — `MMF_PositionSpring` "Board Swap Thump" sobre
  `RollArea` (mismo spring del land thump a ~1/3 de bump: -40/-60).
- `Juice_ToAttack` — `MMF_RotationSpring` "Attack Logo Wobble" sobre el logo
  (bump z 18–28°, agresivo).
- `Juice_ToDefense` — `MMF_RotationSpring` "Defense Logo Settle" sobre el logo
  (bump z -8/-12°, suave y en dirección opuesta — el escudo "asienta").
- `_transitionClip` = `sfx_dice_throw_whoosh` (**placeholder** — reemplazar
  cuando haya un swish propio). **Nunca `MMF_Sound`** (TECHNICAL.md §17).

Los springs de rotación no pelean con el juice procedural: PrimeTween anima
scale (punch) y color (fade/flash); los players, posición y rotación.
Tuning fino: editar los feedbacks en el inspector de cada `Juice_*`.

### Iteración post-playtest (2026-07-20)

- **Idle del logo por tipo** (`DiceBoardSkinJuice`, sección "Idle del logo"):
  Attack = `Pulse` (latido de escala ±8%, 0.9 s), Defense = `Bob` (flote ±3 px,
  2.2 s). Estilos disponibles: `None`/`Pulse`/`Bob`/`Sway`; cada uno usa un canal
  distinto (scale/posición/rotación) para no pelear con la transición.
- **El board ya NO vuelve a Default al terminar una acción**: se queda en su
  tipo actual hasta que otra acción empuje otro (cambios en
  `CombatHUDView.Set/ClearBehaviorForFormula` y
  `DiceBoardSkinView.RefreshFromActionRoll`/`OnEnable` — el View re-aplica su
  `CurrentType` al re-habilitarse).
- **Confirm / Reroll con estética del menú**: fondo invisible + texto m6x11plus
  con outline + underline animado + `JuicyMenuButton` (mismos settings del main
  menu). Doble línea: label principal (top-center, 22 pt) + shortcut del
  `HotkeyLabel` abajo (13 pt, gris #5F737A). El `UiButtonJuice` queda solo para
  el click SFX; su pulso "available" ahora targetea el label (el
  `JuicyMenuButton` escribe el scale del root cada frame) y `Juice_Press` se
  eliminó (el punch lo hace `JuicyMenuButton`).

**Comportamiento:** el feedback dispara SOLO en cambios reales de tipo — ni en
el `Default` inicial de `OnEnable`, ni cuando `OnPhaseChanged` re-aplica el
mismo tipo por fase. Sin wiring del Juice todo degrada a no-op.
