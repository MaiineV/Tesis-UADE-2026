# Registro de bugs y pulido — staging local

> **WS5.** Entradas listas para copiar al sheet compartido y a su ventana
> correspondiente. IDs siguen la serie existente del repo (última referencia
> encontrada en código: BUG-018). Formato: ID · Severidad · Área · Repro ·
> Esperado vs Observado · Branch.

## Bugs

> **Nota de numeración**: el código ya usa BUG-019 (gating de free rolls del chain,
> `CombatHandoffService.cs:924`) — la serie interna va más adelante que lo que refleja
> el código commiteado. El bug del escudo acá se numera **BUG-021**.

### BUG-021 — Escudo reusa la tabla de daño de ataque (causa raíz "escudo trivial")
- **Severidad**: Alta (balance-breaking)
- **Área**: Combat / Effects
- **Repro**: combatir con el Warrior y resolver cualquier combo alto; observar el escudo otorgado por la fase escudo del chain.
- **Esperado**: escudo acotado (spec v2: `min(base_escudo × multi, 8)`).
- **Observado**: escudo = `BaseDamage` del combo de ATAQUE × multiplier (`EffAddShield.cs:78-79`). Generala → 90 de escudo ≈ 45 turnos de inmunidad vs Melee (Attack 2).
- **Branch**: `feature/damage-formula-v2` (pre-fix). **Estado**: **FIXED 09/07** — `PlayerComboShield` (min(tabla × multi, 8)) + rewire de `EffAddShield` + tabla seedeada en CH_Warrior. 1743/1743 tests EditMode verdes, incl. regresión `ComboValue_UsesShieldTable_IgnoresAttackBaseDamage`. Sin commitear aún.

### ~~BUG-020 — Fase escudo hereda el ComboResult del ataque~~ INVALIDADO 09/07
**Falso positivo del análisis estático.** Verificado en código: el chain hace roll POR FASE
— tras la fase de daño, `PrepareNextChainPhase` → `RollViaThrow` (CNF-008: el jugador tira
de nuevo para la defensa, rerolls gateados por free rolls sobrantes del ataque) y
`ExecuteChainPhase` re-detecta el combo sobre los dados de ESA tirada
(`CombatHandoffService.cs:868-877`). La "lectura A" (tirada propia de escudo) **ya es el
diseño implementado** — la spec de Bocco se cumple sin cambios en el chain.

## Pulido / mejoras

### PUL-001 — `ED_Boss.asset` (100 HP / Atk 2) aparenta ser placeholder
Los tres bosses nominales (Sunken_Grand, Security_Boss, GeneralDirector) tienen 200 HP. Confirmar si `ED_Boss` se referencia en algún layout; si no, eliminarlo para evitar que un pool lo agarre por accidente.

### PUL-002 — `ExtraTiers` vacío en Ranged, Healer y los 3 bosses
Solo `ED_MeleeCardEnemy` define T2. El sistema de tiers está construido pero desaprovechado — es la perilla natural para la escalada de pisos 2-3 (ver `docs/planning/balance-modelo-3-pisos.md` §3.2).

### PUL-003 — HP de bosses plano entre pisos (200/200/200)
La escalada entre bosses es solo por Attack. Con upgrades de dados del jugador, el boss de piso 3 puede caer más rápido que el de piso 1. Revisar curva en sesión de balance.

### PUL-004 — Nada comunica que el escudo se gana atacando
Origen del planteo del profesor (ver `docs/design/pas-defensa-pura.md`): la regla "defensa solo tras atacar" es invisible para el jugador. Fix barato: tooltip/onboarding con la regla explícita.

