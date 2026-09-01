using System;
using System.Collections.Generic;
using Rollgeon.DevConsole.Commands;
using Rollgeon.DevConsole.Core;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Items;
using Rollgeon.Player;
using Rollgeon.Shop;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.DevConsole.Tests
{
    /// <summary>Contexto de consola fakeado: servicios inyectables + buffer de log.</summary>
    public sealed class FakeConsoleContext : IDevConsoleContext
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public BufferLogSink Buffer { get; } = new BufferLogSink();
        public ILogSink Log => Buffer;
        public bool IsRunActive { get; set; } = true;
        public Guid PlayerGuid { get; set; } = Guid.NewGuid();

        public void Register<T>(T service) => _services[typeof(T)] = service;

        public bool TryResolve<T>(out T service)
        {
            if (_services.TryGetValue(typeof(T), out var s)) { service = (T)s; return true; }
            service = default;
            return false;
        }

        public T Resolve<T>()
        {
            if (_services.TryGetValue(typeof(T), out var s)) return (T)s;
            throw new KeyNotFoundException(typeof(T).Name);
        }
    }

    public sealed class FakePlayerService : IPlayerService
    {
        public Guid PlayerGuid { get; set; }
        public Guid RunId { get; set; }
        public ClassHeroSO CurrentHero { get; set; }
        public DiceBagSO DiceBag { get; set; }

        public void SetPlayer(ClassHeroSO hero, Guid runId) { CurrentHero = hero; RunId = runId; }
        public void SetDiceBag(DiceBagSO bag) { DiceBag = bag; }
        public void ClearPlayer() { CurrentHero = null; }

#pragma warning disable 67
        public event Action<ClassHeroSO> OnPlayerSet;
        public event Action OnPlayerCleared;
#pragma warning restore 67
    }

    public sealed class FakeEconomyService : IEconomyService
    {
        public int CurrentGold { get; private set; }
        public FakeEconomyService(int start = 0) { CurrentGold = start < 0 ? 0 : start; }
        public void Add(int amount) { if (amount > 0) CurrentGold += amount; }
        public bool Spend(int amount) { if (amount <= CurrentGold) { CurrentGold -= amount; return true; } return false; }
        public bool CanAfford(int amount) => amount <= CurrentGold;
        public void ResetTo(int amount) { CurrentGold = amount < 0 ? 0 : amount; }
    }

    public sealed class FakeInventoryService : IInventoryService
    {
        public readonly List<ItemSO> Added = new List<ItemSO>();

        /// <summary>Simula el rechazo de <c>AddItem</c> por slots activos llenos.</summary>
        public bool RejectAdd { get; set; }

        public IReadOnlyList<InventorySlot> PassiveItems => Array.Empty<InventorySlot>();
        public IReadOnlyList<InventorySlot> ActiveItems => Array.Empty<InventorySlot>();
        public int MaxActiveSlots { get; set; } = 3;

        public bool AddItem(ItemSO item)
        {
            if (RejectAdd) return false;
            Added.Add(item);
            return true;
        }
        public bool RemoveItem(string itemId) => false;
        public bool HasItem(string itemId) => false;
        public ItemSO GetItem(string itemId) => null;
        public bool ActivateItem(int activeSlotIndex, EffectContext ctx) => false;
        public ItemActivationBlock CanActivateItem(int activeSlotIndex, EffectContext ctx)
            => ItemActivationBlock.InvalidSlot;
        public int GetComboDamageBonusPreview(string comboId) => 0;
        public void TickCooldowns() { }

#pragma warning disable 67
        public event Action<ItemSO, bool> OnItemChanged;
#pragma warning restore 67
    }

    /// <summary>Dungeon con un set fijo de rooms — para los comandos que buscan una sala
    /// por <see cref="RoomType"/> sin generar un piso.</summary>
    public sealed class FakeDungeonService : IDungeonService
    {
        public readonly Dictionary<Guid, RoomInstance> Rooms = new Dictionary<Guid, RoomInstance>();

        public RoomInstance CurrentRoomInstance { get; set; }
        public RoomSO CurrentRoom => CurrentRoomInstance?.Template;
        public DoorDirection? LastEntryDirection => null;

        /// <summary>Crea una room del tipo pedido y la registra. Devuelve su instanceId.</summary>
        public Guid AddRoom(RoomType type, out RoomInstance instance)
        {
            var template = ScriptableObject.CreateInstance<RoomSO>();
            template.Type = type;
            instance = new RoomInstance { InstanceId = Guid.NewGuid(), Template = template };
            Rooms[instance.InstanceId] = instance;
            return instance.InstanceId;
        }

        public void GenerateFloor(FloorLayoutSO layout, int seed) { }
        public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => Rooms;
        public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => new Dictionary<Guid, FloorShell>();

        public bool CanEnterRoomByDoor(DoorDirection direction, out Guid neighborInstanceId)
        {
            neighborInstanceId = Guid.Empty;
            return false;
        }

        public bool EnterRoomByDoor(DoorDirection direction) => false;

        public bool EnterRoomByInstanceId(Guid instanceId)
        {
            if (!Rooms.TryGetValue(instanceId, out var room)) return false;
            CurrentRoomInstance = room;
            return true;
        }

        public bool SetRoomState(Guid instanceId, RoomState state) => false;
        public void ResyncDoorVisuals(Guid instanceId) { }
        public Bounds GetFloorBounds() => default;
        public IReadOnlyList<GameCamera.WallOccluder> GetCurrentRoomOccluders() =>
            Array.Empty<GameCamera.WallOccluder>();
    }

    /// <summary>Shop manager con slots inyectados y registro de las compras cerradas.</summary>
    public sealed class FakeShopManagerService : IShopManagerService
    {
        public readonly Dictionary<Guid, List<ShopSlot>> SlotsByRoom = new Dictionary<Guid, List<ShopSlot>>();
        public readonly HashSet<Guid> InitializedRooms = new HashSet<Guid>();
        public readonly List<(Guid room, string spawnPoint, int price)> Purchases =
            new List<(Guid, string, int)>();

        public IReadOnlyList<ShopSlot> GetSlots(Guid roomInstanceId) =>
            SlotsByRoom.TryGetValue(roomInstanceId, out var slots) ? slots : Array.Empty<ShopSlot>();

        public bool IsInitialized(Guid roomInstanceId) => InitializedRooms.Contains(roomInstanceId);

        public ShopSlot FindActiveSlot(Guid roomInstanceId, string spawnPointId)
        {
            foreach (var slot in GetSlots(roomInstanceId))
                if (!slot.Purchased && slot.SpawnPointId == spawnPointId) return slot;
            return null;
        }

        public void NotifyItemPurchased(Guid roomInstanceId, string spawnPointId, int pricePaid)
        {
            Purchases.Add((roomInstanceId, spawnPointId, pricePaid));
            var slot = FindActiveSlot(roomInstanceId, spawnPointId);
            if (slot != null) slot.Purchased = true;
        }

        public bool CanRestock(Guid roomInstanceId) => false;
        public void Restock(Guid roomInstanceId) { }
        public void Initialize(RoomInstance room, int floorDepth) { }
        public void SetTutorialOverride(ShopConfigSO config, ShopPoolSO pool) { }
        public void ClearTutorialOverride() { }
    }

    /// <summary>
    /// Servicio de encantamientos con un <see cref="RuntimeDiceBag"/> real
    /// (listas append-only sin techo) y una regla de aceptación inyectable para
    /// simular qué rechaza <c>ValidateApply</c>.
    /// </summary>
    public sealed class FakeDiceEnchantmentService : IDiceEnchantmentService
    {
        public readonly List<(int bag, int slot, EnchantmentSO ench)> Applied =
            new List<(int, int, EnchantmentSO)>();

        /// <summary>Qué acepta <see cref="ValidateApply"/>. Default: todo.</summary>
        public Func<EnchantmentSO, bool> Accepts = _ => true;

        public FakeDiceEnchantmentService(params DiceType[] dice)
        {
            Bag = new RuntimeDiceBag(dice != null && dice.Length > 0 ? dice : new[] { DiceType.D6 });
        }

        public RuntimeDiceBag Bag { get; }
        public bool IsReady { get; set; } = true;
        public EnchantmentScratch LastComboScratch => null;

        public IReadOnlyCollection<int> ComputeAllowedFaces(int bagIndex) => Array.Empty<int>();

        public EnchantmentApplyResult ValidateApply(int bagIndex, EnchantmentSO ench)
            => Accepts(ench) ? EnchantmentApplyResult.Ok(null) : EnchantmentApplyResult.Fail("rechazado por el fake");

        public EnchantmentApplyResult Apply(int bagIndex, EnchantmentSO ench)
        {
            var validation = ValidateApply(bagIndex, ench);
            if (!validation.Success) return validation;

            int slot = Bag.AddEnchantment(bagIndex, ench);
            if (slot < 0) return EnchantmentApplyResult.Fail("bagIndex inválido en el fake");
            Applied.Add((bagIndex, slot, ench));
            return EnchantmentApplyResult.Ok(null, slot);
        }

        public bool Remove(int bagIndex, int enchSlotIndex)
        {
            if (Bag.GetEnchantmentAt(bagIndex, enchSlotIndex) == null) return false;
            return Bag.SetEnchantmentAt(bagIndex, enchSlotIndex, null);
        }

        public EnchantmentScratch ResolveComboBonus(Guid sourceGuid, string comboId,
            IReadOnlyList<int> diceResult, int comboBaseDamage) => null;

        public void InitializeFromBag(DiceBagSO bag) { }
    }

    /// <summary>Altar de encantamiento con oferta y confirmación predefinidas.</summary>
    public sealed class FakeEnchantmentRoomService : IEnchantmentRoomService
    {
        public EnchantmentOfferResult NextOffer = EnchantmentOfferResult.Fail("sin configurar");
        public EnchantmentRollResult NextChoice = EnchantmentRollResult.Fail("sin configurar");
        public int RollOfferCalls;
        public readonly List<(int option, int bag)> ConfirmCalls = new List<(int, int)>();

        public bool IsInitialized(Guid roomInstanceId) => true;
        public void NotifyAltarActivated(Guid roomInstanceId, string spawnPointId) { }
        public int ResolveCost() => 0;

        public EnchantmentOfferResult RollOffer(Guid roomInstanceId)
        {
            RollOfferCalls++;
            if (NextOffer.Success) CurrentOffer = NextOffer.Offer;
            return NextOffer;
        }

        public EnchantmentRollResult ConfirmChoice(int optionIndex, int bagIndex)
        {
            ConfirmCalls.Add((optionIndex, bagIndex));
            if (NextChoice.Success) CurrentOffer = null;
            return NextChoice;
        }

        public EnchantmentOffer? CurrentOffer { get; set; }

        public void ClearOffer() => CurrentOffer = null;
    }

    /// <summary>Comando stub para tests de parser/registry/autocomplete.</summary>
    public sealed class FakeCommand : DevCommandBase
    {
        private readonly string _name;
        private readonly string[] _aliases;
        private readonly ArgSpec[] _args;

        public FakeCommand(string name, string[] aliases = null, ArgSpec[] args = null)
        {
            _name = name;
            _aliases = aliases ?? Array.Empty<string>();
            _args = args ?? Array.Empty<ArgSpec>();
        }

        public override string Name => _name;
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description => "fake";
        public override IReadOnlyList<ArgSpec> Args => _args;
        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx) => CommandResult.Ok();
    }
}
