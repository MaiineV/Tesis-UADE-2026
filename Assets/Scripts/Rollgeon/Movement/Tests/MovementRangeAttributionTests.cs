using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects;
using Rollgeon.Items;
using Rollgeon.Movement.Die;
using UnityEngine;

namespace Rollgeon.Movement.Tests
{
    /// <summary>
    /// Atribución por item del bonus de MoveRange (§6.6): qué chips muestra el dado de
    /// Movimiento ("Botas Ligeras +1"). Cubre la función pura y el wrapper por ServiceLocator.
    /// </summary>
    [TestFixture]
    public sealed class MovementRangeAttributionTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private readonly List<MovementRangeContribution> _into = new List<MovementRangeContribution>();

        [SetUp]
        public void SetUp() => ServiceLocator.Clear();

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _created) if (so != null) UnityEngine.Object.DestroyImmediate(so);
            _created.Clear();
            ServiceLocator.Clear();
        }

        private ItemSO Item(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            _created.Add(item);
            return item;
        }

        private static Modifier<int> Mod(int amount, ModifierOperation op, Guid source)
            => new Modifier<int>(amount, op, 0, Guid.NewGuid(), source,
                                 ModifierDirection.Intrinsic, ModifierLifetime.Permanent, default);

        // ---- Función pura ----------------------------------------------------

        [Test]
        public void Resolve_AddModifierFromItem_YieldsPositiveDeltaWithAsset()
        {
            var botas = Item("botas.ligeras");
            var mods = new[] { Mod(1, ModifierOperation.Add, ItemPassiveSourceId.For("botas.ligeras")) };

            MovementRangeAttribution.Resolve(mods, new[] { botas }, _into);

            Assert.AreEqual(1, _into.Count);
            Assert.AreSame(botas, _into[0].SourceAsset);
            Assert.AreEqual(1, _into[0].Delta);
        }

        [Test]
        public void Resolve_SubtractModifier_YieldsNegativeDelta()
        {
            var guantelete = Item("guantelete.pesado");
            var mods = new[] { Mod(1, ModifierOperation.Subtract, ItemPassiveSourceId.For("guantelete.pesado")) };

            MovementRangeAttribution.Resolve(mods, new[] { guantelete }, _into);

            Assert.AreEqual(1, _into.Count);
            Assert.AreEqual(-1, _into[0].Delta);
        }

        [Test]
        public void Resolve_UnknownSource_IsSkipped()
        {
            var botas = Item("botas.ligeras");
            var mods = new[] { Mod(2, ModifierOperation.Add, Guid.NewGuid()) }; // reward "Movimiento+", no item

            MovementRangeAttribution.Resolve(mods, new[] { botas }, _into);

            Assert.IsEmpty(_into);
        }

        [Test]
        public void Resolve_TwoModifiersSameItem_MergeIntoOneEntry()
        {
            var botas = Item("botas.ligeras");
            var src = ItemPassiveSourceId.For("botas.ligeras");
            var mods = new[] { Mod(1, ModifierOperation.Add, src), Mod(2, ModifierOperation.Add, src) };

            MovementRangeAttribution.Resolve(mods, new[] { botas }, _into);

            Assert.AreEqual(1, _into.Count);
            Assert.AreEqual(3, _into[0].Delta);
        }

        [Test]
        public void Resolve_NonAdditiveOperation_IsIgnored()
        {
            var botas = Item("botas.ligeras");
            var mods = new[] { Mod(2, ModifierOperation.Multiply, ItemPassiveSourceId.For("botas.ligeras")) };

            MovementRangeAttribution.Resolve(mods, new[] { botas }, _into);

            Assert.IsEmpty(_into);
        }

        [Test]
        public void Resolve_FollowsInventoryOrder_AndSkipsItemsWithoutModifier()
        {
            var botas = Item("botas.ligeras");
            var espada = Item("espada");
            var guantelete = Item("guantelete.pesado");
            var mods = new[]
            {
                Mod(1, ModifierOperation.Subtract, ItemPassiveSourceId.For("guantelete.pesado")),
                Mod(1, ModifierOperation.Add, ItemPassiveSourceId.For("botas.ligeras")),
            };

            MovementRangeAttribution.Resolve(mods, new[] { botas, espada, guantelete }, _into);

            Assert.AreEqual(2, _into.Count);
            Assert.AreSame(botas, _into[0].SourceAsset);
            Assert.AreEqual(1, _into[0].Delta);
            Assert.AreSame(guantelete, _into[1].SourceAsset);
            Assert.AreEqual(-1, _into[1].Delta);
        }

        [Test]
        public void Resolve_ClearsTargetList_WhenNothingApplies()
        {
            _into.Add(new MovementRangeContribution(null, 9));

            MovementRangeAttribution.Resolve(Array.Empty<Modifier<int>>(), Array.Empty<ItemSO>(), _into);

            Assert.IsEmpty(_into);
        }

        // ---- Wrapper por ServiceLocator ---------------------------------------

        [Test]
        public void ResolveByGuid_WithoutServices_IsEmptyAndSilent()
        {
            _into.Add(new MovementRangeContribution(null, 9));

            MovementRangeAttribution.Resolve(Guid.NewGuid(), _into);

            Assert.IsEmpty(_into);
        }

        [Test]
        public void ResolveByGuid_ReadsMoveRangeModifiersAndInventory()
        {
            var owner = Guid.NewGuid();
            var botas = Item("botas.ligeras");

            var attrs = new AttributesManager();
            var a = new ModifiableAttributes();
            a.SetAttribute<MoveRange>(new MoveRange(0));
            attrs.Register(owner, a);
            attrs.AddModifier<MoveRange, int>(owner, Mod(1, ModifierOperation.Add, ItemPassiveSourceId.For("botas.ligeras")));
            ServiceLocator.AddService<AttributesManager>(attrs, ServiceScope.Global);
            ServiceLocator.AddService<IInventoryService>(new FakeInventory(botas), ServiceScope.Global);

            MovementRangeAttribution.Resolve(owner, _into);

            Assert.AreEqual(1, _into.Count);
            Assert.AreSame(botas, _into[0].SourceAsset);
            Assert.AreEqual(1, _into[0].Delta);
        }

        private sealed class FakeInventory : IInventoryService
        {
            private readonly List<InventorySlot> _passive = new List<InventorySlot>();
            public FakeInventory(params ItemSO[] items)
            {
                foreach (var i in items) _passive.Add(new InventorySlot { Item = i });
            }
            public IReadOnlyList<InventorySlot> PassiveItems => _passive;
            public IReadOnlyList<InventorySlot> ActiveItems => Array.Empty<InventorySlot>();
            public bool AddItem(ItemSO item) => false;
            public bool RemoveItem(string itemId) => false;
            public bool HasItem(string itemId) => _passive.Exists(s => s.Item != null && s.Item.ItemId == itemId);
            public ItemSO GetItem(string itemId) => _passive.Find(s => s.Item != null && s.Item.ItemId == itemId)?.Item;
            public bool ActivateItem(int activeSlotIndex, EffectContext ctx) => false;
            public ItemActivationBlock CanActivateItem(int activeSlotIndex, EffectContext ctx) => default;
            public int GetComboDamageBonusPreview(string comboId) => 0;
            public void TickCooldowns() { }
            public int MaxActiveSlots => 0;
            public void AddActiveSlotBonus(int amount) { }
            public event Action<ItemSO, bool> OnItemChanged { add { } remove { } }
        }
    }
}