### PUL-005 — `ItemSO` vs `ShopItemDef`: doble autoría de la misma identidad
- **Área**: Items / Shop
- Un item vendible se autora **dos veces**: `ItemSO` declara el efecto, `ShopItemDef` la identidad vendible, y los dos repiten `ItemId` / `DisplayName` / `Description` / `Icon`. Visible hoy en `Item_HealingPotion.asset` + `ShopItem_HealingPotion.asset` (mismo `potion.healing`). Solo `ShopItemDef` puede entrar a `ShopPool.asset`.
- **Riesgo**: editar uno y olvidar el otro deja la tienda mostrando datos viejos. El `Tools/Item Editor` (§26.13) solo edita el `ItemSO`, así que no lo detecta.
- **Resolución prevista**: `ShopItemDef` está documentado como placeholder MVP — muere cuando llegue `RewardEntrySO` (§19). **No arreglar antes**; el fix real es esa migración, no un parche de sincronización.
- **Estado**: abierto. Detectado al mapear el sistema para Feature#0032 (PR #48).

### PUL-006 — 3 `ShopItemDef` huérfanos comparten `ItemId="Item01"`
- **Área**: Items / Shop
- `D20Die.asset`, `D20DieEnchantmentPlus.asset` y `D20DieEnhancent.asset` (en `Assets/Rollgeon/Rooms/Shop/Items/`) son tres assets distintos con el **mismo** `ItemId="Item01"`, mismo `DisplayName`, mismo ícono y `Description` vacía. No son tres items: son restos de experimentación.
- **Hoy es inofensivo**: verificado por dependencias — ninguno está referenciado en `ShopPool.asset` ni en `SP_Tutorial.asset`, así que no aparecen en la tienda. Mismo riesgo que **PUL-001** — si alguien los carga a un pool por error, dos entries con el mismo id rompen la resolución por `GetById` (el `ValidateNoDuplicateIds` de `BaseCatalogSO` lo avisa solo si entran a un catálogo, y estos no están en ninguno).
- **Fix**: quedarse con uno, o borrar los tres si el D20 ya no es un item vendible.
- **Estado**: abierto. Detectado al mapear el sistema para Feature#0032 (PR #48).

### PUL-007 — `InventoryService` sin cobertura de activación ni de save/restore
- **Área**: Items / Tests
- `Rollgeon.Items.Tests` existe desde Feature#0032 (PR #48) pero cubre **solo** el bind/unbind de hooks pasivos (9 tests, ver §18.2.1). Siguen sin cobertura:
  - `ActivateItem` — la ruta de action economy (`ConsumesAction` → `ActionDefinitionSO` transitoria → `TurnManager.CanExecute`), el cooldown, y el `ConsumedOnUse` que remueve el slot **por índice**.
  - `CaptureState` / `RestoreState` (`SaveKey = "run.inventory"`) — rehidratación via `_catalog.GetById`, incluido el caso de un item cuyo asset ya no existe.
  - `ApplyPersistentModifiers` / `RemovePersistentModifiers`.
- **Por qué importa ahora**: hasta el PR #48 había **1 solo item autorado**, así que estas rutas casi no corrían. Con las tools de autoría (§26.13/§26.14) el contenido va a crecer y estas rutas pasan a ser calientes.
- **Estado**: abierto. Anotado al cerrar Feature#0032 (PR #48).

### PUL-008 — El D3 se ve idéntico al D4 (el sheet de dados no trae su fila)
- **Área**: UI / Dice / Arte
- `Assets/Art/UI/Dices/Dices.png` trae 6 filas (D4, D6, D8, D10, D12, D20). El **D3** es el 7º valor de `DiceType` — llegó con el pack de Encantamientos, después de que se pintara el sheet. Para que el catálogo valide, `DiceShapeCatalogAuthoring` le asigna la fila del **D4** (decisión explícita, ver `TypeRows`).
- **Riesgo**: un jugador con D3 y D4 en la bolsa no los distingue salvo por el número. `Validate`/`ValidateRoles` chequean **tipo** duplicado, no **sprite** duplicado, así que nada lo atrapa automáticamente.
- **Fix**: pedirle al artista la fila del D3 (5 columnas: frontal, 2 laterales, hover, selected). Al llegar, solo se re-slicea el sheet y se corre `Tools/Rollgeon/Dice/Author Shape Catalog From Sheet` — cero código.
- **Estado**: abierto. Decidido al implementar Feature#0033.

