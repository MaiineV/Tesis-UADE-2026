using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.TurnState;
using Rollgeon.Effects.Readers;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// El reader de Mosaico Errático: racha de combos distintos × PerStepAmount.
    /// </summary>
    [TestFixture]
    public class ReadComboVarietyStreakScaledTests
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

        [Test]
        public void Read_StreakTwoAtTwoPerStep_ReturnsSix()
        {
            // Arrange — tercer combo distinto seguido del combate: cadena de 3 → 3 × 2.
            _state.VarietyStreak = 2;
            var reader = new ReadComboVarietyStreakScaled { PerStepAmount = 2 };

            // Act + Assert
            Assert.AreEqual(6, reader.Read(new EffectContext()));
        }

        [Test]
        public void Read_StreakOne_PaysTheWholeChain()
        {
            // Arrange — segundo combo distinto: cadena de 2 → 2 × 2 (decisión GD 2026-09-03).
            _state.VarietyStreak = 1;
            var reader = new ReadComboVarietyStreakScaled { PerStepAmount = 2 };

            // Act + Assert
            Assert.AreEqual(4, reader.Read(new EffectContext()));
        }

        [Test]
        public void Read_GddExample_FourthDistinctComboPaysEight()
        {
            // doble par → par → trío → doble par: racha 3 → cadena de 4 → 8.
            _state.VarietyStreak = 3;
            var reader = new ReadComboVarietyStreakScaled { PerStepAmount = 2 };

            Assert.AreEqual(8, reader.Read(new EffectContext()));
        }

        [Test]
        public void Read_StreakZero_ReturnsZero()
        {
            // Arrange — primer combo del combate o el que rompe la racha (cadena de 1).
            _state.VarietyStreak = 0;
            var reader = new ReadComboVarietyStreakScaled { PerStepAmount = 2 };

            // Act + Assert
            Assert.AreEqual(0, reader.Read(new EffectContext()));
        }

        [Test]
        public void Read_WithoutService_ReturnsZero()
        {
            // Arrange
            ServiceLocator.Clear();
            var reader = new ReadComboVarietyStreakScaled { PerStepAmount = 2 };

            // Act + Assert
            Assert.AreEqual(0, reader.Read(new EffectContext()));
        }

        private sealed class FakeTurnState : IPlayerTurnStateService
        {
            public int VarietyStreak;
            public int TilesMovedThisTurn => 0;
            public int CleanTurnStreak => 0;
            public int ComboVarietyStreak => VarietyStreak;
            public int AttacksPlayedThisCombat => 0;
            public System.Collections.Generic.IReadOnlyList<string> ComboHistoryThisCombat => System.Array.Empty<string>();
            public int CombosPlayedThisCombat => 0;
        }
    }
}
