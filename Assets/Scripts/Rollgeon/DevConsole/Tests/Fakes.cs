using System;
using System.Collections.Generic;
using Rollgeon.DevConsole.Commands;
using Rollgeon.DevConsole.Core;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Items;
using Rollgeon.Player;

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
    }

    public sealed class FakeInventoryService : IInventoryService
    {
        public readonly List<ItemSO> Added = new List<ItemSO>();

        public IReadOnlyList<InventorySlot> PassiveItems => Array.Empty<InventorySlot>();
        public IReadOnlyList<InventorySlot> ActiveItems => Array.Empty<InventorySlot>();
        public int MaxActiveSlots => 3;

        public bool AddItem(ItemSO item) { Added.Add(item); return true; }
        public bool RemoveItem(string itemId) => false;
        public bool HasItem(string itemId) => false;
        public ItemSO GetItem(string itemId) => null;
        public bool ActivateItem(int activeSlotIndex, EffectContext ctx) => false;
        public void TickCooldowns() { }

#pragma warning disable 67
        public event Action<ItemSO, bool> OnItemChanged;
#pragma warning restore 67
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
