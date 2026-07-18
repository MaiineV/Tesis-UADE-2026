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
