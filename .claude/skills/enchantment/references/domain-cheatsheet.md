# Dominio de encantamientos — cheatsheet

> **Este archivo se actualiza con el codigo.** Las listas de aca son orientativas: la fuente de
> verdad es el descubrimiento por `execute_code` del Paso 1. Si esto y el runtime discrepan,
> gana el runtime.

## Anatomia de un EnchantmentSO

| Campo | Tipo | Que decide |
|---|---|---|
| `UpgradeId` | `string` | Id estable `ench.<snake_case>`. Clave de save, de localizacion y de meta-unlock. Congelado al crear |
| `DisplayName` / `Description` | `string` | Fallback de UI — lo que ve el jugador sale de la tabla `Content` (`<id>.name` / `.desc`) |
| `Icon` | `Sprite` | Opcional hasta el pipeline de arte |
| `StatGrants` | `List<StatGrant>` | Boosts permanentes al aplicar (Health/RollRegen/Speed/Attack). Casi nunca se usa en este canal |
| `Category` | `EnchantmentCategory` | Taxonomia GDD (abajo). **`None` = auditoria roja** |
| `AllowedDiceTypes` | `List<DiceType>` | A que dados puede aplicarse. **Vacio = todos.** El altar filtra el pool con esto |
| `FaceFilter` | `IFaceFilter` (uno solo) | En que caras "existe" el encantamiento. Null = sin restriccion |
| `Triggers` | `List<IEnchantmentTrigger>` | El comportamiento. El unico concreto autorable es `ExecuteEffectsOnDiceEvent` |
| `Capabilities` | `List<IEnchantmentCapability>` | Propiedades declarativas que los services consultan. Varias `[NotYetWired]` |

Setters editor-only (los assets son Odin — nunca YAML a mano): `EditorSetUpgradeId/DisplayName/
Description/Icon` (en `UpgradeSO`), `EditorSetCategory/FaceFilter/AllowedDiceTypes`,
`EditorAddTrigger` (agrega), `EditorSetTriggers`/`EditorSetCapabilities` (**pisan la lista**).

## El catalogo de disparadores (`EnchantmentTriggerCatalog`)

| Id | Evento | Cuando | Trampa |
|---|---|---|---|
| `combo.played.any` | ComboPlayed | Se jugo cualquier combo (apply, pre-daño) | — |
| `combo.played.ids` | ComboPlayed | Se jugaron los combos elegidos | Sin combos elegidos no dispara nunca |
| `combo.matched.any` | ComboMatched | Preview de cualquier combo | **Solo scratch-writers** (BUG-017) |
| `combo.matched.ids` | ComboMatched | Preview de los combos elegidos | Idem |
| `roll.dice` | DiceRolled | Cada tirada cruda, pre-reroll | Dispara aunque despues se rerolee |
| `roll.resolved` | RollResolved | La tirada firme tras rerolls | El momento para leer la cara definitiva |
| `applied` | EnchantmentApplied | Una vez, al encantar el dado | — |
| `turn.finished` | TurnFinished | Fin del turno del jugador | — |

`Apply`/`Match`/`Describe` espejo de `ItemTriggerCatalog`. `Filter.Mode = None` en un hook de
combo equivale a `AnyCombo` (asi lo trata el runtime y asi lo matchea el catalogo).

### ComboMatched vs ComboPlayed (BUG-017)

`ComboMatched` es **preview**: re-dispara en cada toggle de hold del reroll. Un efecto de apply
directo ahi (oro, escudo, curacion) es farmeable infinito. La auditoria
(`EnchantmentAssets_ComboMatchedTriggers_OnlyContainScratchWriters`) solo admite
`IComboScratchWriter` en ese hook. Los recursos van **siempre** en `ComboPlayed`.

### RequireCarrierParticipates

"Solo dispara si ESTE dado formo parte del combo." Sin datos de contribucion NO dispara
(conservador). **Obligatorio si algun grupo usa `PcCarrierFace`** — sin el flag, el gate del
carrier se evalua sobre la tirada entera y no sobre el combo real (auditoria
`..._WithCarrierFacePrecondition_RequireCarrierParticipates`). Solo aplica a hooks de combo.

## Filtros de caras (`IFaceFilter`, en `Rollgeon.Upgrades.Dice.Filters`)

Componen por **interseccion**: cada filtro nuevo se aplica sobre las caras ya permitidas. El
apply se rechaza si la interseccion queda vacia (`EnchantmentConfigSO.MinFacesAfterApply`).

