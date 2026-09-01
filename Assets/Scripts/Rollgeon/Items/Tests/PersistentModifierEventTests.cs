using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Cubre que los <see cref="PersistentModifierDef"/> de un item pasen por
    /// <see cref="AttributesManager"/> y no por el atributo crudo.
    /// </summary>
    /// <remarks>
    /// Bug de la Coraza Reforzada: el modifier de MaxHealth entraba directo al stack
    /// del atributo, sin <c>OnModifierAdded</c>/<c>OnAttributeChanged</c> — la barra de
    /// vida no se enteraba hasta el próximo daño/heal. Estos tests fijan (a) que los
    /// eventos suenan al agregar Y al quitar, y (b) que quitar el item restaura el
    /// valor modificado.
    /// </remarks>
    public sealed class PersistentModifierEventTests
    {
        readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        InventoryService _service;
        AttributesManager _attrMgr;
        Guid _playerGuid;
        readonly List<(Guid entity, Type attr)> _attributeChanged = new List<(Guid, Type)>();
        EventManager.EventReceiver _onAttributeChanged;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _playerGuid = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_playerGuid));

            _attrMgr = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrMgr);
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<MaxHealth>(new MaxHealth(100));
            _attrMgr.Register(_playerGuid, attrs);

            _attributeChanged.Clear();
            _onAttributeChanged = args =>
            {
                if (args.Length >= 2 && args[0] is Guid g && args[1] is Type t)
                    _attributeChanged.Add((g, t));
            };
            EventManager.Subscribe(EventName.OnAttributeChanged, _onAttributeChanged);

            _service = new InventoryService(null, 4);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.UnSubscribe(EventName.OnAttributeChanged, _onAttributeChanged);
            _service?.Dispose();
            _service = null;
            _attrMgr?.Dispose();
            _attrMgr = null;

            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        ItemSO NewMaxHpItem(int amount)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.coraza.test";
            item.DisplayName = "Coraza (test)";
            item.Type = ItemType.Passive;

            var hook = new PassiveItemHook();
            hook.PersistentModifiers.Add(new PersistentModifierDef
            {
                TargetStat = typeof(MaxHealth),
                Operation = ModifierOperation.Add,
                Amount = amount,
            });
            item.PassiveHooks.Add(hook);

            _spawned.Add(item);
            return item;
        }

        [Test]
        public void AddItem_WithPersistentModifier_RaisesOnAttributeChangedForThatStat()
        {
            // Arrange
            var item = NewMaxHpItem(10);

            // Act
            _service.AddItem(item);

            // Assert
            Assert.That(_attributeChanged, Does.Contain((_playerGuid, typeof(MaxHealth))),
                "el HUD escucha OnAttributeChanged(MaxHealth) — sin el evento la barra queda stale");
            Assert.AreEqual(110, _attrMgr.GetAttributeModifiedValue<MaxHealth, int>(_playerGuid));
        }

        [Test]
        public void RemoveItem_WithPersistentModifier_RestoresModifiedValueAndNotifies()
        {
            // Arrange
            var item = NewMaxHpItem(10);
            _service.AddItem(item);
            _attributeChanged.Clear();

            // Act
            var removed = _service.RemoveItem(item.ItemId);

            // Assert
            Assert.IsTrue(removed);
            Assert.AreEqual(100, _attrMgr.GetAttributeModifiedValue<MaxHealth, int>(_playerGuid),
                "quitar el item tiene que barrer sus modifiers (RemoveAllModifiersBySource)");
            Assert.That(_attributeChanged, Does.Contain((_playerGuid, typeof(MaxHealth))),
                "el remove también refresca el HUD");
        }

        [Test]
        public void Dispose_SweepsPersistentModifiers()
        {
            // Arrange
            _service.AddItem(NewMaxHpItem(10));

            // Act
            _service.Dispose();
            _service = null;

            // Assert
            Assert.AreEqual(100, _attrMgr.GetAttributeModifiedValue<MaxHealth, int>(_playerGuid));
        }

        [Test]
        public void TwoCopies_StackAdditively_AndSweepTogetherOnRemove()
        {
            // Dos copias comparten ItemId ⇒ mismo SourceId determinístico. El GDD pide
            // stacking aditivo; al quitar UNA copia el barrido por source se lleva las
            // dos — comportamiento pineado acá para que el día que se soporten copias
            // independientes se decida a propósito, no por accidente.
            var a = NewMaxHpItem(10);
            var b = NewMaxHpItem(10);

            _service.AddItem(a);
            _service.AddItem(b);
            Assert.AreEqual(120, _attrMgr.GetAttributeModifiedValue<MaxHealth, int>(_playerGuid));

            _service.RemoveItem(a.ItemId);
            Assert.AreEqual(100, _attrMgr.GetAttributeModifiedValue<MaxHealth, int>(_playerGuid));
        }

        sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid guid) { PlayerGuid = guid; }

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
