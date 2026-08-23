using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Status;
using Rollgeon.UI.HUD.Status;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="TeleportCooldownStatusProvider"/>: espejo de los de veneno —
    /// publica el estado con los turnos restantes solo mientras hay cooldown activo.
    /// </summary>
    [TestFixture]
    public class TeleportCooldownStatusProviderTests
    {
        private TeleportCooldownService _svc;
        private TeleportCooldownStatusProvider _provider;
        private List<StatusIconState> _states;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _player = Guid.NewGuid();

            _svc = new TeleportCooldownService();
            _svc.ConfigureForTests();
            ServiceLocator.AddService<ITeleportCooldownService>(_svc, ServiceScope.Global);

            _provider = new TeleportCooldownStatusProvider(catalog: null);
            _states = new List<StatusIconState>();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void Collect_WithActiveCooldown_PublishesStateWithRemainingTurns()
        {
            _svc.Apply(_player, 2);

            _provider.Collect(_player, _states);

            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(TeleportCooldownStatusProvider.StateId, _states[0].Id);
            Assert.IsTrue(_states[0].Active);
            Assert.AreEqual(2, _states[0].RemainingTurns);
        }

        [Test]
        public void Collect_AfterTick_PublishesDecrementedTurns()
        {
            _svc.Apply(_player, 2);
            EventManager.Trigger(EventName.OnTurnStarted, _player);

            _provider.Collect(_player, _states);

            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(1, _states[0].RemainingTurns);
        }

        [Test]
        public void Collect_WithoutCooldown_PublishesNothing()
        {
            _provider.Collect(_player, _states);

            Assert.AreEqual(0, _states.Count);
        }

        [Test]
        public void Collect_WithoutServiceRegistered_PublishesNothing()
        {
            ServiceLocator.Clear();

            _provider.Collect(_player, _states);

            Assert.AreEqual(0, _states.Count);
        }
    }
}
