using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    // El fragmento se arma acá a mano: el builder vive en el assembly de Editor y que lo autoree
    // así lo afirma AnotadorPhaseWiringTests.
    [TestFixture]
    public class AnotadorAxisAlternationTests
    {
        private const int RoomWidth = 11;
        private const int RoomHeight = 7;

        private const int RowDamage = 30;
        private const int ColumnDamage = 32;
        private const int PencilDamage = 12;
        private const int ParityDivisor = 2;

        private static readonly GridCoord PlayerStart = new GridCoord(5, 3);
        private static readonly GridCoord BossStart = new GridCoord(9, 3);

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private SpyDamagePipeline _pipeline;

        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomWidth, RoomHeight));
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, BossStart);
            _grid.Register(_player, PlayerStart);
        }

        [TearDown]
        public void TearDown()
        {
            // AINode_TelegraphMark crea el GameObject del overlay: sin limpiarlo queda huérfano.
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

        [Test]
        public void OddRound_MarksTheRow()
        {
            Assert.AreEqual(AIResult.Succeeded, MarkAxis(round: 1));

            var area = Pending();
            Assert.AreEqual(RoomWidth, area.Tiles.Count, "La fila del jugador es el ancho de la sala.");
            Assert.AreEqual(RowDamage, area.Damage);
            foreach (var tile in area.Tiles)
                Assert.AreEqual(PlayerStart.Y, tile.Y, "Una casilla marcada quedó fuera de la fila.");
        }

        /// <summary>La columna sale de la paridad de la ronda y de nada más — ni de la fase.</summary>
        [Test]
        public void EvenRound_MarksTheColumn()
        {
            Assert.AreEqual(AIResult.Succeeded, MarkAxis(round: 2));

            var area = Pending();
            Assert.AreEqual(RoomHeight, area.Tiles.Count, "La columna del jugador es el alto de la sala.");
            Assert.AreEqual(ColumnDamage, area.Damage);
            foreach (var tile in area.Tiles)
                Assert.AreEqual(PlayerStart.X, tile.X, "Una casilla marcada quedó fuera de la columna.");
        }

        [Test]
        public void AcrossFourRounds_TheAxisAlternates_AndOnlyOneIsPendingPerTurn()
        {
            var damages = new List<int>();
            var sizes = new List<int>();

            // El jugador no se mueve: lo único que cambia el eje es la paridad de la ronda.
            for (int round = 1; round <= 4; round++)
            {
                Assert.AreEqual(AIResult.Succeeded, MarkAxis(round));
                var area = Pending();
                damages.Add(area.Damage);
                sizes.Add(area.Tiles.Count);
            }

            // El tamaño delata el eje: 11 vs 7.
            CollectionAssert.AreEqual(
                new[] { RowDamage, ColumnDamage, RowDamage, ColumnDamage }, damages);
            CollectionAssert.AreEqual(
                new[] { RoomWidth, RoomHeight, RoomWidth, RoomHeight }, sizes);
        }

        [Test]
        public void TheStepThatDodgesTheRow_DoesNotDodgeTheColumnNextRound()
        {
            MarkAxis(round: 1);
            var row = Pending();
            var sidestep = new GridCoord(PlayerStart.X, PlayerStart.Y + 1);

            Assert.IsFalse(row.Contains(sidestep), "Un paso en Y tiene que salir de la fila marcada.");
            Assert.IsTrue(row.Contains(new GridCoord(PlayerStart.X + 1, PlayerStart.Y)),
                "Un paso en X no saca de la fila.");

            Assert.IsTrue(_grid.Move(_player, sidestep), "Arrange: no se pudo esquivar.");
            MarkAxis(round: 2);

            var column = Pending();
            Assert.IsTrue(column.Contains(new GridCoord(sidestep.X, sidestep.Y + 1)),
                "Repetir el paso en Y deja al jugador dentro de la columna: el eje cambió.");
            Assert.IsFalse(column.Contains(new GridCoord(sidestep.X - 1, sidestep.Y)),
                "Ahora el paso que esquiva es el de X.");
        }

        [Test]
        public void Pencil_HitsOnOddRounds()
        {
            Assert.IsTrue(_grid.Move(_player, new GridCoord(8, 3)), "Arrange: no se pudo acercar.");

            var result = PencilGate().Tick(NewContext(round: 3));

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(PencilDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void Pencil_StaysQuietOnEvenRounds_EvenWithThePlayerGlued()
        {
            Assert.IsTrue(_grid.Move(_player, new GridCoord(8, 3)), "Arrange: no se pudo acercar.");

            var result = PencilGate().Tick(NewContext(round: 4));

            Assert.AreEqual(AIResult.Failed, result,
                "El gate de paridad no pasa: por eso en el árbol va dentro de un Selector[..., Wait].");
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        /// <summary><c>Selector[ If(ronda par) → columna, fila ]</c> — el hijo 6 del Sequence raíz.</summary>
        private static AINode_Selector AxisMark()
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode>
                {
                    new AINode_If
                    {
                        Conditions = new List<BasePreCondition> { EvenRound() },
                        Then = Mark(ThreatShape.Column, ColumnDamage),
                    },
                    Mark(ThreatShape.Row, RowDamage),
                },
            };
        }

        private static AINode_If PencilGate()
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition> { OddRound() },
                Then = new AINode_AnotadorPencil
                {
                    Damage = PencilDamage,
                    Range = 1,
                    Metric = DistanceMetric.Manhattan,
                    Kind = AttackKind.BasicAttack,
                },
            };
        }

        private static AINode_TelegraphMark Mark(ThreatShape shape, int damage) => new AINode_TelegraphMark
        {
            Shape = shape,
            Size = 1,
            Damage = damage,
            Kind = AttackKind.BasicAttack,
        };

        private static PcRoundNumber EvenRound() => new PcRoundNumber
        {
            Mode = PcRoundNumber.CompareMode.Multiple,
            Value = ParityDivisor,
        };

        private static PCComposite OddRound() => new PCComposite
        {
            Mode = CompositeMode.Not,
            Children = new List<BasePreCondition> { EvenRound() },
        };

        private AIResult MarkAxis(int round) => AxisMark().Tick(NewContext(round));

        private ThreatenedArea Pending()
        {
            Assert.IsTrue(_threat.TryConsume(_boss, out var area), "El jefe no dejó ningún eje marcado.");
            return area;
        }

        private AIContext NewContext(int round) => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            DamagePipeline = _pipeline,
            SelfMaxHp = 190,
            RoundIndex = round,
        };

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx) => ctx;
        }
    }
}
