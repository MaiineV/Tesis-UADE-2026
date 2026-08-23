using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class AnotadorPencilTests
    {
        private const int RoomWidth = 11;
        private const int RoomHeight = 7;
        private const int PencilDamage = 12;

        private static readonly GridCoord BossTile = new GridCoord(5, 3);

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
            _grid.Register(_boss, BossTile);
            _grid.Register(_player, new GridCoord(0, 0));
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Tick_WithThePlayerAdjacent_HitsForTwelve()
        {
            PlacePlayer(new GridCoord(4, 3));

            var result = Pencil().Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, _pipeline.Resolved.Count, "El lápiz pega una sola vez por turno.");
            Assert.AreEqual(PencilDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_boss, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(AttackKind.BasicAttack, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void Tick_ResolvesTheDamageNow_WithoutWaitingATurn()
        {
            PlacePlayer(new GridCoord(5, 4));

            Pencil().Tick(NewContext());

            CollectionAssert.IsNotEmpty(_pipeline.Resolved,
                "El golpe se resuelve en el mismo tick: si esperara un turno volvería a ser un telegraph.");
        }

        /// <summary><see cref="IThreatenedAreaService"/> guarda UN área pendiente por source guid.</summary>
        [Test]
        public void Tick_MarksNothing_SoTheAxisTelegraphKeepsItsSlot()
        {
            PlacePlayer(new GridCoord(4, 3));
            _threat.Mark(_boss, new List<GridCoord> { new GridCoord(0, 3), new GridCoord(1, 3) },
                damage: 30, kind: AttackKind.BasicAttack);

            Pencil().Tick(NewContext());

            Assert.IsTrue(_threat.TryConsume(_boss, out var pending),
                "El lápiz no puede consumir ni pisar el área marcada del eje.");
            Assert.AreEqual(30, pending.Damage, "El área pendiente sigue siendo la del eje, no la del lápiz.");
            Assert.AreEqual(2, pending.Tiles.Count);
        }

        [Test]
        public void Tick_WithThePlayerTwoTilesAway_FailsWithoutHitting()
        {
            PlacePlayer(new GridCoord(3, 3));

            var result = Pencil().Tick(NewContext());

            Assert.AreEqual(AIResult.Failed, result,
                "Fuera de rango falla: por eso en el árbol va dentro de un Selector[..., Wait].");
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        /// <summary>El rango del jugador es Manhattan: desde la diagonal no llega al jefe.</summary>
        [Test]
        public void Tick_OnTheDiagonal_FailsBecauseTheMetricIsManhattan()
        {
            PlacePlayer(new GridCoord(4, 2));

            Assert.AreEqual(AIResult.Failed, Pencil().Tick(NewContext()));
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Tick_WithChebyshev_TakesTheDiagonalToo()
        {
            PlacePlayer(new GridCoord(4, 2));
            var node = Pencil();
            node.Metric = DistanceMetric.Chebyshev;

            Assert.AreEqual(AIResult.Succeeded, node.Tick(NewContext()));
            Assert.AreEqual(PencilDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void Tick_WithARangeOfTwo_ReachesTwoTiles()
        {
            PlacePlayer(new GridCoord(3, 3));
            var node = Pencil();
            node.Range = 2;

            Assert.AreEqual(AIResult.Succeeded, node.Tick(NewContext()));
            Assert.AreEqual(1, _pipeline.Resolved.Count);
        }

        [Test]
        public void Tick_WithoutADamagePipeline_FailsInsteadOfThrowing()
        {
            PlacePlayer(new GridCoord(4, 3));
            var context = NewContext();
            context.DamagePipeline = null;

            Assert.AreEqual(AIResult.Failed, Pencil().Tick(context));
        }

        [Test]
        public void Tick_WithoutAGrid_FailsInsteadOfThrowing()
        {
            var context = NewContext();
            context.Grid = null;

            Assert.AreEqual(AIResult.Failed, Pencil().Tick(context));
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Tick_WithTheTargetOffTheGrid_FailsInsteadOfHittingNobody()
        {
            _grid.Unregister(_player);

            Assert.AreEqual(AIResult.Failed, Pencil().Tick(NewContext()));
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Tick_WithZeroDamage_DoesNotResolveAnEmptyHit()
        {
            // Un 0 autorado por error no debe disparar feedback ni evento de daño.
            PlacePlayer(new GridCoord(4, 3));
            var node = Pencil();
            node.Damage = 0;

            Assert.AreEqual(AIResult.Failed, node.Tick(NewContext()));
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        private static AINode_AnotadorPencil Pencil() => new AINode_AnotadorPencil
        {
            Damage = PencilDamage,
            Range = 1,
            Metric = DistanceMetric.Manhattan,
            Kind = AttackKind.BasicAttack,
        };

        /// <summary>Mover puede fallar en silencio (casilla ocupada / fuera de sala).</summary>
        private void PlacePlayer(GridCoord coord) =>
            Assert.IsTrue(_grid.Move(_player, coord), $"Arrange: no se pudo poner al jugador en {coord}.");

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            DamagePipeline = _pipeline,
            SelfMaxHp = 190,
            RoundIndex = 1,
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
