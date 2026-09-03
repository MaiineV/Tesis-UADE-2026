using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.TurnState;
using Rollgeon.Effects.Readers;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// El reader de Eco Menguante: x5.0 al primer ataque, -0.1 por ataque ya ejecutado,
    /// piso x1.0. <c>ReadFloat</c> preserva la fracción; <c>Read</c> floorea.
    /// </summary>
    [TestFixture]
    public class ReadAttackDecayMultiplierTests
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

        private static ReadAttackDecayMultiplier Eco()
            => new ReadAttackDecayMultiplier { Start = 5f, DecayPerAttack = 0.1f, Min = 1f };

        [Test]
        public void ReadFloat_NoAttacksYet_ReturnsStart()
        {
            _state.Attacks = 0;
            Assert.AreEqual(5f, Eco().ReadFloat(new EffectContext()), 0.0001f);
        }

        [Test]
        public void ReadFloat_OneAttackPlayed_DecaysByOneStep()
        {
            _state.Attacks = 1;
            Assert.AreEqual(4.9f, Eco().ReadFloat(new EffectContext()), 0.0001f);
        }

        [Test]
        public void ReadFloat_ManyAttacks_ClampsToMin()
        {
            // 5 - 60 × 0.1 = -1 → piso 1.
            _state.Attacks = 60;
            Assert.AreEqual(1f, Eco().ReadFloat(new EffectContext()), 0.0001f);
        }

        [Test]
        public void Read_LegacyIntConsumer_Floors()
        {
            _state.Attacks = 1;
            Assert.AreEqual(4, Eco().Read(new EffectContext()));
        }

        [Test]
        public void ReadFloat_WithoutService_ReturnsStart()
        {
            ServiceLocator.Clear();
            Assert.AreEqual(5f, Eco().ReadFloat(new EffectContext()), 0.0001f);
        }

        private sealed class FakeTurnState : IPlayerTurnStateService
        {
            public int Attacks;
            public int TilesMovedThisTurn => 0;
            public int CleanTurnStreak => 0;
            public int ComboVarietyStreak => 0;
            public int AttacksPlayedThisCombat => Attacks;
            public System.Collections.Generic.IReadOnlyList<string> ComboHistoryThisCombat => System.Array.Empty<string>();
            public int CombosPlayedThisCombat => 0;
        }
    }
}
