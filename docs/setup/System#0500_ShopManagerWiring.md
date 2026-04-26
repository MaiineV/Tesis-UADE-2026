# System #0500 — Shop Manager wiring

> Levantar el Shop System (§17.F) en modo MVP: 1 shop por piso obligatorio,
> ítems como props en el piso, compra vía gold service. Todo el C# ya está
> en `Assets/Scripts/Rollgeon/Shop/` y `Assets/Scripts/Rollgeon/Economy/`.

---

## §0. Archivos involucrados

Código (ya commiteado):

- `Assets/Scripts/Rollgeon/Economy/IEconomyService.cs`
- `Assets/Scripts/Rollgeon/Economy/EconomyService.cs`
- `Assets/Scripts/Rollgeon/Economy/EconomyBootstrap.cs`
- `Assets/Scripts/Rollgeon/Shop/ShopItemDef.cs`
- `Assets/Scripts/Rollgeon/Shop/WeightedShopItem.cs`
- `Assets/Scripts/Rollgeon/Shop/ShopRollResult.cs`
- `Assets/Scripts/Rollgeon/Shop/ShopSlot.cs`
- `Assets/Scripts/Rollgeon/Shop/ShopPoolSO.cs`
- `Assets/Scripts/Rollgeon/Shop/ShopConfigSO.cs`
- `Assets/Scripts/Rollgeon/Shop/IShopManagerService.cs`
- `Assets/Scripts/Rollgeon/Shop/ShopManagerService.cs`
- `Assets/Scripts/Rollgeon/Shop/ShopManagerBootstrap.cs`
- `Assets/Scripts/Rollgeon/Shop/ShopItemPedestalInteractable.cs`
- Edit en `Assets/Scripts/Rollgeon/Dungeon/DungeonManager.cs` —
  `AssignTemplates` garantiza 1 shop por piso (fallback a bossCell si el
  piso es mínimo).
- Edit en `Assets/Scripts/Rollgeon/Dungeon/FloorLayoutSO.cs` —
  `ShopRooms` marcado `[Required]` con warning si viene vacío.

Setup manual (este doc):

1. `Assets/Rollgeon/Economy/EconomyBootstrap.asset`
2. Items: N × `Assets/Rollgeon/Shop/Items/<Item>.asset` (`ShopItemDef`)
3. `Assets/Rollgeon/Shop/ShopPool.asset` (`ShopPoolSO`)
4. `Assets/Rollgeon/Shop/ShopConfig.asset` (`ShopConfigSO`)
5. `Assets/Rollgeon/Shop/ShopPedestal.prefab` — prefab del pedestal
6. `Assets/Rollgeon/Shop/ShopManagerBootstrap.asset`
7. Prefab de la shop room con `RoomLayout.RewardSpawnPoints` poblados
8. `RoomSO` para la shop room + agregarla a `FloorLayoutSO.ShopRooms`
9. Entradas en `ServiceBootstrap.ExtraServices`

---

## §1. Economy bootstrap

1. `Assets / Create / Rollgeon / Economy / Economy Bootstrap` →
   `Assets/Rollgeon/Economy/EconomyBootstrap.asset`.
2. Inspector:
   - **Starting Gold** → `10` (o lo que el diseño pida). El MVP no persiste
     el oro — cada Play arranca con este valor.
3. Agregarlo a `ServiceBootstrap.ExtraServices`. Priority 40 — antes que el
   Audio (50) y el Shop (60).

---

## §2. Shop items (`ShopItemDef`)

Cada ítem vendible es un asset `ShopItemDef`. MVP: placeholder hasta que
aterrice `RewardEntrySO` (§19) — mismo shape.

1. `Assets / Create / Rollgeon / Shop / Shop Item Def` por cada ítem.
   Ubicación sugerida: `Assets/Rollgeon/Shop/Items/`.
2. Inspector por ítem:
   - **Item Id** → string stable, ej: `potion_small`, `bomb_basic`.
     Este string se persiste en `ShopItemState.ReservedItemId` y se
     migra tal cual cuando `RewardEntrySO` aterrice.
   - **Display Name** → nombre visible al jugador.
   - **Description** → texto corto para el `ItemInspectView`.
   - **Icon** → sprite del ítem.

---

## §3. Shop pool

1. `Assets / Create / Rollgeon / Shop / Shop Pool` →
   `Assets/Rollgeon/Shop/ShopPool.asset`.
