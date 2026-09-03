using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.TurnState;
using Rollgeon.Effects.Readers;

namespace Rollgeon.Effects.Tests
{
    /// <summary>Tests de <see cref="ReadCombosSinceLastCombo"/> (Vértigo).</summary>
    [TestFixture]
    public class ReadCombosSinceLastComboTests
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
        public void Read_NoCombos_ReturnsZero()
        {
            Assert.AreEqual(0, new ReadCombosSinceLastCombo().Read(new EffectContext()));
        }

        [Test]
        public void Read_CountsCombosAfterLastReset_IncludingCurrent()
        {
            // par, trío, escalera, par, trío(actual) → 1 después del último par
            _state.History.AddRange(new[] { "combo.pair", "combo.trio", "combo.ladder", "combo.pair", "combo.trio" });

            Assert.AreEqual(1, new ReadCombosSinceLastCombo { ResetComboId = "combo.pair" }.Read(new EffectContext()));
        }

        [Test]
        public void Read_NoResetInHistory_CountsWholeCombat()
        {
            _state.History.AddRange(new[] { "combo.trio", "combo.ladder", "combo.trio" });

            Assert.AreEqual(3, new ReadCombosSinceLastCombo { ResetComboId = "combo.pair" }.Read(new EffectContext()));
        }

        [Test]
        public void Read_CurrentComboIsTheReset_ReturnsZero()
        {
            _state.History.AddRange(new[] { "combo.trio", "combo.ladder", "combo.pair" });

            Assert.AreEqual(0, new ReadCombosSinceLastCombo { ResetComboId = "combo.pair" }.Read(new EffectContext()));
        }

        [Test]
        public void Read_EmptyResetId_CountsEverything()
        {
            _state.History.AddRange(new[] { "combo.pair", "combo.pair" });

            Assert.AreEqual(2, new ReadCombosSinceLastCombo { ResetComboId = "" }.Read(new EffectContext()));
        }

        [Test]
        public void Read_WithoutService_ReturnsZero()
        {
            ServiceLocator.Clear();
            Assert.AreEqual(0, new ReadCombosSinceLastCombo().Read(new EffectContext()));
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
