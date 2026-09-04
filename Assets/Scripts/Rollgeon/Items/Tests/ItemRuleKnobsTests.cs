using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Healing;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Las perillas de regla de <see cref="ItemSO"/> (Ayuno: <c>BlocksPassiveItemHealing</c>)
    /// se registran en su servicio al entrar el item y se sueltan al salir / al limpiar.
    /// </summary>
    [TestFixture]
    public class ItemRuleKnobsTests
    {
        private InventoryService _service;
        private HealingRuleService _rules;
        private readonly List<UnityEngine.Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService(Guid.NewGuid()));
            _rules = new HealingRuleService();
            _rules.Register();
            _service = new InventoryService(null, 4);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _rules?.Dispose();
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private ItemSO NewAyuno(string id = "ayuno")
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Passive;
            item.BlocksPassiveItemHealing = true;
            _created.Add(item);
            return item;
        }

        [Test]
        public void AddItem_WithHealingBlock_RegistersRule()
        {
            _service.AddItem(NewAyuno());

            Assert.IsTrue(_rules.PassiveItemHealingBlocked);
        }

        [Test]
        public void RemoveItem_ReleasesRule()
        {
            var item = NewAyuno();
            _service.AddItem(item);

            _service.RemoveItem(item.ItemId);

            Assert.IsFalse(_rules.PassiveItemHealingBlocked);
        }

        [Test]
        public void Dispose_ReleasesRule()
        {
            _service.AddItem(NewAyuno());

            _service.Dispose();
            _service = null;

            Assert.IsFalse(_rules.PassiveItemHealingBlocked);
        }

        [Test]
        public void ItemWithoutKnob_DoesNotTouchRule()
        {
            var plain = ScriptableObject.CreateInstance<ItemSO>();
            plain.ItemId = "plain";
            plain.DisplayName = "plain";
            plain.Type = ItemType.Passive;
            _created.Add(plain);

            _service.AddItem(plain);
            _service.RemoveItem("plain");

            Assert.IsFalse(_rules.PassiveItemHealingBlocked);
        }

        // ---- PotionHealMultiplier (Ayuno: la poción cura la mitad) ----------------------

        private ItemSO NewPotionScaler(string id, float factor)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Passive;
            item.PotionHealMultiplier = factor;
            _created.Add(item);
            return item;
        }

        [Test]
        public void AddItem_WithPotionHealMultiplier_RegistersFactorUnderItsId()
        {
            _service.AddItem(NewPotionScaler("ayuno", 0.5f));

            Assert.AreEqual(0.5f, _rules.PotionHealMultiplier, 0.0001f);
            Assert.AreEqual(0.5f, _rules.PotionHealMultiplierSources["ayuno"], 0.0001f);
            Assert.IsFalse(_rules.PassiveItemHealingBlocked, "el multiplicador no bloquea curas pasivas");
        }

        [Test]
        public void RemoveItem_ReleasesPotionHealMultiplier()
        {
            var item = NewPotionScaler("ayuno", 0.5f);
            _service.AddItem(item);

            _service.RemoveItem(item.ItemId);

            Assert.AreEqual(1f, _rules.PotionHealMultiplier, 0.0001f);
        }

        [Test]
        public void Dispose_ReleasesPotionHealMultiplier()
        {
            _service.AddItem(NewPotionScaler("ayuno", 0.5f));

            _service.Dispose();
            _service = null;

            Assert.AreEqual(1f, _rules.PotionHealMultiplier, 0.0001f);
        }

        [Test]
        public void ItemWithDefaultPotionHealMultiplier_DoesNotRegister()
        {
            _service.AddItem(NewPotionScaler("plain", 1f));

            Assert.AreEqual(0, _rules.PotionHealMultiplierSources.Count);
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
#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
