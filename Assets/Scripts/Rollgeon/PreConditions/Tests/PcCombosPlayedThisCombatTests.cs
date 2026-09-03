using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.TurnState;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    /// <summary>
    /// La precondición de Piedra Angular: compara los combos jugados en el combate (incluido
    /// el actual) con Value. Sin servicio veta.
    /// </summary>
    [TestFixture]
    public class PcCombosPlayedThisCombatTests
    {
        private FakeTurnState _state;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _state = new FakeTurnState();
            ServiceLocator.AddService<IPlayerTurnStateService>(_state, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private static PreConditionContext Ctx() => new PreConditionContext { OwnerGuid = Guid.NewGuid() };

        private static PcCombosPlayedThisCombat PiedraAngular()
            => new PcCombosPlayedThisCombat { Comparison = IntComparison.Equal, Value = 1 };

        [Test]
        public void FirstComboOfCombat_Passes()
        {
            _state.History.Add("combo.pair");
            Assert.IsTrue(PiedraAngular().Evaluate(Ctx()));
        }

        [Test]
        public void SecondComboOfCombat_Fails()
        {
            _state.History.Add("combo.pair");
            _state.History.Add("combo.trio");
            Assert.IsFalse(PiedraAngular().Evaluate(Ctx()));
        }

        [Test]
        public void GreaterOrEqual_ComparesCount()
        {
            _state.History.AddRange(new[] { "a", "b", "c" });
            var pc = new PcCombosPlayedThisCombat { Comparison = IntComparison.GreaterOrEqual, Value = 3 };
            Assert.IsTrue(pc.Evaluate(Ctx()));
            pc.Value = 4;
            Assert.IsFalse(pc.Evaluate(Ctx()));
        }

        [Test]
        public void WithoutService_Fails()
        {
            ServiceLocator.Clear();
            Assert.IsFalse(PiedraAngular().Evaluate(Ctx()));
        }

        private sealed class FakeTurnState : IPlayerTurnStateService
        {
            public readonly List<string> History = new();
            public int TilesMovedThisTurn => 0;
            public int CleanTurnStreak => 0;
            public int ComboVarietyStreak => 0;
            public int AttacksPlayedThisCombat => 0;
            public IReadOnlyList<string> ComboHistoryThisCombat => History;
            public int CombosPlayedThisCombat => History.Count;
        }
    }
}
