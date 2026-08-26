using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Threat;
using Rollgeon.Economy;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class AINode_TelegraphMarkGoldScaledTests
    {
        private const int RoomSize = 9;

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private FakeEconomyService _economy;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomSize, RoomSize));
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _player = Guid.NewGuid();
            _boss = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(4, 4));
            _grid.Register(_boss, new GridCoord(8, 4));

            _economy = new FakeEconomyService();
            ServiceLocator.AddService<IEconomyService>(_economy);
        }

        [TearDown]
        public void TearDown()
        {
            // AINode_TelegraphMark crea el GameObject del overlay al marcar: sin limpiarlo queda
            // huérfano entre tests.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay)
                && overlay is IDisposable disposable)
            {
                disposable.Dispose();
            }
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static List<CashierGoldTier> FichaTiers() => new List<CashierGoldTier>
        {
            new CashierGoldTier { MinGold = 0,   ColumnSize = 1, Damage = 14 },
            new CashierGoldTier { MinGold = 100, ColumnSize = 3, Damage = 28 },
            new CashierGoldTier { MinGold = 250, ColumnSize = 3, Damage = 35 },
        };

        private AINode_TelegraphMarkGoldScaled NewNode() => new AINode_TelegraphMarkGoldScaled
        {
            Shape = ThreatShape.Column,
            Tiers = FichaTiers(),
        };

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            SelfMaxHp = 190,
            Rng = new System.Random(7),
        };

        private int MarkedWidth()
        {
            var xs = new HashSet<int>();
            foreach (var coord in _threat.GetPendingTiles(_boss)) xs.Add(coord.X);
            return xs.Count;
        }

        private int MarkedDamage()
        {
            Assert.IsTrue(_threat.TryConsume(_boss, out var area), "El jefe no dejó área marcada.");
            return area.Damage;
        }

        [TestCase(0,   1, 14)]
        [TestCase(99,  1, 14)]
        [TestCase(100, 3, 28)]
        [TestCase(249, 3, 28)]
        [TestCase(250, 3, 35)]
        [TestCase(700, 3, 35)]
        public void Tick_MarksColumnScaledByPlayerGold(int gold, int expectedWidth, int expectedDamage)
        {
            _economy.ResetTo(gold);
            var node = NewNode();

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(expectedWidth, MarkedWidth(), $"Ancho de la columna con {gold} de oro.");
            Assert.AreEqual(expectedDamage, MarkedDamage(), $"Daño telegrafiado con {gold} de oro.");
        }

        [Test]
        public void Tick_ColumnIsCenteredOnThePlayer()
        {
            _economy.ResetTo(250);

            NewNode().Tick(NewContext());

            var tiles = _threat.GetPendingTiles(_boss);
            Assert.IsTrue(_grid.TryGetPosition(_player, out var playerCoord));
            foreach (var coord in tiles)
            {
                Assert.LessOrEqual(Mathf.Abs(coord.X - playerCoord.X), 1,
                    "La franja de Size 3 va centrada en el jugador (±1 columna).");
            }
            Assert.AreEqual(RoomSize * 3, tiles.Count, "3 columnas completas de una sala 9×9.");
        }

        [Test]
        public void Tick_RicherPlayer_NeverGetsLessThreat()
        {
            var node = NewNode();
            int previousDamage = 0;

            foreach (var gold in new[] { 0, 100, 250 })
            {
                _economy.ResetTo(gold);
                node.Tick(NewContext());
                int damage = MarkedDamage();

                Assert.GreaterOrEqual(damage, previousDamage,
                    "El escalado por oro tiene que ser monótono: más oro nunca amenaza menos.");
                previousDamage = damage;
            }
            Assert.AreEqual(35, previousDamage, "El escalón rico de la ficha pega 35 (techo de piso 2).");
        }

        [Test]
        public void Tick_ExposesResolvedTier_ForDebugging()
        {
            _economy.ResetTo(120);

            var node = NewNode();
            node.Tick(NewContext());

            Assert.AreEqual(1, node.LastRank);
            Assert.AreEqual(120, node.LastGold);
        }

        [Test]
        public void Tick_WithBribeActive_MarksOneTierLower()
        {
            _economy.ResetTo(250);
            ServiceLocator.AddService<ICashierLedgerService>(new FakeCashierLedgerService { DamageStepDown = 1 });

            NewNode().Tick(NewContext());

            Assert.AreEqual(3, MarkedWidth(), "El soborno baja el daño, no adelgaza la columna del escalón medio.");
            Assert.AreEqual(28, MarkedDamage(), "Con soborno activo, el rico paga como el escalón medio.");
        }

        [Test]
        public void Tick_WithBribeActive_AtCheapestTier_StaysAtCheapest()
        {
            _economy.ResetTo(10);
            ServiceLocator.AddService<ICashierLedgerService>(new FakeCashierLedgerService { DamageStepDown = 1 });

            NewNode().Tick(NewContext());

            Assert.AreEqual(14, MarkedDamage(), "No hay escalón debajo del más barato.");
        }

        [Test]
        public void Tick_BribeStepDownDisabled_IgnoresTheLedger()
        {
            _economy.ResetTo(250);
            ServiceLocator.AddService<ICashierLedgerService>(new FakeCashierLedgerService { DamageStepDown = 1 });

            var node = NewNode();
            node.ApplyBribeStepDown = false;
            node.Tick(NewContext());

            Assert.AreEqual(35, MarkedDamage());
        }

        [Test]
        public void Tick_WithoutEconomyService_FallsBackToCheapestTier_InsteadOfNotAttacking()
        {
            ServiceLocator.RemoveService<IEconomyService>();

            var result = NewNode().Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result,
                "Sin economía el jefe igual amenaza: un combate sin amenaza es peor que un golpe flojo.");
            Assert.AreEqual(14, MarkedDamage());
        }

        [Test]
        public void Tick_WithoutTiers_ReturnsFailed()
        {
            var node = NewNode();
            node.Tiers = new List<CashierGoldTier>();

            Assert.AreEqual(AIResult.Failed, node.Tick(NewContext()));
            Assert.IsFalse(_threat.HasPending(_boss));
        }

        [Test]
        public void Tick_NullContext_ReturnsFailed()
        {
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(null));
        }
    }
}
