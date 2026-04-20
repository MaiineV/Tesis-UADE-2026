using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Phase;
using UnityEngine;

namespace Rollgeon.Phase.Tests
{
    [TestFixture]
    public class PhaseTransitionMatrixTests
    {
        private PhaseTransitionMatrixSO _matrix;

        [SetUp]
        public void SetUp()
        {
            _matrix = ScriptableObject.CreateInstance<PhaseTransitionMatrixSO>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_matrix != null) Object.DestroyImmediate(_matrix);
        }

        [Test]
        public void CanTransition_AllowedPair_ReturnsTrue()
        {
            SetTransition(GamePhase.None, GamePhase.Exploration, true);
            Assert.IsTrue(_matrix.CanTransition(GamePhase.None, GamePhase.Exploration));
        }

        [Test]
        public void CanTransition_DisallowedPair_ReturnsFalse()
        {
            Assert.IsFalse(_matrix.CanTransition(GamePhase.Exploration, GamePhase.GameOver));
        }

        [Test]
        public void CanPushOverlay_ValidPair_ReturnsTrue()
        {
            SetOverlay(GamePhase.Exploration, PhaseOverlay.Pause);
            Assert.IsTrue(_matrix.CanPushOverlay(GamePhase.Exploration, PhaseOverlay.Pause));
        }

        [Test]
        public void CanPushOverlay_InvalidPair_ReturnsFalse()
        {
            Assert.IsFalse(_matrix.CanPushOverlay(GamePhase.Combat, PhaseOverlay.Craps));
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private void SetTransition(GamePhase from, GamePhase to, bool allowed)
        {
            var field = typeof(PhaseTransitionMatrixSO)
                .GetField("_allowedTransitions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var arr = (bool[,])field.GetValue(_matrix);
            arr[(int)from, (int)to] = allowed;
        }

        private void SetOverlay(GamePhase phase, PhaseOverlay overlay)
        {
            var field = typeof(PhaseTransitionMatrixSO)
                .GetField("_allowedOverlays", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dict = (Dictionary<GamePhase, List<PhaseOverlay>>)field.GetValue(_matrix);
            if (!dict.TryGetValue(phase, out var list))
            {
                list = new List<PhaseOverlay>();
                dict[phase] = list;
            }
            if (!list.Contains(overlay)) list.Add(overlay);
        }
    }
}
