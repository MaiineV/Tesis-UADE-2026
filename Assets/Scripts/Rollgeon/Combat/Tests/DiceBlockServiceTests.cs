using System;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.DiceBlock;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="DiceBlockService"/> (Sistemas prerequisito Bosses §2):
    /// Block / IsBlocked / Unblock / BlockedIndices / Clear, y auto-release al finalizar
    /// el turno del jugador (OnTurnFinished filtrado por el player guid).
    /// </summary>
    [TestFixture]
    public class DiceBlockServiceTests
    {
        private DiceBlockService _svc;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _player = Guid.NewGuid();
            _svc = new DiceBlockService();
            _svc.ConfigureForTests(() => _player);
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            ServiceLocator.Clear();
        }

        [Test]
        public void Block_MarksIndexAsBlocked()
        {
            _svc.Block(2);

            Assert.IsTrue(_svc.IsBlocked(2));
            Assert.IsFalse(_svc.IsBlocked(0));
            Assert.IsTrue(_svc.BlockedIndices.Contains(2));
        }

        [Test]
        public void Block_NegativeIndex_IsNoop()
        {
            _svc.Block(-1);

            Assert.AreEqual(0, _svc.BlockedIndices.Count);
        }

        [Test]
        public void Unblock_RemovesSingleIndex()
        {
            _svc.Block(1);
            _svc.Block(3);

            _svc.Unblock(1);

            Assert.IsFalse(_svc.IsBlocked(1));
            Assert.IsTrue(_svc.IsBlocked(3));
        }

        [Test]
        public void PlayerTurnFinished_AutoReleasesBlocks()
        {
            _svc.Block(0);
            _svc.Block(4);

            EventManager.Trigger(EventName.OnTurnFinished, _player);

            Assert.AreEqual(0, _svc.BlockedIndices.Count, "El turno del jugador libera los bloqueos.");
        }

        [Test]
        public void OtherEntityTurnFinished_DoesNotReleaseBlocks()
        {
            _svc.Block(0);

            EventManager.Trigger(EventName.OnTurnFinished, Guid.NewGuid());

            Assert.IsTrue(_svc.IsBlocked(0), "Solo el fin de turno del jugador libera.");
        }

        [Test]
        public void Clear_RemovesAllBlocks()
        {
            _svc.Block(0);
            _svc.Block(1);

            _svc.Clear();

            Assert.AreEqual(0, _svc.BlockedIndices.Count);
        }
    }
}
