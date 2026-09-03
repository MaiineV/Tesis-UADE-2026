using System;
using NUnit.Framework;
using Patterns;

namespace Rollgeon.Combat.Status.Tests
{
    /// <summary>
    /// <see cref="SecondWindPhaseCutter"/> sin FSM: se simula <c>OnSecondWindTriggered</c> a
    /// mano y se verifica cuándo pide el corte de la fase enemiga.
    /// </summary>
    [TestFixture]
    public class SecondWindPhaseCutterTests
    {
        private SecondWindPhaseCutter _cutter;
        private Guid _playerId;
        private bool _enemyTurn;
        private int _cutRequests;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            _playerId = Guid.NewGuid();
            _enemyTurn = true;
            _cutRequests = 0;
            _cutter = new SecondWindPhaseCutter(() => _playerId, () => _enemyTurn, () => _cutRequests++);
            _cutter.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            _cutter?.Dispose();
            EventManager.ResetEventDictionary();
        }

        private void FireSecondWind(Guid saved)
            => EventManager.Trigger(EventName.OnSecondWindTriggered, saved, null, 1);

        [Test]
        public void PlayerSaved_DuringEnemyTurn_RequestsTheCut()
        {
            FireSecondWind(_playerId);

            Assert.AreEqual(1, _cutRequests);
            Assert.AreEqual(1, _cutter.CutsRequested);
        }

        [Test]
        public void OtherGuid_IsIgnored()
        {
            FireSecondWind(Guid.NewGuid());
            FireSecondWind(Guid.Empty);

            Assert.AreEqual(0, _cutRequests);
        }

        [Test]
        public void OutsideEnemyTurn_IsIgnored()
        {
            _enemyTurn = false;

            FireSecondWind(_playerId);

            Assert.AreEqual(0, _cutRequests, "en el turno del jugador no hay fase que cortar");
        }

        [Test]
        public void AfterDispose_NothingFires()
        {
            _cutter.Dispose();

            FireSecondWind(_playerId);

            Assert.AreEqual(0, _cutRequests);
        }

        [Test]
        public void Attach_Twice_SubscribesOnce()
        {
            _cutter.Attach();

            FireSecondWind(_playerId);

            Assert.AreEqual(1, _cutRequests);
        }
    }
}
