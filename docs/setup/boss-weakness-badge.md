# Badge de debilidad del jefe · cableado

> Estado al 2026-08-14. El código está; falta cablear cuatro refs en
> `Assets/Prefabs/UI/Canvas/Canvas_BossBar.prefab`. **Sin cablear no rompe nada**:
> los campos son opcionales y null-guardeados, la barra sigue funcionando exactamente
> como hoy y el badge simplemente no aparece.

De la tabla "Reglas invisibles" de `docs/design/bosses-seis-refinados.html`:

| Qué te hicieron | Cómo lo ves |
|---|---|
| La debilidad del jefe | Icono del combo + ×1,5 junto a su barra de vida. Destello cuando la conectás. |

El **destello** ya existía (`BossBarJuice` pinta el flash amarillo y sube el shake
cuando `DamageResolvedPayload.WeaknessHit`). Lo que faltaba es la mitad
**persistente**: la que se lee *antes* de tirar, cuando todavía se puede elegir la
mano. Eso es este badge.

## Qué cambió en código

| Archivo | Qué hace |
|---|---|
| `Assets/Scripts/Rollgeon/UI/HUD/BossBarView.cs` | Resuelve la debilidad vigente y pinta icono + multiplicador. Campos nuevos: `_weaknessRoot`, `_weaknessIcon`, `_weaknessText`, `_weaknessFormat`. |
| `Assets/Scripts/Rollgeon/UI/HUD/BossBarJuice.cs` | Campo nuevo `_weaknessBadge`: punchea el badge cuando el golpe fue a la debilidad. |

### De dónde sale el dato

```
EnemyDataSO.WeaknessComboId ──spawn──► IWeaknessRegistry ──lee──► BossBarView
EnemyDataSO.WeaknessMultiplierOverride ─┘                              │
                                                                       ▼
                                        ComboCatalogSO.GetById(id).Icon
```

La view lee el **registry**, no el `EnemyDataSO`, porque la debilidad no es fija: la
fase 2 de La Generala la reasigna en vivo (`AINode_AdoptWeakness` escribe en el
registry el combo que más venís usando). Leer el SO mostraría el dato de autoría y
no el vigente.

Por eso el badge se repinta en cada `OnTurnStarted`, no sólo al abrir la barra:
cuando La Generala cambia de debilidad en su turno, el jugador lo ve al empezar el
suyo — que es cuando el dato sirve.

**Multiplicador mostrado:** `WeaknessMultiplierOverride` si es > 0; si es 0, el
`RulesetSO.Weakness.DefaultMultiplier` (hoy 1.5). Los seis jefes tienen override
explícito de 1.5, así que ambos caminos dan lo mismo — el fallback importa para
enemigos comunes que se autoren con debilidad y sin override.

## 1. Armar el badge en el prefab

Abrir `Assets/Prefabs/UI/Canvas/Canvas_BossBar.prefab`. Todo cuelga del hijo `Root`
(el mismo que la view prende y apaga, donde ya viven `NameText`, `HpText` y
`Portrait`).

```
Root
├── LifeBorder
├── BarTrack ──► GhostFill, Fill
├── NameText
├── HpText
├── BurstLayer
├── Portrait                 ← ya existe (medallón arriba-izquierda)
└── WeaknessBadge            ← NUEVO
    ├── Icon                 (Image)
    └── Multiplier           (TextMeshProUGUI)
```

Pasos:

1. Crear `WeaknessBadge` como hijo de `Root`, **último hermano** (se dibuja encima
   del `LifeBorder`, igual que el `Portrait`).
   - `RectTransform`: anchor y pivot en `(1, 1)` — arriba-derecha, espejo del retrato.
   - `Anchored Position` `(-12, -8)`, `Size` `(112, 40)`. Números para nudgear.
   - Agregarle un `HorizontalLayoutGroup` (`Child Alignment: Middle Right`,
     `Spacing: 6`, `Child Force Expand` en off) y un `ContentSizeFitter`
     (`Horizontal Fit: Preferred`) si querés que el ancho lo ponga el contenido.
2. Hijo `Icon` con `Image`:
   - `Preserve Aspect` on, `Raycast Target` **off**.
   - `Layout Element` con `Preferred Width/Height` = 32 si usaste el layout group.
   - Dejarlo **sin sprite y con el componente `Image` deshabilitado**: la view lo
     prende sólo cuando resuelve un sprite, así el prefab nunca muestra el cuadro
     blanco del default de uGUI.
