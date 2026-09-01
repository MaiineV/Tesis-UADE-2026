using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.TurnState;
using Rollgeon.Effects.Readers;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// El reader de Furia Contenida: <see cref="ReadCleanTurnStreakScaled.ReadFloat"/>
    /// preserva la fracción (0.25/ronda del GDD) — el redondeo pasa una sola vez al
    /// final de la fórmula N×M. <c>Read</c> (consumidores int legacy) floorea.
    /// </summary>
    [TestFixture]
    public class ReadCleanTurnStreakScaledTests
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
        public void ReadFloat_StreakTwoAtQuarter_ReturnsHalf()
        {
            // Arrange — 2 rondas limpias × 0.25.
            _state.Streak = 2;
            var reader = new ReadCleanTurnStreakScaled { PerTurnAmount = 0.25f };

            // Act + Assert — 0.5 sin floor: exactamente el bug de QA ("los 0.25 no suman").
            Assert.AreEqual(0.5f, reader.ReadFloat(new EffectContext()), 0.0001f);
        }

        [Test]
        public void Read_LegacyIntConsumer_Floors()
        {
            // Arrange
            _state.Streak = 3;
            var reader = new ReadCleanTurnStreakScaled { PerTurnAmount = 0.25f };

            // Act + Assert — 0.75 → 0 para consumidores int (contrato viejo intacto).
            Assert.AreEqual(0, reader.Read(new EffectContext()));
        }

        [Test]
        public void ReadFloat_WithoutService_ReturnsZero()
        {
            // Arrange
            ServiceLocator.Clear();
            var reader = new ReadCleanTurnStreakScaled { PerTurnAmount = 0.25f };

            // Act + Assert
            Assert.AreEqual(0f, reader.ReadFloat(new EffectContext()), 0.0001f);
        }

        private sealed class FakeTurnState : IPlayerTurnStateService
        {
            public int Streak;
            public int TilesMovedThisTurn => 0;
            public int CleanTurnStreak => Streak;
        }
    }
}