2. Inspector, **Items disponibles**: agregar entries. Por cada entry:
   - **Item** → drag del `ShopItemDef` correspondiente.
   - **Weight** → peso relativo. Entries con weight 0 no se rolean.
   - **Base Price** → precio base en oro antes del multiplicador.
   - **Min Floor Depth** → `0` por ahora (el MVP no tiene multi-floor;
     queda para cuando el depth real exista).

MVP: un solo pool global — cuando aterrice multi-floor, se crea un pool
por piso y el bootstrap resuelve cuál usar.

---

## §4. Shop config

1. `Assets / Create / Rollgeon / Shop / Shop Config` →
   `Assets/Rollgeon/Shop/ShopConfig.asset`.
2. Inspector:
   - **Price Multiplier** → `1.0` (ajuste por ruleset).
   - **Price Variance** → `0.1` (±10%).
   - **Max Item Slots** → `4`. Cantidad de pedestales por shop room. Si el
     prefab tiene más `RewardSpawnPoints`, se usan los primeros N.
   - **Allow Restock** → `false` en el MVP (el restock machine es follow-up).
   - **First Purchase Discount Percent** → `0` en el MVP (follow-up).
   - **Pedestal Prefab** → referencia al prefab del pedestal (§5).

---

## §5. Pedestal prefab

Prefab que el `ShopManagerService` instancia por cada slot no comprado.

1. Crear un GameObject con el visual del pedestal (mesh + glow).
2. Agregar `ShopItemPedestalInteractable` (`Rollgeon/Shop/Shop Item Pedestal Interactable`).
   Los campos `InteractLabel`, `RoomInstanceId`, `Slot` se rellenan al
   runtime por `Configure` — no tocar en el prefab.
3. (Opcional, para probar sin interaction service real) agregar un
   `BoxCollider` en `isTrigger = true` + un script temporal que llame
   `Interact()` al apretar la `F` mientras el player está adentro. Mismo
   patrón que `FloorExitInteractable` — un stub. Se reemplaza cuando §7.7
   aterrice.
4. Guardar como `Assets/Rollgeon/Shop/ShopPedestal.prefab` y asignarlo al
   `ShopConfig.PedestalPrefab`.

**Display dinámico del ítem en el pedestal** (nombre, ícono, precio): por
ahora el pedestal muestra un visual fijo. Cuando aterrice el
`ItemInspectView` (§D.6b) la metadata se muestra en el HUD — el pedestal
solo triggerea `OnShopItemTargetChanged` al hover. Para un feedback
mientras tanto, podés agregar un `TextMeshPro` child que lea
`ShopItemPedestalInteractable.InteractLabel` al `Configure`.

---

## §6. Shop room prefab + RoomSO

1. Abrir (o duplicar) un prefab de combat room existente.
2. Asegurar que el `RoomLayout` del prefab tenga:
   - **RewardSpawnPoints** → agregar N Transforms (ej 4) donde aparecen
     los pedestales. Suelen ir distribuidos alrededor del centro de la sala.
   - **DoorSlots** → los 4 N/S/E/W como en cualquier otra sala.
3. Guardar el prefab como `Assets/Rollgeon/Rooms/Shop_01.prefab`.
4. `Assets / Create / Rollgeon / Dungeon / Room SO` →
   `Assets/Rollgeon/Rooms/Shop_01.asset`. Inspector:
   - **Room Id** → `shop_01` (stable string).
   - **Display Name** → "Shop".
   - **Type** → `Shop`.
   - **Room Prefab** → `Shop_01.prefab` de §6.3.
   - **Grid Size** → según el prefab.

---

## §7. Engancharlo al floor layout

1. Abrir el `FloorLayoutSO` activo (ej: `Assets/Rollgeon/Floors/Floor_01.asset`).
2. **Shop Rooms** → agregar `Shop_01` (el RoomSO de §6.4). El inspector
   tiene un `[Required]` — si queda vacía, Odin loggea warning y el
   `DungeonManager` lo vuelve a loggear como error en runtime (§17.F
   exige 1 shop por piso).
3. **Room Count Min** → si está en `3`, considerá subirlo a `4`. Con 3
   cells mínimas el algoritmo reclasifica la boss cell como shop (ver log
   del `DungeonManager`), que es un fallback no ideal.

---

## §8. Shop bootstrap

1. `Assets / Create / Rollgeon / Shop / Shop Manager Bootstrap` →
   `Assets/Rollgeon/Shop/ShopManagerBootstrap.asset`.
2. Inspector:
   - **Config** → `ShopConfig.asset` de §4.
   - **Pool** → `ShopPool.asset` de §3.
