using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// <see cref="IInventoryService.TickCooldowns"/> existía sin un solo caller en
    /// producción: <c>ActivateItem</c> seteaba <c>CurrentCooldown</c> y nunca bajaba, así
    /// que cualquier item con <c>Cooldown &gt; 0</c> quedaba bloqueado para el resto de la
    /// run. Invisible hasta ahora porque la poción tiene cooldown 0.
    /// </summary>
    [TestFixture]
    public sealed class InventoryCooldownTickTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private InventoryService _service;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _playerGuid = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_playerGuid));
            _service = new InventoryService(null, 4);
        }

        [TearDown]
        public void TearDown()
        {
            // El handler vive en el EventManager estático: una suscripción filtrada
            // dispararía en el próximo test.
            _service?.Dispose();
            _service = null;
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            ServiceLocator.Clear();
        }

        [Test]
        public void test_playerTurnFinished_decrementsCooldown()
        {
            // Arrange
            _service.AddItem(NewActive("item.cd", cooldown: 2));
            _service.ActivateItem(0, Ctx());
            Assert.AreEqual(2, _service.ActiveItems[0].CurrentCooldown);

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            Assert.AreEqual(1, _service.ActiveItems[0].CurrentCooldown);
        }

        [Test]
        public void test_enoughPlayerTurns_makeTheItemUsableAgain()
        {
            // Arrange
            _service.AddItem(NewActive("item.cd", cooldown: 2));
            _service.ActivateItem(0, Ctx());

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            Assert.AreEqual(0, _service.ActiveItems[0].CurrentCooldown);
            Assert.AreEqual(ItemActivationBlock.None, _service.CanActivateItem(0, Ctx()),
                "tras cumplir el cooldown el item vuelve a estar disponible");
        }

        [Test]
        public void test_otherEntityTurnFinished_doesNotDecrement()
        {
            // Arrange — el cooldown se mide en turnos propios, no en turnos de mesa.
            _service.AddItem(NewActive("item.cd", cooldown: 2));
            _service.ActivateItem(0, Ctx());

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, Guid.NewGuid());

            // Assert
            Assert.AreEqual(2, _service.ActiveItems[0].CurrentCooldown);
        }

        [Test]
        public void test_cooldownNeverGoesNegative()
        {
            // Arrange
            _service.AddItem(NewActive("item.cd", cooldown: 1));
            _service.ActivateItem(0, Ctx());

            // Act
            for (int i = 0; i < 5; i++) EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            Assert.AreEqual(0, _service.ActiveItems[0].CurrentCooldown);
        }

        [Test]
        public void test_disposedService_stopsTicking()
        {
            // Arrange — sin unsubscribe en Dispose el handler sobreviviría a la run.
            _service.AddItem(NewActive("item.cd", cooldown: 2));
            _service.ActivateItem(0, Ctx());
            var slot = _service.ActiveItems[0];

            // Act
            _service.Dispose();
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            Assert.AreEqual(2, slot.CurrentCooldown);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private EffectContext Ctx()
        {
            return new EffectContext { SourceGuid = _playerGuid, TargetGuid = _playerGuid, lastResult = true };
        }

        private ItemSO NewActive(string id, int cooldown)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Active;
            item.Cooldown = cooldown;
            item.ConsumesAction = false;
            item.ConsumedOnUse = false;
            item.OnActivate = new EffectData();
            _spawned.Add(item);
            return item;
        }

        private sealed class FakePlayerService : IPlayerService
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