| Filtro | Campos | Caras validas |
|---|---|---|
| `ParityFilter` | `Allowed` (Even/Odd) | pares o impares (esto ES el "only evens") |
| `FaceRangeFilter` | `Min`, `Max` | rango cerrado, truncado al max real |
| `MultipleOfNFilter` | `N` (>=2) | multiplos de N |
| `OnlyPrimesFilter` / `NotPrimesFilter` | — | primos / no primos (el 1 cuenta como no primo) |
| `MinHalfMaxFilter` | — | `cara >= ceil(max/2)` — D6→{3..6} |
| `RelativeHalfFilter` | `Side` (Upper/Lower) | mitad alta/baja del dado |
| `CenterQuartersFilter` | — | 50% central — D8→{3,4,5,6} |
| `ExtremesFilter` | — | cuartos extremos — D8→{1,2,7,8} |
| `SpecificValuesFilter` | `AllowedFaces` | set explicito |

## Capabilities (`IEnchantmentCapability`, todas en `EnchantmentCapabilities.cs`)

| Capability | Campos | Estado |
|---|---|---|
| `CapForceRerollOnTurn` | `TriggerOnTurn` | ✅ cableada (`ForcedRerollCapabilityService`) |
| `CapCursed` | — | ✅ cableada — marca "maldito": color de titulo + multiplicador de peso |
| `CapPreventHolding` | — | ⚠️ `[NotYetWired]` |
| `CapWildcard` | — | ⚠️ `[NotYetWired]` |
| `CapLadderStep` | — | ⚠️ `[NotYetWired]` |
| `CapMimeticCopy` | — | ⚠️ `[NotYetWired]` |
| `CapRerollKeepHighest` | — | ⚠️ `[NotYetWired]` |
| `CapAnchorAccumulate` | `MaxAccumulation` | ⚠️ `[NotYetWired]` |

**No diseñar contenido nuevo sobre las `[NotYetWired]`** — configuran y no hacen nada in-game.
`IsCursed()` = tener una `CapCursed`; es **ortogonal a la categoria** (mitad_inferior es maldito
pero Control).

## Categorias GDD (`EnchantmentCategory`)

| Categoria | Que agrupa | Color |
|---|---|---|
| `Caos` | Efectos negativos a cambio de una ganancia | rojo #D1365A |
| `Recursos` | Generan oro/escudo al usar el dado | dorado #D9A44E |
| `Ataque` | Daño o multiplicador a partir de una condicion | naranja #E0763D |
| `Control` | Restringen caras, modifican valores, alteran combos | azul #6E7FD1 |
| `Movimiento` | Dado de movimiento (GDD; sin soporte runtime aun) | verde #63E063 |

`Defensa`/`Economia`/`Maldicion` son **legacy** (pre-GDD 2026-09) — no autorar con ellas. El
enum es APPEND-ONLY: los assets serializan el int.

## Efectos y piezas del canal scratch

Los triggers corren con contexto de scratch de combo (canal `DiceEnchantment`) — todo lo que el
encantamiento aporta al daño aparece atribuido en el breakdown. Utiles tipicos (verificar campos
en `Assets/Scripts/Rollgeon/Effects/Concretes/` y `Upgrades/Dice/Effects/`):

- **Bonos al combo** (scratch-writers, validos en preview): `EffAddComboBonus`,
  `EffMultiplyComboDamage`, `EffBlockComboDamage`.
- **Apply directo** (solo `ComboPlayed`/roll/turn): `EffModifyGold`, `EffAddShield`, `EffHeal`,
  `EffDealDamage`.
- **Del canal dados**: `EffSlotCounter { Increment/Reset }` (contador por slot),
  `EffRemoveEnchantment` (un solo uso — al final de la cadena).
- **Readers**: `ReadConstantInt`, `ReadCarrierFace` (la cara que saco el dado),
  `ReadCarrierRollDelta`, `ReadDiceFace`, `ReadComboCounter`, `ReadCurrentGold`,
  `ReadCurrentGoldSqrtScaled`.
- **Precondiciones**: `PcCarrierFace { OnMaxFace, ... }` (exige `RequireCarrierParticipates`),
  `PcSlotCounterCompare`, y las genericas (`PcChance`, `PcOwnerHpBelow`, `PcGoldCompare`).

