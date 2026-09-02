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
        public void Read_StreakTwoAtTwoPerStep_ReturnsFour()
        {
            // Arrange — tercer combo distinto seguido del combate.
            _state.VarietyStreak = 2;
            var reader = new ReadComboVarietyStreakScaled { PerStepAmount = 2 };

            // Act + Assert
            Assert.AreEqual(4, reader.Read(new EffectContext()));
        }

        [Test]
        public void Read_StreakZero_ReturnsZero()
        {
            // Arrange — primer combo del combate o repetición.
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
        }
    }
}