3. Abrir `ServiceBootstrap.asset` y agregar ambos bootstraps a
   **Extra Services** (si no están):
   - `EconomyBootstrap` (priority 40)
   - `AudioManagerBootstrap` (priority 50)
   - `FeedbackManagerBootstrap` (priority 55)
   - `ShopManagerBootstrap` (priority 60) ← este

---

## §9. Verificación

1. Entrar a Play. Consola: log de bootstrap sin errores de
   `[EconomyBootstrap]` ni `[ShopManagerBootstrap]`. Debe aparecer un
   `OnGoldChanged` inicial con el starting gold.
2. Generar el piso. En el log del `DungeonManager` no debe haber
   `FloorLayoutSO.ShopRooms está vacío`. La shop room se ubica en una
   cell intermedia.
3. Caminar hasta la shop room. Al entrar, en la jerarquía del
   `RoomInstance.SpawnedPrefab` deben aparecer N GO
   `[ShopPedestal] <nombre>` — uno por slot no comprado.
4. Llamar `Interact()` en un pedestal (via test, trigger o stub):
   - Si `CurrentGold >= Price`: log de compra, gold baja, pedestal
     destruido, evento `OnShopItemPurchased` disparado.
   - Si `CurrentGold < Price`: log "Gold insuficiente", pedestal intacto.
5. Salir de la shop room y volver a entrar: los pedestales comprados no
   reaparecen (leen `ShopItemState.Purchased` de `RoomInstance.ObjectStates`).

---

## §10. Lo que queda pendiente (fuera de este ticket)

- **IInteractionService real (§7.7).** El pedestal hoy espera que alguien
  llame `Interact()` manualmente (mismo patrón que `FloorExitInteractable`).
  Cuando aterrice, se migra a `InteractableComponent` con
  `PhaseRules: { Exploration: Prompt }`.
- **ItemInspectView (§D.6b).** El pedestal publica `OnShopItemTargetChanged`
  al hover, pero sin suscriptor hoy la metadata no se muestra. Cuando la
  view exista, se suscribe al evento y listo — cero cambios acá.
- **Effects canónicos (§8).** `EffDeductGold`, `EffAddItemToInventory`,
  `EffConsumeProp`, `EffNotifyShopPurchase`, `EffRestockShop`. El MVP los
  inlinea en `ShopItemPedestalInteractable.Interact` y
  `ShopManagerService.NotifyItemPurchased`.
- **IInventoryService + ItemSO (§18).** Sin inventario, el ítem comprado
  se "cobra pero no se entrega". El TODO está marcado en
  `ShopItemPedestalInteractable.Interact`. Al implementar §18, agregar
  el call a `inventory.Add(slot.Item.ItemId)` + `EventManager.Trigger(OnItemObtained, …)`.
- **RewardEntrySO (§19).** `ShopItemDef` es el placeholder. Cuando §19
  aterrice, cambiar `WeightedShopItem.Item` de `ShopItemDef` a
  `RewardEntrySO`. Los `ShopItemState.ReservedItemId` ya persistidos se
  mapean 1-a-1 al `RewardId` nuevo.
- **Restock machine (§17.F.5).** `EffRestockShop` +
  `IShopManagerService.Restock` quedan no-op con log. Cuando aterrice,
  agregar un `PropEntitySO "RestockMachine"` con behavior y enchufarlo al
  prefab de la shop room opcionalmente.
- **First-purchase discount (§17.F.3).** El flag `HasHadFirstPurchase` por
  `RoomInstance` queda sin wiring — requiere agregar un `RoomObjectState`
  extra o un field al `RoomInstance` cuando valga la pena.
- **Pasiva "Comerciante" (§17.F.8).** `ShopPriceMultiplier` como atributo
  de PC + `Modifier<float>` lifetime Run. Necesita el sistema de
  atributos real primero (§1.3).
- **Multi-floor pools.** El MVP tiene un solo pool global en el
  bootstrap. Cuando el `FloorManager` / depth aterrice, mover el pool al
  `FloorLayoutSO` y resolverlo por piso en `ShopManagerService.InitializeInternal`.

---

## §11. Cross-ref

- TECHNICAL.md §17.F — spec completo del shop system.
- TECHNICAL.md §13.6 — `RoomInstance.ObjectStates` + `ShopItemState` persistente.
- TECHNICAL.md §7.7 — interaction service real, migración futura del pedestal.
- TECHNICAL.md §18 — `ItemSO` + inventario, para "entregar" el ítem comprado.
- TECHNICAL.md §19 — `RewardEntrySO`, reemplazo futuro de `ShopItemDef`.
- `docs/setup/System#0400_AudioManagerWiring.md` — hermano, mismo patrón
  de bootstrap asset + ServiceBootstrap wiring.
