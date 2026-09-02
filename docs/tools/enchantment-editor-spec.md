# Enchantment Editor — espejo del Item Editor

> Feature#0068 (2026-09). Este doc registra los **deltas** contra
> [`item-editor-spec.md`](item-editor-spec.md); todo lo que no se nombra acá (los seis bloques
> de la tool, la regla transversal de Dirty/Undo del §7, el límite de atomicidad del §7.1, el
> mandato de único punto de entrada del §6.7) aplica igual, palabra por palabra.

## 0. Qué es

La tool de autoría de `EnchantmentSO`: `EnchantmentAuthoring` + `EnchantmentQuery` + bridges
(`Assets/Scripts/Editor/Tools/Enchantment/`), la ventana `Tools → Enchantment Editor`
(extensión de la ya existente sobre `BlockEditorWindow`) y la skill `/enchantment`
(`.claude/skills/enchantment/`). Ventana y skill arman una `EnchantmentCreationSpec` y llaman
al mismo `CreateEnchantment` — no hay dos caminos que puedan divergir (§6.7).

## 1. Deltas de dominio contra items

| Eje | Items | Encantamientos |
|---|---|---|
| Economía | Rareza → precio en `ShopPool` (`ItemShopPriceBridge`) | **Sin rareza ni precio.** Costo del altar global (`EnchantmentConfigSO`); el dial por asset es `Weight` + `MinFloorDepth` en el único `EnchantmentPoolSO` (`EnchantmentPoolBridge`) |
| Agrupación | `FamilyId`/`VariantIndex` (escalones de variantes, `CreateFamily`) | **Categoría GDD** (`Caos/Recursos/Ataque/Control/Movimiento`), obligatoria ≠ `None`. No hay familias ni `CreateFamily` |
| El "cuándo" | `PassiveItemHook` + `ItemTriggerCatalog` curando un `EventName` de 100+ miembros | `ExecuteEffectsOnDiceEvent` + `EnchantmentTriggerCatalog`: el enum es chico y sano; lo curado es la **semántica** — `ComboMatched` es preview (`ScratchOnly`, BUG-017) y `RequireCarrierParticipates` gatea por participación del dado |
| Id | `banquete.real` (dot-separated, `ItemIdSlug`) | `ench.<snake_case>` (`EnchantmentIdSlug`; prefijo de canal de `UpgradeSO`) |
| Serialización | `ItemSO` con setters públicos | Todo `[OdinSerialize] protected` — la tool escribe por setters editor-only (`EditorSet*` en `UpgradeSO`/`EnchantmentSO`) |
| Comportamiento extra | Perillas de lifecycle (Feature#0065) | `FaceFilter` (uno, compone por intersección) + `Capabilities` (varias `[NotYetWired]` — la salud las marca) |

## 2. Escrituras del alta

`CreateEnchantment` valida todo por adelantado y agrupa en un solo undo step: asset
(`Ench_<Pascal>.asset`, prefijo `ench.` dropeado del nombre de archivo) → `EnchantmentCatalog`
(`BaseCatalogSO.EditorAdd`) → localización es+en (`<UpgradeId>.name/.desc` en `Content`, vía
`ContentLocalizationBridge`, extraído de `ItemLocalizationBridge` para compartir plomería) →
entry del pool. El trigger nace armado y sin efectos (mismo contrato que items).

`DeleteEnchantment`: catálogo → pool → claves de loc → asset, con el asset vivo (las entries se
localizan por referencia), NO undoable — mismas razones que `DeleteItem`.

`RenameEnchantmentId`: mueve las claves de loc, exige conservar el prefijo `ench.`, y siempre
`BreaksSaveCompatibility` (`RuntimeDiceBag` restaura slots por id y descarta desconocidos).
Quedan a cargo del caller: el diccionario de `EnchantmentCategoryAssigner` y los
`UnlockableDefinition` de meta-progresión.

## 3. Salud y auditoría

`EnchantmentQuery.CheckCatalogHealth` cubre lo de items (ids vacíos/duplicados, sin icono,
"no hace nada", combos inexistentes) más lo propio del canal: fuera del catálogo (**los saves
descartan el slot**), fuera del pool (no se ofrece nunca; peso 0 = Info), categoría `None`,
apply directo en `ComboMatched`, `PcCarrierFace` sin `RequireCarrierParticipates`, capabilities
`[NotYetWired]`.

`EnchantmentCoverageAuditTests` (junto a la tool) gatea en CI los cuatro huecos que dejaron a
los dos Codicioso huérfanos durante meses: todo asset en catálogo, en pool, ids únicos y
localización presente en ambos idiomas. `EnchantmentAssetAuditTests` (en Upgrades/Dice/Tests)
sigue auditando el contenido de cada asset.

## 4. Categorías (GDD 2026-09)

`EnchantmentCategory` quedó alineado al "Listado encantamientos" del GDD: `Caos / Recursos /
Ataque / Control / Movimiento` (append-only; `Defensa/Economia/Maldicion` legacy). La categoría
es **ortogonal** a lo maldito: `CapCursed` decide color y multiplicador de peso, el diccionario
de `EnchantmentCategoryAssigner` decide la categoría. Cada alta nueva suma su id al diccionario
(la skill lo recuerda en su Paso 6).

## 5. Fuera de alcance (pendientes anotados)

- **La regla de la máquina del GDD** — elegir 3 categorías distintas por mesa y 1 encantamiento
  de cada una (hoy `EnchantmentRoomService` rolea plano del pool). Conviene encararla ahora que
  las categorías están alineadas.
- **Encantamientos de Movimiento** — los 7 del GDD apuntan al dado de movimiento, que no existe
  como target de encantamiento en runtime.
- **Pesos de balance** — el pool sigue plano (todo Weight 1); el GDD los deja "a definir".
- El GDD "Listado encantamientos" tiene `enc_recurso_codicioso` **duplicado** entre Codicioso y
  El Caudal (copy-paste; la línea Disparador de El Caudal dice "+5" y el efecto "+3") — el
  repo ya los separó (`ench.codicioso` / `ench.el_caudal`); falta corregir el doc.