### PUL-009 — El throw 2D/3D no cicla los sprites laterales del set
- **Área**: UI / Dice
- `DiceThrowDieView.SetDiceType` (`:44-49`) toma el **frontal** del set vía el default de `GetShape` y nada más: en modo throw el dado rota de verdad en vuelo (`DiceThrow2DPresenter.cs:935`), así que el ciclado 0-1-0-2 del modo Classic no aplica tal cual.
- **Consecuencia**: el hover/selected/laterales del arte nuevo solo se ven en el HUD Classic. Si el juego shippea en modo throw, media inversión del sheet no se usa.
- **Fix**: decidir con arte/diseño si el dado volador debe cambiar de sprite según su rotación (leer el ángulo y mapearlo a frontal/lateral) o quedarse con el frontal.
- **Estado**: abierto. Alcance excluido a propósito en Feature#0033, no olvido.

### PUL-010 — El último tick del spin nunca dispara (off-by-one)
- **Área**: UI / Dice / Anim
- Con el tuning shippeado `TickCount(0.5, 0.06)` = **8**, pero `TickTime(8, 8, …)` devuelve exactamente `0.5` = la duración, y el loop de `DiceSlotAnimator.SpinRoutine` corre `while (elapsed < plan.Duration)`. El tick 8 nunca entra: se ven **7 de 8** cambios de cara.
- **Hoy es benigno**: 7 u 8 parpadeos de número random en 0.5s es imperceptible, y el ciclado de sprites de Feature#0033 está construido para aterrizar bien igual (el tick 7 es impar ⇒ frontal, y `LandFromSpin` suelta el rol pase lo que pase).
- **Cuidado si se toca**: `DiceAnimChoreographerTests.SpinRole_LastReachableTick_IsFront_WithShippedTuning` documenta y asierta este comportamiento — arreglar el off-by-one cambia cuál es el último tick y hay que revisar en qué rol aterriza.
- **Estado**: abierto. Detectado al implementar Feature#0033.

### PUL-011 — La generación conecta salas sin validar que el prefab tenga puerta en esa dirección
- **Área**: Dungeon / Generación
- `DungeonManager.GenerateFloor` cablea `RoomInstance.Connections` por pura adyacencia de grilla (N/S/E/W del vecino que exista) y elige prefab por `RoomType`, sin ningún paso que garantice que el prefab asignado tenga un `DoorController` para cada dirección conectada. El minimapa/fog lee ese grafo lógico; el paso físico lee las puertas autoradas del prefab. Si divergen, hay conexión en el mapa pero no puerta cruzable.
- **Cómo se manifestó**: `Start_Room01.prefab` era la única sala sin puerta Este (arreglado en `Fix#0034`, commit `d5c036a0`). Al estar en la celda (0,0), la topología podía poner una sala al Este del spawn → conexión visible, sala inalcanzable.
- **Hoy es benigno**: tras el fix, **las 21 salas de piso tienen las 4 puertas** `[N,S,E,O]`, así que ninguna divergencia es posible con el contenido actual. El runtime ya **detecta** el mismatch (`DungeonManager.cs` loguea "tiene Connection al {dir} pero el prefab no tiene DoorSlotRef" desde ambos lados de la reciprocidad) pero **solo avisa, no repara**.
- **Riesgo a futuro**: cualquier sala nueva autorada sin las 4 puertas reintroduce el bug en silencio (solo un warning en consola). El usuario declinó el guard de generación en `Fix#0034` a propósito (alcance).
- **Fix**: un paso de validación/reparación en generación (o un test de assets que recorra `Assets/Prefabs/Rooms/**` y falle si un `RoomLayout` no tiene los 4 `DoorSlots`). El tool `Rollgeon/Tools/Diagnose Room Doors` ya hace el diagnóstico manual.
- **Estado**: **cerrado** en `Fix#0034`. Se agregó el guard en generación (`DoorTopologyGuard` + `DungeonManager.PruneDoorlessConnections`, paso 5c de `GenerateFloor`): poda las conexiones cuya dirección no tiene puerta autorada (o cuyo vecino no tiene la recíproca) de ambos lados, con `LogError` por poda, y reporta con `LogError` cualquier sala que quede inalcanzable tras podar. Con las 21 salas actuales (4 puertas c/u) es no-op. Cubierto por `DoorTopologyGuardTests` (7 tests de lógica pura).