Patron "cada 3 combos, +50": `EffSlotCounter{Increment}` + `PcSlotCounterCompare{>=3}` gateando
el bono + `EffSlotCounter{Reset}`.

## API de alta (`Rollgeon.Editor.Tools.Enchantment`)

```csharp
EnchantmentCreationResult  EnchantmentAuthoring.CreateEnchantment(EnchantmentCreationSpec spec);
bool                       EnchantmentAuthoring.IsIdAvailable(string id, out EnchantmentSO owner);
EnchantmentRenameResult    EnchantmentAuthoring.RenameEnchantmentId(EnchantmentSO e, string newId);
EnchantmentDeletionResult  EnchantmentAuthoring.DeleteEnchantment(EnchantmentSO e);
string                     EnchantmentIdSlug.FromDisplayName(string displayName); // "X" -> "ench.x"

// Solo lectura:
EnchantmentQuery.GetAll() / GetByCategory() / GetEffectTypes(e) / CheckCatalogHealth()
                / CheckLocalizationHealth(...) / GetMetrics()
EnchantmentLocalizationBridge.Read(id, locale) / Write(id, locale, name, desc)
EnchantmentPoolBridge.IsInPool / TryGetWeight / SetWeight / AddToPool / RemoveFromPool
```

- `CreateEnchantment` valida TODO antes de escribir (id, categoria ≠ None, folder, peso >= 0,
  trigger en catalogo) y agrupa las cuatro escrituras (asset + catalogo + loc es/en + pool) en
  un paso de undo. **Lo que NO hace: efectos** — eso es el Paso 5.
- Enums por nombre calificado: `Rollgeon.Upgrades.Dice.EnchantmentCategory.Recursos`,
  `Rollgeon.Dice.DiceType.D6`.

## Economia: peso, no precio

No hay rareza ni precio por encantamiento. El costo del altar es **global**
(`EnchantmentConfig.asset`: base 15, re-roll de oferta ×1.5 acumulativo, con descuentos por
items via `IEnchantmentCostModifierService`). Los diales por encantamiento son:

- **`Weight`** en el pool (default 1; 0 = deshabilitado sin borrar la entry).
- **`MinFloorDepth`** (default 0).
- Los malditos (`IsCursed()` o categoria `Caos`) escalan su peso por el multiplicador de
  `IEnchantmentWeightModifierService` (Moneda Maldita).
- El pool tambien filtra por `AllowedDiceTypes`, por exclusion de ya-aplicados y por el gate de
  meta-progresion (`MetaUnlockGate`; ids sin definicion de unlock estan disponibles).

## Limites del alta (spec §7.1, heredado de items)

| | Tras crear | Tras un Ctrl+Z |
|---|---|---|
| Archivo `.asset` | creado | ⚠️ **sigue existiendo** |
| `EnchantmentCatalog` | registrado | revertido |
| `EnchantmentPool` | con peso | revertido |
| Tabla `Content` (es+en) | 2 claves | revertido |

El escape hatch real es `DeleteEnchantment` (no undoable; limpia catalogo → pool → claves de
loc → asset, en ese orden y con el asset vivo). Un huerfano post-Ctrl+Z lo detectan
`EnchantmentQuery.CheckCatalogHealth` y los tests `EnchantmentCoverageAuditTests`.

## Ids y saves

- `RuntimeDiceBag` guarda los slots por `UpgradeId` y al restaurar **descarta en silencio** los
  ids que el catalogo no resuelve. Renombrar un id publicado = slots perdidos en saves viejos.
- Ademas del save: claves de loc (`RenameEnchantmentId` las mueve), diccionario de
  `EnchantmentCategoryAssigner` (sumar el id nuevo a mano) y `UnlockableDefinition` de
  meta-progresion si existiera.
- El nombre de archivo dropea el prefijo: `ench.multiplo_de_3` → `Ench_MultiploDe3.asset`.

## Por que nada de esto edita YAML

`EnchantmentSO` es Odin (`SerializationNodes`): el stream renumera indices de tipo por orden de
aparicion, y un edit manual desincroniza el asset **en silencio** — el sintoma aparece despues,
en otro campo, imposible de rastrear. Precedentes en el repo: `EgoistaComboBonusReauthorTool` y
`AfiladoFaceFilterFixTool` existen porque ya paso dos veces. Todo por `execute_code` +
setters editor-only + `SetDirty` + `SaveAssets`.
