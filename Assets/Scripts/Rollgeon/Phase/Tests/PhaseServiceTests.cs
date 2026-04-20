using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Phase;
using UnityEngine;

namespace Rollgeon.Phase.Tests
{
    [TestFixture]
    public class PhaseServiceTests
    {
        private PhaseService _service;
        private PhaseTransitionMatrixSO _matrix;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _matrix = ScriptableObject.CreateInstance<PhaseTransitionMatrixSO>();
            AllowAllTransitions(_matrix);

            _service = new PhaseService();
            _service.ConfigureForTests(_matrix);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            if (_matrix != null) Object.DestroyImmediate(_matrix);
        }

        [Test]
        public void ReplacePhase_ChangesCurrentBase()
        {
            _service.ReplacePhase(GamePhase.Exploration);
            Assert.AreEqual(GamePhase.Exploration, _service.CurrentBase);
        }

        [Test]
        public void ReplacePhase_SamePhase_NoOp()
        {
            _service.ReplacePhase(GamePhase.Exploration);

            var events = new List<object>();
            EventManager.Subscribe(EventName.OnPhaseEnter, args => events.Add(args));

            _service.ReplacePhase(GamePhase.Exploration);
            Assert.IsEmpty(events);
        }

        [Test]
        public void PushOverlay_SetsCurrentOverlay()
        {
            AllowOverlay(_matrix, GamePhase.None, PhaseOverlay.Pause);
            _service.PushOverlay(PhaseOverlay.Pause);
            Assert.AreEqual(PhaseOverlay.Pause, _service.CurrentOverlay);
        }

        [Test]
        public void PopOverlay_RestoresOverlayToNone()
        {
            AllowOverlay(_matrix, GamePhase.None, PhaseOverlay.Pause);
            _service.PushOverlay(PhaseOverlay.Pause);
            _service.PopOverlay();
            Assert.AreEqual(PhaseOverlay.None, _service.CurrentOverlay);
        }

        [Test]
        public void ReplacePhase_WhileOverlayActive_Throws()
        {
            AllowOverlay(_matrix, GamePhase.None, PhaseOverlay.Pause);
            _service.PushOverlay(PhaseOverlay.Pause);

            Assert.Throws<InvalidPhaseTransitionException>(
                () => _service.ReplacePhase(GamePhase.Combat));
        }

        [Test]
        public void PushOverlay_FiresEvent()
        {
            AllowOverlay(_matrix, GamePhase.None, PhaseOverlay.Cutscene);

            PhaseOverlay? received = null;
            EventManager.Subscribe(EventName.OnOverlayPushed, args =>
            {
                received = (PhaseOverlay)args[0];
            });

            _service.PushOverlay(PhaseOverlay.Cutscene);
            Assert.AreEqual(PhaseOverlay.Cutscene, received);
        }

        [Test]
        public void PopOverlay_FiresEvent()
        {
            AllowOverlay(_matrix, GamePhase.None, PhaseOverlay.Pause);
            _service.PushOverlay(PhaseOverlay.Pause);

            PhaseOverlay? received = null;
            EventManager.Subscribe(EventName.OnOverlayPopped, args =>
            {
                received = (PhaseOverlay)args[0];
            });

            _service.PopOverlay();
            Assert.AreEqual(PhaseOverlay.Pause, received);
        }

        [Test]
        public void ReplacePhase_FiresExitAndEnterEvents()
        {
            _service.ReplacePhase(GamePhase.Exploration);

            var sequence = new List<string>();
            EventManager.Subscribe(EventName.OnPhaseExit, args =>
                sequence.Add($"exit:{args[0]}"));
            EventManager.Subscribe(EventName.OnPhaseEnter, args =>
                sequence.Add($"enter:{args[0]}"));

            _service.ReplacePhase(GamePhase.Combat);

            Assert.AreEqual(2, sequence.Count);
            Assert.AreEqual("exit:Exploration", sequence[0]);
            Assert.AreEqual("enter:Combat", sequence[1]);
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static void AllowAllTransitions(PhaseTransitionMatrixSO matrix)
        {
            for (int from = 0; from < 5; from++)
            for (int to = 0; to < 5; to++)
            {
                SetTransition(matrix, (GamePhase)from, (GamePhase)to, true);
            }
        }

        private static void SetTransition(PhaseTransitionMatrixSO matrix, GamePhase from, GamePhase to, bool allowed)
        {
            var field = typeof(PhaseTransitionMatrixSO)
                .GetField("_allowedTransitions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var arr = (bool[,])field.GetValue(matrix);
            arr[(int)from, (int)to] = allowed;
        }

        private static void AllowOverlay(PhaseTransitionMatrixSO matrix, GamePhase phase, PhaseOverlay overlay)
        {
            var field = typeof(PhaseTransitionMatrixSO)
                .GetField("_allowedOverlays", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dict = (Dictionary<GamePhase, List<PhaseOverlay>>)field.GetValue(matrix);
            if (!dict.TryGetValue(phase, out var list))
            {
                list = new List<PhaseOverlay>();
                dict[phase] = list;
            }
            if (!list.Contains(overlay)) list.Add(overlay);
        }
    }
}
