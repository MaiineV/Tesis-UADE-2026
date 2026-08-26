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

        [Test]
        public void AdoptWeakness_PointsTheWeaknessAtThePlayersMostUsedCombo()
        {
            _log.History = new List<string>
            {
                Rollgeon.Combos.ComboId.FullHouse,
                Rollgeon.Combos.ComboId.Par,
                Rollgeon.Combos.ComboId.FullHouse,
                Rollgeon.Combos.ComboId.FullHouse,
                Rollgeon.Combos.ComboId.Poker,
            };
            var node = new AINode_AdoptWeakness { LogWindow = 8, MultiplierOverride = 1.5f };

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(_weakness.TryGet(_boss, out var data));
            Assert.AreEqual(Rollgeon.Combos.ComboId.FullHouse, data.comboId);
            Assert.AreEqual(1.5f, data.mult, 0.0001f);
        }

        [Test]
        public void AdoptWeakness_IgnoresTheNoComboMarker()
        {
            // Pegar sin combo es lo más frecuente del log, pero no es una mano elegida.
            _log.History = new List<string>
            {
                _log.NoComboMarker, _log.NoComboMarker, _log.NoComboMarker,
                Rollgeon.Combos.ComboId.Par,
            };
            var node = new AINode_AdoptWeakness();

            node.Tick(NewContext());

            Assert.IsTrue(_weakness.TryGet(_boss, out var data));
            Assert.AreEqual(Rollgeon.Combos.ComboId.Par, data.comboId);
        }

        [Test]
        public void AdoptWeakness_OnATie_TakesTheMostRecent()
        {
            _log.History = new List<string>
            {
                Rollgeon.Combos.ComboId.Poker,
                Rollgeon.Combos.ComboId.Par,
            };
            var node = new AINode_AdoptWeakness();

            node.Tick(NewContext());

            Assert.IsTrue(_weakness.TryGet(_boss, out var data));
            Assert.AreEqual(Rollgeon.Combos.ComboId.Poker, data.comboId);
        }

        [Test]
        public void AdoptWeakness_WithAnEmptyLog_LeavesTheWeaknessUntouched()
        {
            // El jugador todavía no atacó (fase cruzada de un solo golpe grande).
            _weakness.SetWeakness(_boss, Rollgeon.Combos.ComboId.Generala, 1.5f);
            _log.History = new List<string>();
            var node = new AINode_AdoptWeakness();

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result, "Sin log el turno del jefe no debe abortar.");
            Assert.IsTrue(_weakness.TryGet(_boss, out var data));
            Assert.AreEqual(Rollgeon.Combos.ComboId.Generala, data.comboId,
                "Sin nada que adoptar, la debilidad base se mantiene.");
        }

        [Test]
        public void AdoptWeakness_OnlyLooksAtTheLastLogWindowEntries()
        {
            // El Par domina el historial viejo, el Póker las últimas 2 entradas.
            _log.History = new List<string>
            {
                Rollgeon.Combos.ComboId.Poker,
                Rollgeon.Combos.ComboId.Poker,
                Rollgeon.Combos.ComboId.Par,
                Rollgeon.Combos.ComboId.Par,
                Rollgeon.Combos.ComboId.Par,
            };
            var node = new AINode_AdoptWeakness { LogWindow = 2 };

            node.Tick(NewContext());

            Assert.IsTrue(_weakness.TryGet(_boss, out var data));
            Assert.AreEqual(Rollgeon.Combos.ComboId.Poker, data.comboId);
        }

        [Test]
        public void PcBossHandCombo_MatchesTheArmedHandCombo()
        {
            var hands = BossDiceHandService.ResolveOrCreate();
            hands.SetHand(_boss, new[] { 4, 4, 4, 4, 2 }, Rollgeon.Combos.ComboId.Poker, armed: true);
            var pc = new PcBossHandCombo
            {
                Match = PcBossHandCombo.HandMatch.Combo,
                ComboId = Rollgeon.Combos.ComboId.Poker,
            };

            Assert.IsTrue(pc.Evaluate(NewPcContext()));
        }

        [Test]
        public void PcBossHandCombo_DoesNotMatchADifferentCombo()
        {
            BossDiceHandService.ResolveOrCreate()
                .SetHand(_boss, new[] { 4, 4, 1, 2, 3 }, Rollgeon.Combos.ComboId.Par, armed: true);
            var pc = new PcBossHandCombo
            {
                Match = PcBossHandCombo.HandMatch.Combo,
                ComboId = Rollgeon.Combos.ComboId.Poker,
            };

            Assert.IsFalse(pc.Evaluate(NewPcContext()));
        }

        [Test]
        public void PcBossHandCombo_WithRequireArmed_VetoesTheCalledButNotArmedHand()
        {
            // El turno de la ronda extra de aviso: la mano está cantada, no armada.
            BossDiceHandService.ResolveOrCreate()
                .SetHand(_boss, new[] { 6, 6, 6, 6, 6 }, Rollgeon.Combos.ComboId.Generala, armed: false);
            var pc = new PcBossHandCombo
            {
                Match = PcBossHandCombo.HandMatch.Combo,
                ComboId = Rollgeon.Combos.ComboId.Generala,
                RequireArmed = true,
            };

            Assert.IsFalse(pc.Evaluate(NewPcContext()),
                "Con la mano solo cantada, esa ronda no se marca nada.");
        }

        [Test]
        public void PcBossHandCombo_NoCombo_MatchesTheBustHand()
        {
            BossDiceHandService.ResolveOrCreate()
                .SetHand(_boss, new[] { 1, 2, 4, 6, 3 }, BossDiceHand.NoCombo, armed: true);
            var bust = new PcBossHandCombo { Match = PcBossHandCombo.HandMatch.NoCombo };
            var anyCombo = new PcBossHandCombo { Match = PcBossHandCombo.HandMatch.AnyCombo };

            Assert.IsTrue(bust.Evaluate(NewPcContext()));
            Assert.IsFalse(anyCombo.Evaluate(NewPcContext()));
        }

        [Test]
        public void PcBossHandCombo_WithoutAPublishedHand_Vetoes()
        {
            BossDiceHandService.ResolveOrCreate().ClearAll();
            var pc = new PcBossHandCombo
            {
                Match = PcBossHandCombo.HandMatch.Combo,
                ComboId = Rollgeon.Combos.ComboId.Par,
            };

            Assert.IsFalse(pc.Evaluate(NewPcContext()));
        }

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
