using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Rollgeon.UI.ChestReveal.Tests
{
    [TestFixture]
    public class ChestRevealPlayerTests
    {
        /// <summary>Stage fake: registra el orden de beats y captura los onDone.</summary>
        private sealed class RecordingStage : IChestRevealStage
        {
            public readonly List<string> Calls = new List<string>();
            public Action OpenDone, SpinDone, RevealDone, DismissDone;
            public int ForceFinalCount;

            public void PlayOpen(Action onDone) { Calls.Add("open"); OpenDone = onDone; }
            public void PlaySpin(Action onDone) { Calls.Add("spin"); SpinDone = onDone; }
            public void PlayReveal(Action onDone) { Calls.Add("reveal"); RevealDone = onDone; }
            public void WaitDismiss(Action onDone) { Calls.Add("dismiss"); DismissDone = onDone; }
            public void ForceFinalState() { Calls.Add("force"); ForceFinalCount++; }
        }

        private ChestRevealPlayer _player;
        private RecordingStage _stage;
        private int _finishedCount;

        [SetUp]
        public void SetUp()
        {
            _player = new ChestRevealPlayer();
            _stage = new RecordingStage();
            _finishedCount = 0;
        }

        private void Play() => _player.Play(_stage, () => _finishedCount++);

        [Test]
        public void Play_ShouldRunBeatsInOrder_AndFinishExactlyOnce()
        {
            // Act
            Play();
            _stage.OpenDone();
            _stage.SpinDone();
            _stage.RevealDone();
            _stage.DismissDone();

            // Assert
            CollectionAssert.AreEqual(new[] { "open", "spin", "reveal", "dismiss" }, _stage.Calls);
            Assert.AreEqual(1, _finishedCount);
            Assert.IsTrue(_player.Done);
        }

        [Test]
        public void DuplicateOnDone_ShouldAdvanceOnlyOnce()
        {
            // Arrange
            Play();
            var openDone = _stage.OpenDone;

            // Act — el stage invoca dos veces el mismo onDone.
            openDone();
            openDone();

            // Assert — un solo Spin.
            CollectionAssert.AreEqual(new[] { "open", "spin" }, _stage.Calls);
        }

        [Test]
        public void RequestSkipOnce_ShouldOnlySetFastFlag()
        {
            // Arrange
            Play();
            _stage.OpenDone(); // en Spin

            // Act
            _player.RequestSkip();

            // Assert — el stage lee Skip y acelera; el flujo de beats no cambia.
            Assert.AreEqual(ChestRevealPlayer.SkipStage.Fast, _player.Skip);
            CollectionAssert.AreEqual(new[] { "open", "spin" }, _stage.Calls);
        }

        [Test]
        public void RequestSkipTwice_ShouldForceFinalState_AndJumpToDismiss()
        {
            // Arrange
            Play();
            _stage.OpenDone(); // en Spin
            var staleSpinDone = _stage.SpinDone;

            // Act
            _player.RequestSkip(); // Fast
            _player.RequestSkip(); // Jump

            // Assert — ForceFinalState una vez, Reveal salteado, esperando dismiss.
            Assert.AreEqual(1, _stage.ForceFinalCount);
            CollectionAssert.AreEqual(new[] { "open", "spin", "force", "dismiss" }, _stage.Calls);
            Assert.AreEqual(ChestRevealPlayer.RevealBeat.WaitDismiss, _player.Beat);

            // El onDone viejo del spin (tween abortado que igual completa) se ignora.
            staleSpinDone();
            CollectionAssert.AreEqual(new[] { "open", "spin", "force", "dismiss" }, _stage.Calls);
            Assert.AreEqual(0, _finishedCount);
        }

        [Test]
        public void RequestSkip_DuringWaitDismiss_ShouldFinish()
        {
            // Arrange
            Play();
            _stage.OpenDone();
            _stage.SpinDone();
            _stage.RevealDone(); // en WaitDismiss

            // Act
            _player.RequestSkip();

            // Assert
            Assert.IsTrue(_player.Done);
            Assert.AreEqual(1, _finishedCount);
        }

        [Test]
        public void Abort_MidSpin_ShouldNeverInvokeOnFinished()
        {
            // Arrange
            Play();
            _stage.OpenDone(); // en Spin
            var staleSpinDone = _stage.SpinDone;

            // Act
            _player.Abort();
            staleSpinDone(); // callback tardío del tween muerto

            // Assert
            Assert.AreEqual(0, _finishedCount);
            Assert.IsFalse(_player.IsRunning);
            Assert.IsFalse(_player.Done);
            CollectionAssert.AreEqual(new[] { "open", "spin" }, _stage.Calls);
        }

        [Test]
        public void Play_AfterFinish_ShouldRunAgainCleanly()
        {
            // Arrange — primera pasada completa.
            Play();
            _stage.OpenDone();
            _stage.SpinDone();
            _stage.RevealDone();
            _stage.DismissDone();

            // Act — segunda pasada (multi-cofre en cola).
            var secondStage = new RecordingStage();
            _player.Play(secondStage, () => _finishedCount++);
            secondStage.OpenDone();
            secondStage.SpinDone();
            secondStage.RevealDone();
            secondStage.DismissDone();

            // Assert
            Assert.AreEqual(2, _finishedCount);
            Assert.AreEqual(ChestRevealPlayer.SkipStage.None, _player.Skip);
            CollectionAssert.AreEqual(new[] { "open", "spin", "reveal", "dismiss" }, secondStage.Calls);
        }
    }
}
