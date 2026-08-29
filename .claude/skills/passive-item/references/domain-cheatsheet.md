# passive-item — cheatsheet de dominio

> **Este archivo se actualiza con el codigo.** Si agregas, cambias o borras un efecto, un reader,
> una precondicion o una entrada del catalogo de disparadores, actualizalo en el mismo cambio —
> ver "Mantener esta skill al dia" en `SKILL.md`.

Contexto de apoyo para la skill. **Las listas de aca son orientativas: la fuente de verdad es el
descubrimiento por `execute_code` del Paso 1.** Si esto y el runtime discrepan, gana el runtime.

## Anatomia de un hook

`Assets/Scripts/Rollgeon/Items/PassiveItemHook.cs`

| Campo | Tipo | Que decide |
|---|---|---|
| `Kind` | `PassiveHookKind` | `EventBus` (0, default) o `ComboPlayed` (1) |
| `TriggerEvent` | `EventName` | Solo si `Kind == EventBus`. **No lo elijas a mano: usá `ItemTriggerCatalog`** |
| `Subject` | `PassiveHookSubject` | `Source` (0, default) = el jugador es `args[0]`; `Target` (1) = es `args[1]`. Es lo que separa "cuando pegás" de "cuando te pegan" |
| `ComboFilter` | `ComboFilter` | Solo si `ComboPlayed`. `Mode`: `AnyCombo` o `ComboIds` + `List<string> ComboIds` |
| `ActionKindFilter` | `RollActionKind` | Solo si `ComboPlayed`. `Unknown` = sin restriccion |
| `Effect` | `EffectData` | `Label`, `PreConditions` (AND), `Effects` (en orden), `TargetSelector` |
| `PersistentModifiers` | `List<PersistentModifierDef>` | Mientras el item este en inventario |

`PassiveHookKind` y `PassiveHookSubject` son **append-only**: se serializa el int del enum. No
reordenar.

### El catalogo de disparadores

`Assets/Scripts/Editor/Tools/Item/ItemTriggerCatalog.cs` — la lista curada de disparadores que
**si** funcionan, con nombre de disenio. Es la unica via recomendada para setear el "cuando":
`EventName` tiene mas de cien miembros y elegir uno que no sirve no da error, el item
simplemente no dispara nunca.

| Miembro | Para que |
|---|---|
| `All` | Las opciones ofrecibles (`Id`, `DisplayName`, `Help`, `UsesComboIds`) |
| `Match(hook)` | La opcion de un hook, o `null` si quedo fuera del catalogo |
| `Apply(hook, option)` | Escribe `Kind`/`TriggerEvent`/`Subject`/`ComboFilter.Mode` |
| `Describe(hook)` | El "cuando" en una frase |
| `IsPermanent(hook)` | El hook no usa evento: solo lleva modificadores persistentes |

**No estan en el catalogo, y no por olvido:** `OnCombatStart` y `OnRunStart` llevan el id de la
sala y de la run en `args[0]`, no el del jugador; `OnComboCrossed` se dispara con `Guid.Empty`; y
`OnPlayerHealthChanged` no lo emite nadie en produccion. Los cuatro se ven perfectamente
elegibles en el desplegable crudo y no disparan nunca — dos items del catalogo estan rotos hoy
exactamente por eso.

`PersistentModifierDef`: `Type TargetStat`, `ModifierOperation Operation`, `float Amount`,
`ModifierDirection Direction` (default `Intrinsic`).

### `ComboPlayed` vs `EventBus`

- **`ComboPlayed`** dispara via `TypedEvent<ComboPlayedPayload>` en la accion confirmada,
  **pre-daño**. Un `EffAddComboBonus` aca suma al golpe **en curso**.
- **`EventBus`** dispara con un `EventName` del `EventManager`. Filtra por `args[0] == Guid` del
  jugador (convencion §18); un evento que **no** arranca con un Guid **dispara siempre**, porque
  no hay a quien comparar. Para bonos de daño suele llegar **un golpe tarde** (esa fue la causa
  de BUG-080).

Eventos verificados como usables (del `InfoBox` del propio hook):
`OnTurnStarted`, `OnTurnFinished`, `OnRollStarted`, `OnDiceRolled`, `OnRollResolved`,
`OnDamageIncoming`, `OnDamageOutgoing`, `OnComboCrossed`, `OnWeaknessHit`,
`OnPlayerHealthChanged`. El enum tiene mas; el resto no esta verificado.

### `ActionKindFilter`

`RollActionKind.Attack | Heal | Movement | ... | Unknown`.

