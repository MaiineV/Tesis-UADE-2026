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
    /// Tests de <see cref="AINode_RangedShot"/>: el disparo a distancia genérico del que
    /// <c>AINode_CashierRangedShot</c> (el Cajero) es ahora una thin subclass. Usa un
    /// Damage/Range distintos de la ficha del Cajero (12/4) a propósito, para que un test que
    /// pasara por casualidad con esos números no tape un acople implícito.
    /// </summary>
    [TestFixture]
    public class AINode_RangedShotTests
    {
        private const int RoomWidth = 15;
        private const int RoomHeight = 7;
        private const int ShotDamage = 10;
        private const int ShotRange = 6;

        /// <summary>
        /// Jefe en x=5 de una sala de 15: deja 9 casillas libres a su derecha. El test más ancho
        /// dispara a 8 (<see cref="test_shot_damageAndRangeAreFullyConfigurable_independentOfCashierSheet"/>)
        /// y el de fuera de rango se para a <see cref="ShotRange"/>+1, así que la sala tiene que
        /// llegar hasta x=13 o el jugador cae fuera de la grilla y el test falla por eso en vez de
        /// por el rango.
        /// </summary>
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
            _grid.Register(_player, new GridCoord(BossCoord.X + 1, BossCoord.Y));
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ---- Helpers -----------------------------------------------------

        private static AINode_RangedShot NewNode() => new AINode_RangedShot
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

        // ---- El disparo a distancia > 1 -----------------------------------

        [Test]
        public void test_shot_atRangeGreaterThanOne_dealsConfiguredDamage()
        {
            // Arrange — el jugador a 3, lejos de rango melee pero dentro del alcance configurado.
            PlacePlayerAt(new GridCoord(BossCoord.X + 3, BossCoord.Y));
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
        public void test_shot_atMaxRange_dealsConfiguredDamage()
        {
            // Arrange — el jugador exactamente en el borde del alcance.
            PlacePlayerAt(new GridCoord(BossCoord.X + ShotRange, BossCoord.Y));

            // Act
            var result = NewNode().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(ShotDamage, _pipeline.Resolved[0].BaseDamage);
        }

        // ---- El disparo a rango 1 (melee) ----------------------------------

        [Test]
        public void test_shot_atRangeOne_stillFires()
        {
            // Arrange — el setup ya deja al jugador a distancia 1 (la única desde la que pega melee).
            var node = NewNode();

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result,
                "Un disparo a rango también configurado para cubrir distancia 1 no debe fallar por " +
                "estar 'demasiado cerca' — el nodo no tiene mínimo de rango, sólo máximo.");
            Assert.AreEqual(ShotDamage, _pipeline.Resolved[0].BaseDamage);
        }

        // ---- Fuera de rango -------------------------------------------------

        [Test]
        public void test_shot_outOfRange_failsWithoutDamage()
        {
            // Arrange — un paso más lejos que el alcance configurado.
            PlacePlayerAt(new GridCoord(BossCoord.X + ShotRange + 1, BossCoord.Y));

            // Act
            var result = NewNode().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result,
                "Failed benigno: en el árbol lo absorbe el Selector[disparo, Wait].");
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void test_shot_measuresManhattan_byDefault()
        {
            // Arrange — diagonal (3,3): Chebyshev 3 (dentro) pero Manhattan 6 (fuera de un Range de 5).
            var node = NewNode();
            node.Range = 5;
            PlacePlayerAt(new GridCoord(BossCoord.X + 3, BossCoord.Y + 3));

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result,
                "Con Manhattan, la diagonal cuenta los dos ejes.");
        }

        [Test]
        public void test_shot_withChebyshevMetric_reachesTheDiagonal()
        {
            // Arrange
            var node = NewNode();
            node.Range = 5;
            node.Metric = DistanceMetric.Chebyshev;
            PlacePlayerAt(new GridCoord(BossCoord.X + 3, BossCoord.Y + 3));

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(ShotDamage, _pipeline.Resolved[0].BaseDamage);
        }

        // ---- Independencia del Cajero ---------------------------------------

        [Test]
        public void test_shot_hasNoDependencyOnCashierGoldTierService()
        {
            // Arrange — CashierCounterTollService (u otro servicio del Cajero) nunca se registra
            // acá. Si el nodo genérico dependiera de tiers de oro o de una columna marcada,
            // fallaría al no encontrar ese servicio; en cambio no lo busca en absoluto.
            var node = NewNode();

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result,
                "El disparo genérico no debe requerir ningún servicio propio del Cajero.");
            Assert.AreEqual(ShotDamage, _pipeline.Resolved[0].BaseDamage,
                "El daño sale del campo Damage autorado, no de una tabla de tiers de oro.");
        }

        [Test]
        public void test_shot_damageAndRangeAreFullyConfigurable_independentOfCashierSheet()
        {
            // Arrange — valores explícitamente distintos de la ficha del Cajero (12 / 4), para
            // probar que el nodo no tiene esos números hardcodeados en ningún lado.
            var node = new AINode_RangedShot
            {
                Damage = 25,
                Range = 8,
                Metric = DistanceMetric.Manhattan,
                Kind = AttackKind.BasicAttack,
            };
            PlacePlayerAt(new GridCoord(BossCoord.X + 8, BossCoord.Y));

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(25, _pipeline.Resolved[0].BaseDamage);
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
