using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.BossHand;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.Weakness;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests del setup de Fase 2 de La Generala (<see cref="AINode_AdoptWeakness"/>) y del gate que
    /// ramifica el árbol por la mano tirada (<see cref="PcBossHandCombo"/>).
    /// </summary>
    [TestFixture]
    public class GeneralaPhaseTwoTests
    {
        private WeaknessRegistry _weakness;
        private StubComboLog _log;
        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _weakness = new WeaknessRegistry();
            ServiceLocator.AddService<IWeaknessRegistry>(_weakness);

            _log = new StubComboLog();
            ServiceLocator.AddService<IComboLogService>(_log);

            _boss = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ======================================================================
        // AdoptWeakness — "el jefe que aprende tu mano"
        // ======================================================================

        [Test]
        public void AdoptWeakness_PointsTheWeaknessAtThePlayersMostUsedCombo()
        {
            // Arrange — el jugador viene apoyándose en el Full House.
            _log.History = new List<string>
            {
                Rollgeon.Combos.ComboId.FullHouse,
                Rollgeon.Combos.ComboId.Par,
                Rollgeon.Combos.ComboId.FullHouse,
                Rollgeon.Combos.ComboId.FullHouse,
                Rollgeon.Combos.ComboId.Poker,
            };
            var node = new AINode_AdoptWeakness { LogWindow = 8, MultiplierOverride = 1.5f };

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(_weakness.TryGet(_boss, out var data));
            Assert.AreEqual(Rollgeon.Combos.ComboId.FullHouse, data.comboId);
            Assert.AreEqual(1.5f, data.mult, 0.0001f);
        }

        [Test]
        public void AdoptWeakness_IgnoresTheNoComboMarker()
        {
            // Arrange — pegar sin combo es lo más frecuente del log, pero no es una mano elegida.
            _log.History = new List<string>
            {
                _log.NoComboMarker, _log.NoComboMarker, _log.NoComboMarker,
                Rollgeon.Combos.ComboId.Par,
            };
            var node = new AINode_AdoptWeakness();

            // Act
            node.Tick(NewContext());

            // Assert
            Assert.IsTrue(_weakness.TryGet(_boss, out var data));
            Assert.AreEqual(Rollgeon.Combos.ComboId.Par, data.comboId);
        }

        [Test]
        public void AdoptWeakness_OnATie_TakesTheMostRecent()
        {
            // Arrange — uno y uno; el índice 0 es el más reciente.
            _log.History = new List<string>
            {
                Rollgeon.Combos.ComboId.Poker,
                Rollgeon.Combos.ComboId.Par,
            };
            var node = new AINode_AdoptWeakness();

            // Act
            node.Tick(NewContext());

            // Assert
            Assert.IsTrue(_weakness.TryGet(_boss, out var data));
            Assert.AreEqual(Rollgeon.Combos.ComboId.Poker, data.comboId);
        }

        [Test]
        public void AdoptWeakness_WithAnEmptyLog_LeavesTheWeaknessUntouched()
        {
            // Arrange — el jugador todavía no atacó (fase cruzada de un solo golpe grande).
            _weakness.SetWeakness(_boss, Rollgeon.Combos.ComboId.Generala, 1.5f);
            _log.History = new List<string>();
            var node = new AINode_AdoptWeakness();

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result, "Sin log el turno del jefe no debe abortar.");
            Assert.IsTrue(_weakness.TryGet(_boss, out var data));
            Assert.AreEqual(Rollgeon.Combos.ComboId.Generala, data.comboId,
                "Sin nada que adoptar, la debilidad base se mantiene.");
        }

        [Test]
        public void AdoptWeakness_OnlyLooksAtTheLastLogWindowEntries()
        {
            // Arrange — el Par domina el historial viejo, el Póker las últimas 2 entradas.
            _log.History = new List<string>
            {
                Rollgeon.Combos.ComboId.Poker,
                Rollgeon.Combos.ComboId.Poker,
                Rollgeon.Combos.ComboId.Par,
                Rollgeon.Combos.ComboId.Par,
                Rollgeon.Combos.ComboId.Par,
            };
            var node = new AINode_AdoptWeakness { LogWindow = 2 };

            // Act
            node.Tick(NewContext());

            // Assert
            Assert.IsTrue(_weakness.TryGet(_boss, out var data));
            Assert.AreEqual(Rollgeon.Combos.ComboId.Poker, data.comboId);
        }

        // ======================================================================
        // PcBossHandCombo — el gate de las ramas de ataque
        // ======================================================================

        [Test]
        public void PcBossHandCombo_MatchesTheArmedHandCombo()
        {
            // Arrange
            var hands = BossDiceHandService.ResolveOrCreate();
            hands.SetHand(_boss, new[] { 4, 4, 4, 4, 2 }, Rollgeon.Combos.ComboId.Poker, armed: true);
            var pc = new PcBossHandCombo
            {
                Match = PcBossHandCombo.HandMatch.Combo,
                ComboId = Rollgeon.Combos.ComboId.Poker,
            };

            // Act + Assert
            Assert.IsTrue(pc.Evaluate(NewPcContext()));
        }

        [Test]
        public void PcBossHandCombo_DoesNotMatchADifferentCombo()
        {
            // Arrange
            BossDiceHandService.ResolveOrCreate()
                .SetHand(_boss, new[] { 4, 4, 1, 2, 3 }, Rollgeon.Combos.ComboId.Par, armed: true);
            var pc = new PcBossHandCombo
            {
                Match = PcBossHandCombo.HandMatch.Combo,
                ComboId = Rollgeon.Combos.ComboId.Poker,
            };

            // Act + Assert
            Assert.IsFalse(pc.Evaluate(NewPcContext()));
        }

        [Test]
        public void PcBossHandCombo_WithRequireArmed_VetoesTheCalledButNotArmedHand()
        {
            // Arrange — el turno de la ronda extra de aviso: la mano está cantada, no armada.
            BossDiceHandService.ResolveOrCreate()
                .SetHand(_boss, new[] { 6, 6, 6, 6, 6 }, Rollgeon.Combos.ComboId.Generala, armed: false);
            var pc = new PcBossHandCombo
            {
                Match = PcBossHandCombo.HandMatch.Combo,
                ComboId = Rollgeon.Combos.ComboId.Generala,
                RequireArmed = true,
            };

            // Act + Assert
            Assert.IsFalse(pc.Evaluate(NewPcContext()),
                "Con la mano solo cantada, esa ronda no se marca nada.");
        }

        [Test]
        public void PcBossHandCombo_NoCombo_MatchesTheBustHand()
        {
            // Arrange
            BossDiceHandService.ResolveOrCreate()
                .SetHand(_boss, new[] { 1, 2, 4, 6, 3 }, BossDiceHand.NoCombo, armed: true);
            var bust = new PcBossHandCombo { Match = PcBossHandCombo.HandMatch.NoCombo };
            var anyCombo = new PcBossHandCombo { Match = PcBossHandCombo.HandMatch.AnyCombo };

            // Act + Assert
            Assert.IsTrue(bust.Evaluate(NewPcContext()));
            Assert.IsFalse(anyCombo.Evaluate(NewPcContext()));
        }

        [Test]
        public void PcBossHandCombo_WithoutAPublishedHand_Vetoes()
        {
            // Arrange — nadie tiró todavía.
            BossDiceHandService.ResolveOrCreate().ClearAll();
            var pc = new PcBossHandCombo
            {
                Match = PcBossHandCombo.HandMatch.Combo,
                ComboId = Rollgeon.Combos.ComboId.Par,
            };

            // Act + Assert
            Assert.IsFalse(pc.Evaluate(NewPcContext()));
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = Guid.NewGuid(),
            Rng = new System.Random(1),
        };

        private PreConditionContext NewPcContext() => new PreConditionContext { OwnerGuid = _boss };

        private sealed class StubComboLog : IComboLogService
        {
            /// <summary>Índice 0 = el combo más reciente, igual que el servicio real.</summary>
            public List<string> History = new List<string>();

            public string NoComboMarker => "combo.none";

            public void Record(string comboId) => History.Insert(0, comboId ?? NoComboMarker);

            public string LastCombo => History.Count > 0 ? History[0] : null;

            public IReadOnlyList<string> Last(int count)
            {
                if (count <= 0 || History.Count == 0) return Array.Empty<string>();
                int take = count < History.Count ? count : History.Count;
                return History.GetRange(0, take);
            }

            public void Clear() => History.Clear();
        }
    }
}