Attack, Heal y Movement **comparten el mismo play scratch**. Un bono de daño con
`ActionKindFilter = Unknown` leakea a curaciones y movimientos (BUG-060, BUG-080). Para cualquier
efecto de daño, **poné `Attack`**.

## Combos conocidos (9, verificar en runtime)

`combo.pair`, `combo.trio`, `combo.poker`, `combo.generala`, `combo.full_house`, `combo.ladder`,
`combo.double_pair`, `combo.brute_force`, `combo.higher_number`.

Fuente en vivo: `Rollgeon.Combos.BaseComboSO.GetKnownComboIds()` (lee el `ComboCatalogSO`).

## Efectos (`Assets/Scripts/Rollgeon/Effects/Concretes/`)

~19 concretos. Antes de instanciar uno, **abrí su archivo** para ver los campos reales — varios
exponen propiedades con backing field `[OdinSerialize, SerializeReference]`.

| Intencion | Efectos |
|---|---|
| Daño | `EffDealDamage`, `EffAddComboBonus`, `EffMultiplyComboDamage`, `EffBlockComboDamage`, `EffLowHpAttackBuff` |
| Vida / defensa | `EffHeal`, `EffAddShield` |
| Recursos | `EffModifyGold`, `EffModifyIntAttribute` |
| Inventario | `EffAddItemToInventory`, `EffRemoveInventoryItem` |
| Movimiento / mundo | `EffMove`, `EffApplyImpulse`, `EffForceDoor`, `EffPassDoor` |
| Composicion / presentacion | `EffChain`, `EffPlaySequence`, `EffPlayFeedback`, `EffClassSkillPush` |

### El matiz que hay que preguntar siempre

- **`EffAddComboBonus`** → suma al daño del combo, o sea **se multiplica junto con el combo**.
  Es lo que la gente quiere casi siempre cuando dice "que pegue mas fuerte".
- **`EffDealDamage`** → golpe **aparte**, plano, no escala con el combo.

No son intercambiables y el numero final difiere mucho. Preguntá cual, no elijas por el usuario.

## Readers de magnitud (`Rollgeon.Upgrades.Dice.Readers`)

Para valores dinamicos en lugar de constantes: `ReadCurrentGold`, `ReadCurrentGoldSqrtScaled`
(`Factor`), `ReadComboCounter`, `ReadCarrierFace`, `ReadCarrierRollDelta`, `ReadDiceFace`.

Ejemplo real (`Item_Egoista`): `bono = floor(sqrt(oro_actual x 5))` via
`new EffAddComboBonus { Amount = new ReadCurrentGoldSqrtScaled { Factor = 5f } }`.

## Precondiciones (`Rollgeon.PreConditions.Concretes`)

Se evaluan en **AND**. Disponibles: `PCAdjacentToDoor`, `PCComboAvailable`, `PCCurrentPhase`,
`PCEntityInRange`, `PCFirstRollOfCombat`, `PCHasIntAttribute`, `PCHasInventoryItem`,
`PCHasModifier`, `PcAllyAliveExists`, `PcAllyBelowMaxExists`, `PcBossHandCombo`, `PcChance`,
`PcGoldCompare`, `PcJackpotCountdown`, `PcNoComboThisRoll`, `PcOwnerAtRoomCenter`,
`PcOwnerHpBelow`, `PcOwnerStatCompare`, `PcRoundNumber`, `PcTargetInRange`.
Para OR o anidamiento: `PCComposite`.

## API de alta (`Rollgeon.Editor.Tools.Item.ItemAuthoring`)

```csharp
ItemCreationResult       CreateItem(ItemCreationSpec spec);
ItemFamilyCreationResult CreateFamily(ItemFamilyCreationSpec spec);
ItemRenameResult         RenameItemId(ItemSO item, string newItemId);  // rompe saves
ItemDeletionResult       DeleteItem(ItemSO item);
bool                     IsIdAvailable(string candidateId, out ItemSO owner);
Dictionary<string,ItemSO> BuildIdOwnerSnapshot();
```

`CreateItem` valida antes de escribir (si falla, no toca nada) y hace las cuatro escrituras
—asset, catalogo, precio, localizacion es+en— en **un solo `UndoGroup`**.
`ItemCreationResult`: `Success`, `Errors`, `Item`, `ItemId`, `AssetPath`.