### PUL-012 — El preview de daño no muestra los bonos at-played
- **Área**: UI / Combat / Upgrades
- El canal nuevo de "combo jugado" (`ComboPlayService`, Feature#0035) inyecta `bono_combo` recién al confirmar el ataque, dentro de la ventana de ejecución. `DamageFormulaView` llama `PlayerComboDamage.Resolve` con la ventana cerrada, así que una pasiva/item con `EffAddComboBonus` at-played suma daño real que el preview del HUD no anticipa.
- **Hoy es benigno**: decisión explícita del diseño de Feature#0035 (el preview quedó como estaba); solo hay diferencia si diseño autorea pasivas at-played con bono.
- **Fix previsto**: dry-run de preview — dispatchar solo `EffectData` cuyos efectos sean todos `IComboScratchWriter` (el marker ya existe en `CapabilityInterfaces.cs` para esto) contra un scratch descartable y sumarlo al preview.
- **Estado**: abierto. Diferido a propósito en Feature#0035.

### PUL-013 — Contadores de combo y analytics cuentan previews, no combos jugados
- **Área**: Combos / Analytics
- `ComboCountersService` y `AnalyticsTrackerService` escuchan `ComboMatchedPayload`, que se re-emite en **cada toggle de hold** (preview), no al jugar el combo. Un jugador que holdea/desholdea infla contadores Balatro-style y métricas sin atacar nunca.
- **Fix previsto**: resuscribirlos a `ComboPlayedPayload` (Feature#0035) — una emisión por acción confirmada, pre-daño. Revisar el balance de los readers `ReadComboCounter` antes: los números van a bajar.
- **Estado**: abierto. Detectado al diseñar Feature#0035; fuera de alcance de esa sesión.

### PUL-015 — Las constantes de `ComboId` no coinciden con los ids reales de los assets
- **Área**: Combos / Heroes
- `ComboId.cs` declara `combo.par`, `combo.triple`, `combo.straight`, `combo.sum_x`, pero los `BaseComboSO` del proyecto usan `combo.pair`, `combo.trio`, `combo.ladder`, `combo.higher_number` (los assets se renombraron y las constantes no siguieron). Detectado por `ComboIdDropdownContractTests.GetKnownComboIds_InEditMode_ContainsCanonicalIds` al agregar el dropdown transversal de combo ids.
- **Hoy es benigno en runtime**: el único consumidor no-test es `ContractWarriorFactory.Build`, que nadie invoca en el juego (red de seguridad para tests/doc, y tiraría `InvalidOperationException` con el catálogo real). Los tests que usan las constantes crean sus propios combos con esos ids — autocontenidos.
- **Riesgo real**: cualquier código nuevo que compare `ComboId.Par` contra un `ComboDetectionResult.ComboId` real jamás matchea, en silencio.
- **Fix previsto**: alinear las constantes con los ids de los assets (o al revés, decisión de diseño), actualizar los tests que las usan, y agregar un audit test que cruce `ComboId` contra `BaseComboSO.GetKnownComboIds()`.
- **Estado**: abierto.

### PUL-014 — El hook RoomEntered de pasivas no filtra por sala
- **Área**: Upgrades / Combos
- El trigger legacy `AddGoldOnRoomEntered` tenía un `RoomIdFilter` (string) que ningún asset usaba; al migrarlo a `ExecuteEffectsOnEvent(RoomEntered)` (Feature#0035) ese filtro se descartó — no existe `PcRoomId` y el `RoomId` del evento no llega a las PreConditions.
- **Fix previsto**: si diseño necesita "oro solo al entrar a la tienda", agregar `PcRoomId` que lea el payload de sala vía `PreConditionContext` (el campo `Effect` ya existe; falta poblar RoomId en el contexto del bridge).
- **Estado**: abierto. Diferido a propósito en Feature#0035 (sin uso en assets).

---

**Pendiente del usuario**: link/columnas del sheet compartido y qué ventana
corresponde a cada categoría, para migrar estas entradas y las futuras.