3. Hijo `Multiplier` con `TextMeshProUGUI`:
   - Mismo font asset que `HpText` (`m6x11plus SDF`).
   - `Raycast Target` **off**, `Alignment: Midline Left`, tamaño ~24.
4. Dejar `WeaknessBadge` **desactivado** en el prefab. La view lo prende sola; y si
   quedara prendido, `Awake` lo apaga igual.

## 2. Cablear las refs

En el componente **Boss Bar View** del prefab, sección `Debilidad — badge`:

| Campo | Arrastrar |
|---|---|
| `Weakness Root` | el GameObject `WeaknessBadge` |
| `Weakness Icon` | el `Image` de `Icon` |
| `Weakness Text` | el `TextMeshProUGUI` de `Multiplier` |
| `Weakness Format` | dejar `x{0:0.##}` (ver §4) |

En el componente **Boss Bar Juice** del mismo GameObject:

| Campo | Arrastrar |
|---|---|
| `Weakness Badge` | el `RectTransform` de `WeaknessBadge` |

Guardar el prefab. No hace falta tocar escenas: `Canvas_BossBar` vive bajo
`ScreenHost` y se instancia del prefab.

## 3. Iconos de combo

El icono sale de `BaseComboSO.Icon` (`_icon` en el inspector del combo), vía el
`ComboCatalogSO` registrado en el `ServiceBootstrapSO`. Los assets están en
`Assets/Rollgeon/Combos/`.

**Hoy varios están sin sprite** — es un pipeline de arte aparte. Mientras tanto el
badge no queda mudo: sin sprite esconde la `Image` y antepone el **nombre del combo**
al multiplicador (`ESCALERA x1,5` en vez de un `x1,5` pelado que no dice a qué le
pega). Autorar el sprite en el combo cambia el badge a icono + número, sin tocar
nada más.

Los combos que hoy son debilidad de algún jefe, y por lo tanto los que conviene
ilustrar primero:

| Combo | Jefe |
|---|---|
| `combo.pair` | El Croupier |
| `combo.ladder` | La Bandida |
| `combo.full_house` | El Cajero |
| `combo.generala` | El Anotador |
| `combo.generala` | La Generala |

El Tahúr no tiene debilidad (`WeaknessComboId` vacío) — su barra sale sin badge, y
eso es correcto.

## 4. El `×` y la coma decimal

El formato default es `x{0:0.##}` — **ASCII a propósito**. `m6x11plus SDF` no tiene
glifo para `×` (mismo problema que los `✕ → ▲` de `reglas-visibles.md`): si el font
asset se extiende, cambiar el campo a `×{0:0.##}` en el inspector y listo, no hay
recompilado de por medio.

El `0.##` usa la cultura activa, así que en un editor/build en español sale `x1,5`
como pide el documento y en inglés `x1.5`.

**Sin placeholders de más.** El único argumento es `{0}` (el multiplicador). Un
`{1}` mal tipeado tiraría `FormatException` en pantalla. Si el campo queda vacío, la
view cae al default y no rompe.

## 5. Cómo verificar

1. Entrar a una sala de jefe con un jefe que tenga debilidad (cualquiera menos el
   Tahúr).
2. La barra sale con el badge arriba a la derecha: icono (o nombre) del combo + `x1,5`.
3. Pegarle con **ese** combo: flash amarillo + shake reforzado (lo de siempre) **y**
   ahora el badge da un punch.
4. Pegarle con otro combo: nada de eso.
5. Contra La Generala, pasarla a fase 2 y mirar el badge al empezar tu turno
   siguiente: tiene que haber cambiado al combo que venías repitiendo.

Si el badge no aparece nunca, mirar en este orden: ¿está cableado `Weakness Root`?
¿el jefe tiene `WeaknessComboId` en su `ED_Boss_*.asset`? ¿el `ComboCatalogSO` está
en el `ServiceBootstrapSO`? Los tres son opcionales para la barra, así que ninguno
loguea error — el badge simplemente no se enciende.

## 6. Cobertura

`Assets/Scripts/Rollgeon/UI/Tests/BossBarViewWeaknessTests.cs` cubre: badge con
icono, fallback al nombre del combo sin icono, override 0 → default del ruleset,
jefe sin debilidad, `Hide`, repintado en `OnTurnStarted` tras reasignar la debilidad,
y los tres caminos de degradación (sin refs, sin registry, sin catálogo).

No cubre el layout del prefab — eso es cableado a mano y se verifica con el §5.
