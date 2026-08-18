using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Bandida;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// El brazo de La Bandida como golpe melee directo: 12 a quien haya cerrado el turno pegado a la
    /// máquina, sin marca previa y sin área.
    /// </summary>
    /// <remarks>
    /// Lo que fija esta suite es el cambio de naturaleza del ataque. Antes era un
    /// <c>TelegraphMark</c> de 3×3 sobre el jefe: avisaba un turno antes y un paso lo esquivaba
    /// entero. Un test rojo acá casi siempre significa que alguien lo volvió a convertir en área o
    /// en telegraph, y con eso el jefe pierde su único daño garantizado.
    /// </remarks>
    [TestFixture]
    public class BandidaArmMeleeTests
    {
        private const int ArmDamage = 12;

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
            _grid.LoadRoom(NavGraph.Rect(11, 7));
            ServiceLocator.AddService<IGridManager>(_grid);

            // Real, no fake: la prueba de que el brazo no telegrafía es que este servicio queda
            // vacío después del golpe, y para eso tiene que ser el que un TelegraphMark usaría.
            _threat = new ThreatenedAreaService();
            _threat.Register();

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();

            // La máquina está atornillada a la pared: no se mueve en ningún test.
            _grid.Register(_boss, new GridCoord(5, 3));
            _grid.Register(_player, new GridCoord(0, 0));
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ======================================================================
        // Alcance
        // ======================================================================

        [Test]
        public void Arm_HitsForTwelve_WhenThePlayerClosedTheTurnAdjacent()
        {
            // Arrange
            MovePlayer(new GridCoord(5, 2));

            // Act
            var result = Arm().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, _pipeline.Resolved.Count, "El brazo cobra en el acto, no el turno que viene.");
            Assert.AreEqual(ArmDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(_boss, _pipeline.Resolved[0].SourceId,
                "El daño tiene que salir del jefe: es su ataque, no un hazard de sala.");
            Assert.AreEqual(AttackKind.BasicAttack, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void Arm_ReachesDiagonals_LikeTheGateThatSelectsIt()
        {
            // Arrange — la esquina desde la que se llega a un rodillo de la punta.
            MovePlayer(new GridCoord(4, 2));

            // Act
            var result = Arm().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result,
                "Con métrica Chebyshev la diagonal está pegada: si el brazo no llegara ahí, el " +
                "jugador rompería rodillos gratis parado en diagonal.");
            Assert.AreEqual(1, _pipeline.Resolved.Count);
        }

        [Test]
        public void Arm_DoesNothing_WhenThePlayerStayedOneTileOut()
        {
            // Arrange
            MovePlayer(new GridCoord(5, 1));

            // Act
            var result = Arm().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result, "Fuera de alcance el nodo falla y el pool cae al Wait.");
            Assert.IsEmpty(_pipeline.Resolved, "El brazo no puede alcanzar a nadie a dos casillas.");
        }

        [Test]
        public void Arm_MeasuresDistanceItself_EvenWithoutTheGate()
        {
            // Arrange — el nodo corre suelto, como quedaría tras un rewire que se coma el If.
            MovePlayer(new GridCoord(0, 0));

            // Act
            var result = Arm().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result,
                "Sin auto-gate, un rewire distraído convertiría el brazo en un golpe a distancia " +
                "de 12 — justo lo que un jefe atornillado a la pared no puede tener.");
            Assert.IsEmpty(_pipeline.Resolved);
        }

        // ======================================================================
        // Sin marca y sin área
        // ======================================================================

        [Test]
        public void Arm_LeavesNoThreatenedArea_BecauseItIsNotATelegraph()
        {
            // Arrange
            MovePlayer(new GridCoord(5, 2));

            // Act
            Arm().Tick(NewContext());

            // Assert
            Assert.IsFalse(_threat.HasPending(_boss),
                "Si el brazo deja área pendiente volvió a ser un telegraph: avisaría un turno antes " +
                "y un paso lo esquivaría entero.");
            Assert.IsEmpty(_threat.GetPendingTiles(_boss));
        }

        [Test]
        public void Arm_ResolvesWithoutAnyThreatService_SoItCannotDependOnTelegraphState()
        {
            // Arrange — sala sin servicios de amenaza registrados, como una escena sin bootstrap.
            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(_grid);
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);
            MovePlayer(new GridCoord(5, 2));

            // Act
            var result = Arm().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(ArmDamage, _pipeline.Resolved[0].BaseDamage);
        }

        // ======================================================================
        // Bordes
        // ======================================================================

        [Test]
        public void Arm_Fails_WithoutADamagePipeline()
        {
            // Arrange
            MovePlayer(new GridCoord(5, 2));
            var context = NewContext();
            context.DamagePipeline = null;

            // Act
            var result = Arm().Tick(context);

            // Assert
            Assert.AreEqual(AIResult.Failed, result, "Sin pipeline no hay golpe, y no puede explotar.");
        }

        [Test]
        public void Arm_Fails_WhenTheBossIsNotOnTheGrid()
        {
            // Arrange — el jefe murió y algo todavía tickea su árbol.
            MovePlayer(new GridCoord(5, 2));
            _grid.Unregister(_boss);

            // Act
            var result = Arm().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result);
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Arm_WithManhattanMetric_SparesTheDiagonals()
        {
            // Arrange — la métrica es autorable, y cambiarla cambia el alcance real del brazo.
            MovePlayer(new GridCoord(4, 2));
            var node = Arm();
            node.Metric = DistanceMetric.Manhattan;

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result,
                "Con Manhattan la diagonal está a 2: el gate del árbol tiene que compartir métrica " +
                "con el nodo o una de las dos mitades miente.");
        }

        // ======================================================================
        // Harness
        // ======================================================================

        private static AINode_BandidaArm Arm() => new AINode_BandidaArm
        {
            Damage = ArmDamage,
            Range = 1,
            Metric = DistanceMetric.Chebyshev,
        };

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            SelfMaxHp = 140,
            Grid = _grid,
            DamagePipeline = _pipeline,
            Rng = new System.Random(7),
        };

        private void MovePlayer(GridCoord coord) => _grid.Move(_player, coord);

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
