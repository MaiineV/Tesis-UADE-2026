using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Combos.Counters;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Rezagado (<see cref="LeastUsedComboService"/>): elige una vez el combo menos usado,
    /// desempata por orden de hoja, suma al canal aditivo de M solo en ese combo y persiste.
    /// </summary>
    [TestFixture]
    public class LeastUsedComboServiceTests
    {
        private static readonly string[] Sheet = { "combo.pair", "combo.trio", "combo.poker", "combo.generala" };

        private InventoryService _inventory;
        private ComboPlayService _play;
        private LeastUsedComboService _service;
        private FakeCounters _counters;
        private Guid _player;
        private readonly List<UnityEngine.Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<ComboPlayedPayload>.Clear();
            SaveSystem.ResetForTests();
            _player = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService(_player));
            _inventory = new InventoryService(null, 4);
            ServiceLocator.AddService<IInventoryService>(_inventory, ServiceScope.Global);
            _counters = new FakeCounters();
            ServiceLocator.AddService<IComboCountersService>(_counters, ServiceScope.Global);
            _play = new ComboPlayService();
            _play.Register();
            _service = new LeastUsedComboService();
            _service.SubscribeForTests(() => Sheet);
            ServiceLocator.AddService<ILeastUsedComboService>(_service, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _play?.Dispose();
            _inventory?.Dispose();
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<ComboPlayedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        private ItemSO NewRezagado(float bonus = 0.5f)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "rezagado";
            item.DisplayName = "Rezagado";
            item.Type = ItemType.Passive;
            item.LeastUsedComboBonus = new LeastUsedComboBonusDef { Enabled = true, MultiplierBonus = bonus };
            _created.Add(item);
            return item;
        }

        private EnchantmentScratch Play(string comboId, RollActionKind kind = RollActionKind.Attack)
        {
            _play.BeginPlay(new EffectContext
            {
                SourceGuid = _player,
                DiceResult = new[] { 2, 2, 5 },
                ActionKind = kind,
                ComboResult = ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: 2,
                    contributingIndices: new[] { 0, 1 }),
            });
            var scratch = _play.CurrentPlayScratch;
            _play.EndPlay();
            return scratch;
        }

        // ================================================================
        // PickLeastUsed
        // ================================================================

        [Test]
        public void PickLeastUsed_ChoosesTheMinimum_TieGoesToSheetOrder()
        {
            var counts = new Dictionary<string, int> { ["combo.pair"] = 5, ["combo.trio"] = 1, ["combo.poker"] = 1 };
            int Count(string id) => counts.TryGetValue(id, out var n) ? n : 0;

            // generala nunca matcheó (0) → gana aunque esté última.
            Assert.AreEqual("combo.generala", LeastUsedComboService.PickLeastUsed(Sheet, Count));

            counts["combo.generala"] = 1;
            // Empate trio/poker/generala en 1 → el primero de la hoja.
            Assert.AreEqual("combo.trio", LeastUsedComboService.PickLeastUsed(Sheet, Count));

            Assert.IsNull(LeastUsedComboService.PickLeastUsed(Array.Empty<string>(), Count));
            Assert.IsNull(LeastUsedComboService.PickLeastUsed(null, Count));
        }

        // ================================================================
        // Asignación al adquirir
        // ================================================================

        [Test]
        public void OnAcquire_AssignsLeastUsedCombo_AndAnnouncesIt()
        {
            _counters.Counts["combo.pair"] = 4;
            _counters.Counts["combo.trio"] = 2;
            _counters.Counts["combo.poker"] = 0;
            _counters.Counts["combo.generala"] = 3;
            string announced = null;
            EventManager.Subscribe(EventName.OnLeastUsedComboAssigned, args => announced = (string)args[2]);

            _inventory.AddItem(NewRezagado());

            Assert.AreEqual("combo.poker", _service.GetAssignedCombo("rezagado"));
            Assert.AreEqual("combo.poker", announced);
        }

        [Test]
        public void OnAcquire_DoesNotReevaluate_WhenCountersChangeLater()
        {
            _counters.Counts["combo.pair"] = 9;
            _counters.Counts["combo.trio"] = 2;
            _counters.Counts["combo.poker"] = 0;
            _counters.Counts["combo.generala"] = 2;
            _inventory.AddItem(NewRezagado());
            Assert.AreEqual("combo.poker", _service.GetAssignedCombo("rezagado"));

            _counters.Counts["combo.poker"] = 50;
            EventManager.Trigger(EventName.OnItemObtained, _player, "rezagado");

            Assert.AreEqual("combo.poker", _service.GetAssignedCombo("rezagado"));
        }

        [Test]
        public void ItemWithoutTheDef_IsIgnored()
        {
            var plain = ScriptableObject.CreateInstance<ItemSO>();
            plain.ItemId = "plain";
            plain.Type = ItemType.Passive;
            _created.Add(plain);

            _inventory.AddItem(plain);

            Assert.IsNull(_service.GetAssignedCombo("plain"));
        }

        [Test]
        public void Remove_ClearsTheAssignment()
        {
            _inventory.AddItem(NewRezagado());
            Assert.IsNotNull(_service.GetAssignedCombo("rezagado"));

            _inventory.RemoveItem("rezagado");

            Assert.IsNull(_service.GetAssignedCombo("rezagado"));
        }

        // ================================================================
        // Bono en el play scratch
        // ================================================================

        [Test]
        public void Attack_WithAssignedCombo_AddsToMultiplierBonus_AndJournalsTheItem()
        {
            _counters.Counts["combo.pair"] = 1;
            _counters.Counts["combo.trio"] = 1;
            _counters.Counts["combo.poker"] = 1;
            // generala = 0 → asignada
            var item = NewRezagado(0.5f);
            _inventory.AddItem(item);

            var scratch = Play("combo.generala");

            Assert.AreEqual(0.5f, scratch.ComboMultiplierBonus, 1e-4f);
            bool journaled = false;
            foreach (var entry in scratch.Journal)
                if (entry.SourceId == "rezagado" && Math.Abs(entry.MultiplierBonusDelta - 0.5f) < 1e-4f) journaled = true;
            Assert.IsTrue(journaled, "el breakdown necesita la entrada del item");
        }

        [Test]
        public void OtherCombo_OrNonAttack_AddsNothing()
        {
            _inventory.AddItem(NewRezagado()); // todo en 0 → combo.pair (primero de la hoja)
            Assert.AreEqual("combo.pair", _service.GetAssignedCombo("rezagado"));

            Assert.AreEqual(0f, Play("combo.trio").ComboMultiplierBonus, 1e-4f);
            Assert.AreEqual(0f, Play("combo.pair", RollActionKind.Defense).ComboMultiplierBonus, 1e-4f);
            Assert.AreEqual(0.5f, Play("combo.pair").ComboMultiplierBonus, 1e-4f);
        }

        [Test]
        public void UnassignedItemInInventory_AssignsLazilyOnFirstPlay()
        {
            // Simula un save viejo: el item está pero el servicio no tiene la asignación.
            var item = NewRezagado();
            _inventory.AddItem(item);
            _service.RestoreState(new Dictionary<string, string>());
            Assert.IsNull(_service.GetAssignedCombo("rezagado"));

            Play("combo.trio");

            Assert.AreEqual("combo.pair", _service.GetAssignedCombo("rezagado"));
        }

        // ================================================================
        // Save
        // ================================================================

        [Test]
        public void CaptureRestore_RoundTripsTheAssignment()
        {
            _counters.Counts["combo.pair"] = 3;
            _inventory.AddItem(NewRezagado());
            var state = _service.CaptureState();

            var fresh = new LeastUsedComboService();
            try
            {
                fresh.RestoreState(state);
                Assert.AreEqual("combo.trio", fresh.GetAssignedCombo("rezagado"));
            }
            finally
            {
                fresh.Dispose();
            }
        }

        // ================================================================
        // Fakes
        // ================================================================

        private sealed class FakeCounters : IComboCountersService
        {
            public readonly Dictionary<string, int> Counts = new();
            public int GetCount(string comboId) => Counts.TryGetValue(comboId, out var n) ? n : 0;
            public void IncrementCount(string comboId) => Counts[comboId] = GetCount(comboId) + 1;
            public float GetBonusMultiplier(string comboId) => 1f;
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public StubPlayerService(Guid guid) { PlayerGuid = guid; }
            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet { add { } remove { } }
            public event Action OnPlayerCleared { add { } remove { } }
        }
    }
}