`ItemRarity`: `Common | Uncommon | Rare | Legendary | God`.
`ItemType`: `Passive | Active`.
`ItemCreationSpec.TargetFolder` null → `Assets/Rollgeon/Items`.
`BasePrice` null → derivado de la rareza.
`TriggerId` → un `Id` de `ItemTriggerCatalog.All`; vacio = sin hooks. Uno que no existe **falla la
creacion**. Solo para `Passive`. `TriggerComboIds` solo aplica si la opcion usa combos.

**Lo que `CreateItem` NO hace: efectos.** Con `TriggerId` el item queda con el hook y su
disparador, pero sin efectos: sabe cuando dispara y todavia no hace nada.

## Familias

Una familia es la misma idea en varios escalones. Comparten `FamilyId`; `VariantIndex` las
ordena. `FamilyId` y `VariantIndex` son **solo organizativos** — no los lee ningun sistema de
runtime, viven para la tool y para el balance.

`ItemFamilyCreationSpec`: `FamilyId` (requerido), `Type`, `DefaultDescription`, `DefaultIcon`,
`TargetFolder`, `TriggerId` + `TriggerComboIds` (compartidos), `Variants`.
`ItemFamilyVariantSpec`: `DisplayName`, `Description`, `DisplayNameEn`, `DescriptionEn`, `Icon`,
`Rarity`, `BasePrice`, `VariantIndex`.

`ItemFamilyCreationResult`: `Success`, `Errors`, `Items`.

- `VariantIndex` null → la posicion en la lista. Dos variantes en el mismo indice no da error:
  deja la familia con dos items en el mismo escalon.
- La rareza es **por variante**; el precio se deriva de ella salvo `BasePrice`.
- El disparador es **de la familia**. Dos variantes que disparan distinto no son una familia.
- Consulta: `ItemQuery.GetFamilies()` → `ItemFamily { FamilyId, IReadOnlyList<ItemSO> Variants }`
  (ordenadas por `VariantIndex`); `ItemQuery.GetLooseItems()` para las sin familia.

## Textos localizados

`ItemSO.DisplayName` **no es lo que ve el jugador**: es el fallback.
`LocalizedContent.Name(itemId, so.DisplayName)` devuelve la entrada de la tabla `Content`
(`<itemId>.name` / `.desc`) si existe. `CreateItem` siembra las dos keys, asi que desde el alta
manda la tabla.

`Rollgeon.Editor.Tools.Item.ItemLocalizationBridge`: `Locales()`, `Read(itemId, locale)`,
`Write(itemId, locale, name, desc)`. Para cambiar el texto de un item ya creado, escribi ahi — no
en `DisplayName`, que no cambia nada en el juego.

## Limites medidos (spec §7.1)

| | Tras crear | Tras un Ctrl+Z |
|---|---|---|
| `.asset` | creado | **sigue existiendo** |
| `ItemCatalog` | registrado | revertido |
| `ShopPool` | con precio | revertido |
| Tabla `Content` (es+en) | 2 claves | revertido |

`Undo.RegisterCreatedObjectUndo` desregistra el objeto pero Unity **no borra el archivo**. Un
Ctrl+Z deja justo el estado que el UndoGroup queria evitar: item huerfano, sin catalogo, sin
precio y sin localizacion. `ItemQuery.CheckCatalogHealth` lo reporta como hallazgo.
Para deshacer de verdad: `ItemAuthoring.DeleteItem(item)`.

`Item_New.asset` e `Item_New 1.asset` (sin trackear en el repo) son exactamente este caso.

## Localizacion

El proyecto valida por test que **toda clave tenga valor en es y en, y que difieran**. Dejar
`DisplayNameEn`/`DescriptionEn` vacios deja la suite roja. Es motivo suficiente para pedirlos
siempre.

## Ids y saves

El `itemId` se **deriva del `DisplayName` y se congela al crear** (ver `ItemIdSlug.cs`). Es clave
de save: `RenameItemId` reporta `BreaksSaveCompatibility` justamente porque rompe partidas
guardadas. Elegí bien el nombre la primera vez.

## Por que nada de esto edita YAML

`ItemSO : SerializedScriptableObject` (Odin). El `.asset` guarda un stream de
`SerializationNodes` cuyos **indices de tipo se renumeran por orden de aparicion**. Editar el
YAML a mano desincroniza los indices sin ningun error visible: el item se carga con efectos del
tipo equivocado o vacios. `EgoistaComboBonusReauthorTool` y `AfiladoFaceFilterFixTool` existen
porque el repo ya se comio esto dos veces. Todo se arma **en memoria** y se deja re-serializar a
Odin con `SetDirty` + `SaveAssets`.
