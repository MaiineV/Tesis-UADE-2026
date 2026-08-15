using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_CashierRangedShot"/>: el disparo del Cajero, 12 directos a
    /// distancia ≤ 4 sin área y sin telegráfico.
    /// </summary>
    /// <remarks>
    /// Lo que se cuida es la tenaza: pegarle al jefe exige distancia 1, y distancia 1 tiene que
    /// estar dentro del rango del disparo. Si el rango se achicara por debajo de la distancia a la
    /// que el jefe kitea (<c>AINode_KeepDistance.IdealDistance</c> = 4), el jefe se replegaría
    /// fuera de su propio alcance y volvería a ser el que no podía pegar.
    /// </remarks>
    [TestFixture]
    public class AINode_CashierRangedShotTests
    {
        private const int RoomWidth = 11;
        private const int RoomHeight = 7;
        private const int ShotDamage = 12;
        private const int ShotRange = 4;

        /// <summary>Jefe al centro de la sala: deja 5 casillas libres a cada lado en X.</summary>
        private static readonly GridCoord BossCoord = new GridCoord(5, 3);

        private GridManager _grid;
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

            _pipeline = new SpyDamagePipeline();

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, BossCoord);
            _grid.Register(_player, new GridCoord(BossCoord.X + ShotRange, BossCoord.Y));
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ---- Helpers -----------------------------------------------------

        private static AINode_CashierRangedShot NewNode() => new AINode_CashierRangedShot
        {
            Damage = ShotDamage,
            Range = ShotRange,
            Metric = DistanceMetric.Manhattan,
            Kind = AttackKind.BasicAttack,
        };

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            DamagePipeline = _pipeline,
            SelfMaxHp = 190,
        };

        private void PlacePlayerAt(GridCoord coord) =>
            Assert.IsTrue(_grid.Move(_player, coord), $"La casilla {coord} tiene que existir en la sala.");

        // ---- El disparo --------------------------------------------------

        [Test]
        public void test_shot_atMaxRange_dealsTheSheetDamageToThePlayer()
        {
            // Arrange — el jugador está exactamente a 4, la distancia a la que el jefe kitea.
            var node = NewNode();

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, _pipeline.Resolved.Count, "Un disparo por turno.");
            Assert.AreEqual(ShotDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_boss, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(AttackKind.BasicAttack, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void test_shot_atMeleeRange_stillFires_soHittingTheBossAlwaysCosts()
        {
            // Arrange — la única distancia desde la que el jugador puede pegarle.
            PlacePlayerAt(new GridCoord(BossCoord.X, BossCoord.Y + 1));

            // Act
            var result = NewNode().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result,
                "Estar a rango de pegarle tiene que ser estar a rango de él: es toda la presión del jefe.");
            Assert.AreEqual(ShotDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void test_shot_reachesEveryDistanceUpToItsRange(int distance)
        {
            // Arrange
            PlacePlayerAt(new GridCoord(BossCoord.X + distance, BossCoord.Y));

            // Act
            var result = NewNode().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result, $"Distancia {distance} está dentro del alcance.");
            Assert.AreEqual(1, _pipeline.Resolved.Count);
        }

        [Test]
        public void test_shot_outOfRange_failsWithoutDamage()
        {
            // Arrange — un paso más lejos que el alcance.
            PlacePlayerAt(new GridCoord(BossCoord.X + ShotRange + 1, BossCoord.Y));

            // Act
            var result = NewNode().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result,
                "Failed benigno: en el árbol lo absorbe el Selector[disparo, Wait].");
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void test_shot_measuresManhattan_theSameMetricTheKitingUses()
        {
            // Arrange — diagonal (3,3): Chebyshev 3 (dentro) pero Manhattan 6 (fuera).
            PlacePlayerAt(new GridCoord(BossCoord.X + 3, BossCoord.Y + 3));

            // Act
            var result = NewNode().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result,
                "Con Manhattan, la diagonal cuenta los dos ejes — igual que AINode_KeepDistance.");
        }

        [Test]
        public void test_shot_withChebyshevMetric_reachesTheDiagonal()
        {
            // Arrange
            PlacePlayerAt(new GridCoord(BossCoord.X + 3, BossCoord.Y + 3));
            var node = NewNode();
            node.Metric = DistanceMetric.Chebyshev;

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(ShotDamage, _pipeline.Resolved[0].BaseDamage);
        }

        // ---- Degradados ---------------------------------------------------

        [Test]
        public void test_shot_withoutDamagePipeline_failsWithoutThrowing()
        {
            // Arrange
            var context = NewContext();
            context.DamagePipeline = null;

            // Act & Assert
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(context));
        }

        [Test]
        public void test_shot_withZeroDamage_doesNotTouchThePipeline()
        {
            // Arrange — un asset mal autorado no debe generar golpes de 0 en el log de combate.
            var node = NewNode();
            node.Damage = 0;

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result);
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void test_shot_whenThePlayerIsNotOnTheGrid_fails()
        {
            // Arrange
            _grid.Unregister(_player);

            // Act
            var result = NewNode().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result);
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void test_shot_nullContext_fails()
        {
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(null));
        }

        [Test]
        public void test_shot_withoutGrid_fails()
        {
            // Arrange
            var context = NewContext();
            context.Grid = null;

            // Act & Assert
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(context));
        }

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
