using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Items;
using UnityEngine;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Implementación MVP de <see cref="IShopManagerService"/>. Lazy-init por
    /// room vía <c>OnRoomEntered</c>, rolling contra <see cref="ShopPoolSO"/>,
    /// persistencia en <c>RoomInstance.ObjectStates</c> como
    /// <see cref="ShopItemState"/> (§13.6). TECHNICAL.md §17.F.
    /// </summary>
    public sealed class ShopManagerService : IShopManagerService, IDisposable
    {
        private const string LogPrefix = "[ShopManagerService] ";
        private const string SpawnPointPrefix = "shop_";

        private readonly ShopConfigSO _config;
        private readonly ShopPoolSO _pool;

        // Override del tutorial (tienda de 1 item). El service es Global, así que
        // el teardown del tutorial debe llamar ClearTutorialOverride().
        private ShopConfigSO _overrideConfig;
        private ShopPoolSO _overridePool;

        private ShopConfigSO ActiveConfig => _overrideConfig != null ? _overrideConfig : _config;
        private ShopPoolSO ActivePool => _overridePool != null ? _overridePool : _pool;

        private readonly Dictionary<Guid, List<ShopSlot>> _slotsByRoom = new Dictionary<Guid, List<ShopSlot>>();
        private readonly HashSet<Guid> _initialized = new HashSet<Guid>();

        private EventManager.EventReceiver _onRoomEnteredHandler;

        public ShopManagerService(ShopConfigSO config, ShopPoolSO pool)
        {
            _config = config;
            _pool = pool;

            _onRoomEnteredHandler = OnRoomEntered;
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
        }

        public void Dispose()
        {
            if (_onRoomEnteredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
                _onRoomEnteredHandler = null;
            }
            _slotsByRoom.Clear();
            _initialized.Clear();
        }

        public IReadOnlyList<ShopSlot> GetSlots(Guid roomInstanceId)
        {
            return _slotsByRoom.TryGetValue(roomInstanceId, out var list)
                ? list
                : Array.Empty<ShopSlot>();
        }

        public bool IsInitialized(Guid roomInstanceId) => _initialized.Contains(roomInstanceId);

        public ShopSlot FindActiveSlot(Guid roomInstanceId, string spawnPointId)
        {
            if (!_slotsByRoom.TryGetValue(roomInstanceId, out var list)) return null;
            foreach (var slot in list)
            {
                if (slot.Purchased) continue;
                if (slot.SpawnPointId == spawnPointId) return slot;
            }
            return null;
        }

        public void NotifyItemPurchased(Guid roomInstanceId, string spawnPointId, int pricePaid)
        {
            var slot = FindActiveSlot(roomInstanceId, spawnPointId);
            if (slot == null) return;

            slot.Purchased = true;

            // Persistir en ObjectStates — fuente de verdad para re-entry.
            if (TryGetDungeonService(out var dungeon)
                && dungeon.GetAllRoomInstances().TryGetValue(roomInstanceId, out var room)
                && room.ObjectStates.TryGet<ShopItemState>(spawnPointId, out var state))
            {
                state.Purchased = true;
                state.Consumed = true;
            }

            if (slot.SpawnedVisual != null)
            {
                UnityEngine.Object.Destroy(slot.SpawnedVisual);
                slot.SpawnedVisual = null;
            }

            var entryId = slot.Item != null ? slot.Item.EntryId : string.Empty;
            EventManager.Trigger(EventName.OnShopItemPurchased, spawnPointId, entryId, pricePaid);
        }

        private const string RestockStateKey = "shop_restock";

        public bool CanRestock(Guid roomInstanceId)
        {
            if (ActiveConfig == null || !ActiveConfig.AllowRestock) return false;
            if (ActiveConfig.MaxRestocks <= 0) return true;
            return GetRestockUses(roomInstanceId) < ActiveConfig.MaxRestocks;
        }

        public int GetRestockCost(Guid roomInstanceId)
            => ActiveConfig != null
                ? ActiveConfig.ResolveRestockCost(GetRestockUses(roomInstanceId))
                : 0;

        public void Restock(Guid roomInstanceId) => TryRestock(roomInstanceId);

        public bool TryRestock(Guid roomInstanceId)
        {
            if (!CanRestock(roomInstanceId)) return false;
            if (!TryGetDungeonService(out var dungeon)
                || !dungeon.GetAllRoomInstances().TryGetValue(roomInstanceId, out var room))
                return false;

            int uses = GetRestockUses(roomInstanceId);
            int cost = ActiveConfig.ResolveRestockCost(uses);

            // Mismo patrón de cobro que el altar: sin economía registrada (tests) el
            // uso es gratis; con ella, el Spend gatea.
            if (ServiceLocator.TryGetService<Rollgeon.Economy.IEconomyService>(out var economy)
                && economy != null && !economy.Spend(cost))
            {
                return false;
            }

            // Usos ANTES de re-rolear: el seed del stock nuevo se saltea con el conteo.
            uses++;
            if (!room.ObjectStates.TryGet<ShopRestockState>(RestockStateKey, out var restockState))
            {
                restockState = new ShopRestockState { SpawnPointId = RestockStateKey };
                room.ObjectStates.Set(RestockStateKey, restockState);
            }
            restockState.Uses = uses;

            // Teardown animado de lo que quede en los pedestales.
            if (_slotsByRoom.TryGetValue(roomInstanceId, out var oldSlots))
            {
                foreach (var slot in oldSlots)
                    DespawnVisualAnimated(slot);
            }

            // Re-roll COMPLETO (Isaac real: los comprados también se rellenan). El rng
            // se saltea por uso — cada restock da stock distinto, pero un resume
            // hidrata de los states persistidos y nunca re-rolea.
            int floorDepth = ServiceLocator.TryGetService<Rollgeon.Run.IRunContextService>(out var runCtx)
                ? runCtx.FloorIndex
                : 0;
            var rng = new System.Random(unchecked(DeriveShopSeed(room) + uses * 7919));
            var spawnPoints = ResolveRewardSpawnPoints(room);
            int slotCount = Mathf.Min(spawnPoints.Count, Mathf.Max(1, ActiveConfig.MaxItemSlots));

            var slots = new List<ShopSlot>(slotCount);
            var rolledInThisShop = new HashSet<IShopRewardEntry>();
            for (int i = 0; i < slotCount; i++)
            {
                var slot = BuildFreshSlot(room, SpawnPointKey(i), rng, floorDepth, rolledInThisShop, guaranteedSlot: false);
                if (slot == null) continue;

                SpawnPedestalVisual(slot, room, spawnPoints[i]);
                AnimateDropIn(slot.SpawnedVisual, i);
                if (slot.Item != null) rolledInThisShop.Add(slot.Item);
                slots.Add(slot);
            }

            _slotsByRoom[roomInstanceId] = slots;
            _initialized.Add(roomInstanceId);

            EventManager.Trigger(EventName.OnShopRestocked, roomInstanceId, cost, uses);
            return true;
        }

        private int GetRestockUses(Guid roomInstanceId)
        {
            if (!TryGetDungeonService(out var dungeon)) return 0;
            if (!dungeon.GetAllRoomInstances().TryGetValue(roomInstanceId, out var room)) return 0;
            return room.ObjectStates.TryGet<ShopRestockState>(RestockStateKey, out var state)
                ? state.Uses
                : 0;
        }

        // Solo el ÍTEM se anima — el pedestal muere en el acto y el nuevo aparece en
        // la misma pose, así que a la vista queda fijo. El ítem saliente se despega
        // del pedestal, se encoge y se hunde antes de morir.
        private static void DespawnVisualAnimated(ShopSlot slot)
        {
            var go = slot?.SpawnedVisual;
            if (go == null) return;
            slot.SpawnedVisual = null;

            var item = FindItemVisual(go);
            if (item == null || !Application.isPlaying || Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion)
            {
                UnityEngine.Object.Destroy(go);
                return;
            }

            item.SetParent(go.transform.parent, worldPositionStays: true);
            UnityEngine.Object.Destroy(go);

            PrimeTween.Tween.Scale(item, Vector3.one * 0.01f, 0.22f, PrimeTween.Ease.InBack);
            PrimeTween.Tween.LocalPositionY(item, item.localPosition.y - 0.4f, 0.22f,
                    PrimeTween.Ease.InQuad)
                .OnComplete(item.gameObject, target => UnityEngine.Object.Destroy(target));
        }

        // El ítem nuevo "cae" sobre su pedestal con rebote, escalonado por slot — la
        // otra mitad del recambio. La pose final ya la puso SpawnItemVisualOnTop.
        private static void AnimateDropIn(GameObject go, int slotIndex)
        {
            if (go == null) return;
            if (!Application.isPlaying || Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;

            var item = FindItemVisual(go);
            if (item == null) return;

            var target = item.localPosition;
            item.localPosition = target + Vector3.up * 2.5f;
            PrimeTween.Tween.LocalPosition(item, target, 0.45f, PrimeTween.Ease.OutBounce,
                startDelay: slotIndex * 0.08f);
        }

        private static Transform FindItemVisual(GameObject pedestalRoot)
        {
            if (pedestalRoot == null) return null;
            foreach (Transform child in pedestalRoot.transform)
                if (child != null && child.name.StartsWith("[ShopItemVisual]", StringComparison.Ordinal))
                    return child;
            return null;
        }

        public void Initialize(RoomInstance room, int floorDepth)
        {
            InitializeInternal(room, floorDepth);
        }

        public void SetTutorialOverride(ShopConfigSO config, ShopPoolSO pool)
        {
            _overrideConfig = config;
            _overridePool = pool;
        }

        public void ClearTutorialOverride()
        {
            _overrideConfig = null;
            _overridePool = null;
        }

        // -----------------------------------------------------------------
        // Internals
        // -----------------------------------------------------------------

        private void OnRoomEntered(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (args[0] is not Guid roomId) return;
            if (_initialized.Contains(roomId)) return;

            if (!TryGetDungeonService(out var dungeon)) return;
            if (!dungeon.GetAllRoomInstances().TryGetValue(roomId, out var room)) return;
            if (room.Template == null || room.Template.Type != RoomType.Shop) return;

            // #158: floor depth real desde el RunContext (antes hardcodeado a 0).
            int floorDepth = ServiceLocator.TryGetService<Rollgeon.Run.IRunContextService>(out var runCtx)
                ? runCtx.FloorIndex
                : 0;
            InitializeInternal(room, floorDepth);
        }

        private void InitializeInternal(RoomInstance room, int floorDepth)
        {
            if (room == null) return;
            if (_initialized.Contains(room.InstanceId)) return;
            if (ActiveConfig == null || ActivePool == null)
            {
                Debug.LogError(LogPrefix + "ShopConfigSO o ShopPoolSO ausentes — no se inicializa la shop.");
                return;
            }

            var spawnPoints = ResolveRewardSpawnPoints(room);
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning(LogPrefix + $"La shop room '{room.Template?.RoomId}' no tiene RewardSpawnPoints — sin slots.");
                _initialized.Add(room.InstanceId);
                return;
            }

            int slotCount = Mathf.Min(spawnPoints.Count, Mathf.Max(1, ActiveConfig.MaxItemSlots));
            // Seed derivado del seed del piso + celda (estables entre regeneraciones)
            // y NO del InstanceId (Guid nuevo por GenerateFloor): un piso resumido
            // desde save restockea la tienda idéntica.
            var rng = new System.Random(DeriveShopSeed(room));
            var slots = new List<ShopSlot>(slotCount);
            // Tracks entries ya rolleadas en esta tienda — pasadas como exclude a Roll()
            // para que cada slot tenga un ítem distinto (mientras el pool tenga variedad).
            var rolledInThisShop = new HashSet<IShopRewardEntry>();

            for (int i = 0; i < slotCount; i++)
            {
                string spawnPointId = SpawnPointKey(i);
                // Slot 0 = entry garantizada (la poción, si el pool la cablea);
                // el resto rolea del pool dinámico de pasivas + extras manuales.
                bool guaranteedSlot = i == 0;
                var slot = BuildOrHydrateSlot(room, spawnPointId, rng, floorDepth, rolledInThisShop, guaranteedSlot);
                if (slot == null) continue;

                if (!slot.Purchased)
                {
                    SpawnPedestalVisual(slot, room, spawnPoints[i]);
                }
                if (slot.Item != null) rolledInThisShop.Add(slot.Item);

                slots.Add(slot);
            }

            _slotsByRoom[room.InstanceId] = slots;
            _initialized.Add(room.InstanceId);

            SpawnRestockMachine(room);
        }

        // La máquina de reroll (§17.F.5): una por tienda, en el punto autorado del
        // layout. Vive dentro del SpawnedPrefab como los pedestales — sin teardown,
        // el DungeonManager mantiene los prefabs toda la run.
        private void SpawnRestockMachine(RoomInstance room)
        {
            if (ActiveConfig == null || !ActiveConfig.AllowRestock) return;
            if (ActiveConfig.RestockMachinePrefab == null) return;
            if (room?.SpawnedPrefab == null) return;

            var layout = room.SpawnedPrefab.GetComponent<RoomLayout>();
            var point = layout != null ? layout.RestockMachinePoint : null;
            if (point == null) return;

            var go = UnityEngine.Object.Instantiate(
                ActiveConfig.RestockMachinePrefab, point.position, point.rotation,
                room.SpawnedPrefab.transform);
            go.name = "[RestockMachine]";
            Rollgeon.Dungeon.Components.PropTileBlocker.Attach(go);

            var interactable = go.GetComponent<RestockMachineInteractable>();
            if (interactable != null) interactable.Configure(room.InstanceId, this);
            else Debug.LogError(LogPrefix + "RestockMachinePrefab sin RestockMachineInteractable.");
        }

        private int DeriveShopSeed(RoomInstance room)
        {
            if (!TryGetDungeonService(out var dungeon))
                return room.InstanceId.GetHashCode();
            return DeriveShopSeed(dungeon.CurrentFloorSeed, room.GridCell);
        }

        /// <summary>Puro para tests: mismo (floorSeed, celda) → mismo stock.</summary>
        public static int DeriveShopSeed(int floorSeed, Vector2Int cell)
        {
            unchecked
            {
                // Mismo estilo que FloorProgressionService.DeriveSeed.
                return floorSeed * 92821 + cell.x * 31 + cell.y;
            }
        }

        private ShopSlot BuildOrHydrateSlot(
            RoomInstance room, string spawnPointId, System.Random rng, int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> rolledInThisShop, bool guaranteedSlot)
        {
            if (room.ObjectStates.TryGet<ShopItemState>(spawnPointId, out var state))
            {
                // Re-entry: hidratamos desde el state persistido. No re-rolear.
                var entry = ResolveEntryFromPool(state.ReservedItemId);
                if (entry == null)
                {
                    Debug.LogWarning(LogPrefix + $"ReservedItemId '{state.ReservedItemId}' no encontrado en el pool — slot se omite.");
                    return null;
                }
                return new ShopSlot
                {
                    SpawnPointId = spawnPointId,
                    Item = entry,
                    Price = state.ReservedPrice,
                    Purchased = state.Purchased,
                };
            }

            return BuildFreshSlot(room, spawnPointId, rng, floorDepth, rolledInThisShop, guaranteedSlot);
        }

        /// <summary>
        /// Rolea un slot NUEVO y persiste su state, pisando el anterior si lo había.
        /// Camino compartido entre la primera visita y el restock (que fuerza re-roll
        /// también sobre slots ya comprados — Isaac real).
        /// </summary>
        private ShopSlot BuildFreshSlot(
            RoomInstance room, string spawnPointId, System.Random rng, int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> rolledInThisShop, bool guaranteedSlot)
        {
            // Rolear + persistir. Pasamos los rolled previos como exclude para evitar
            // duplicados dentro del mismo shop. El slot garantizado (poción) saltea el
            // roll; sin garantizado cableado (ej. tutorial) degrada al roll normal.
            ShopRollResult rolled;
            if (!guaranteedSlot || !ActivePool.TryGetGuaranteed(out rolled))
            {
                rolled = ActivePool.RollDynamic(rng, floorDepth, rolledInThisShop);
            }
            if (rolled.Item == null)
            {
                Debug.LogWarning(LogPrefix + "Pool vacío o sin entries eligibles — slot se omite.");
                return null;
            }

            int price = ActiveConfig.ResolvePrice(rolled.BasePrice, rng);
            var newState = new ShopItemState
            {
                SpawnPointId = spawnPointId,
                ReservedItemId = rolled.Item.EntryId,
                ReservedPrice = price,
                Purchased = false,
                Consumed = false,
            };
            room.ObjectStates.Set(spawnPointId, newState);

            return new ShopSlot
            {
                SpawnPointId = spawnPointId,
                Item = rolled.Item,
                Price = price,
                Purchased = false,
            };
        }

        private void SpawnPedestalVisual(ShopSlot slot, RoomInstance room, Transform spawnPoint)
        {
            if (ActiveConfig.PedestalPrefab == null)
            {
                Debug.LogWarning(LogPrefix + "ShopConfigSO.PedestalPrefab sin asignar — no se instancia visual.");
                return;
            }
            if (spawnPoint == null) return;

            Transform parent = room.SpawnedPrefab != null ? room.SpawnedPrefab.transform : null;
            var go = UnityEngine.Object.Instantiate(ActiveConfig.PedestalPrefab, spawnPoint.position, spawnPoint.rotation, parent);
            go.name = $"[ShopPedestal] {slot.Item?.DisplayName ?? slot.Item?.EntryId ?? "?"}";
            // El pedestal ocupa su celda — al comprarse se destruye el visual y el
            // OnDisable del blocker libera la celda.
            Rollgeon.Dungeon.Components.PropTileBlocker.Attach(go);

            var pedestal = go.GetComponent<ShopItemPedestalInteractable>();
            if (pedestal == null)
            {
                Debug.LogError(LogPrefix + "PedestalPrefab no tiene ShopItemPedestalInteractable — no se puede cablear la compra.");
            }
            else
            {
                pedestal.Configure(room.InstanceId, slot, this);
            }

            SpawnItemVisualOnTop(go.transform, slot);
            slot.SpawnedVisual = go;
        }

        /// <summary>
        /// Instancia el visual 3D del reward como hijo del pedestal. Usa el
        /// <see cref="ItemSO.WorldPrefab"/> del item. Posiciona via
        /// <see cref="ShopConfigSO.ItemVisualLocalOffset"/> (default Y=1.5).
        /// </summary>
        private void SpawnItemVisualOnTop(Transform pedestalRoot, ShopSlot slot)
        {
            if (pedestalRoot == null || slot?.Item == null) return;

            var prefab = ResolveWorldPrefab(slot.Item);
            bool isFallback = false;
            if (prefab == null)
            {
                // El item no trae WorldPrefab propio: usar el visual genérico tinteado
                // para que igual haya algo sobre el pedestal (placeholder).
                prefab = ActiveConfig?.DefaultItemVisualPrefab;
                isFallback = true;
            }
            if (prefab == null) return;

            var visual = UnityEngine.Object.Instantiate(prefab, pedestalRoot);
            visual.transform.localPosition = ActiveConfig != null ? ActiveConfig.ItemVisualLocalOffset : new Vector3(0f, 1.5f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            string displayName = slot.Item.DisplayName ?? slot.Item.EntryId ?? "?";
            visual.name = $"[ShopItemVisual] {displayName}";

            if (isFallback && ActiveConfig != null)
                ApplyVisualTint(visual, ActiveConfig.DefaultItemVisualTint);
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Tinta todos los renderers vía MaterialPropertyBlock (sin instanciar materiales).
        // Setea _BaseColor (URP/Lit) y _Color (Built-in/legacy); las props inexistentes se ignoran.
        private static void ApplyVisualTint(GameObject go, Color tint)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, tint);
                mpb.SetColor(ColorId, tint);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Dispatch polimórfico del WorldPrefab. Cuando se agregue un nuevo tipo
        /// de <see cref="IShopRewardEntry"/>, sumar el case.
        /// </summary>
        private static GameObject ResolveWorldPrefab(IShopRewardEntry entry)
        {
            switch (entry)
            {
                case ItemSO item:
                    return item.WorldPrefab;
                case Rollgeon.Upgrades.Combos.ComboPassiveSO passive:
                    return passive.WorldPrefab;
                default:
                    return null;
            }
        }

        private List<Transform> ResolveRewardSpawnPoints(RoomInstance room)
        {
            var list = new List<Transform>();
            if (room?.SpawnedPrefab == null) return list;

            var layout = room.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null || layout.RewardSpawnPoints == null) return list;

            foreach (var t in layout.RewardSpawnPoints)
            {
                if (t != null) list.Add(t);
            }
            return list;
        }

        private IShopRewardEntry ResolveEntryFromPool(string entryId)
        {
            if (ActivePool == null || string.IsNullOrEmpty(entryId)) return null;

            var guaranteed = ActivePool.Guaranteed.GetEntry();
            if (guaranteed != null && guaranteed.EntryId == entryId) return guaranteed;

            if (ActivePool.Items != null)
            {
                foreach (var weighted in ActivePool.Items)
                {
                    var entry = weighted.GetEntry();
                    if (entry == null) continue;
                    if (entry.EntryId == entryId) return entry;
                }
            }

            return null;
        }

        private static bool TryGetDungeonService(out IDungeonService dungeon)
            => ServiceLocator.TryGetService(out dungeon);

        private static string SpawnPointKey(int index) => SpawnPointPrefix + index;
    }
}
