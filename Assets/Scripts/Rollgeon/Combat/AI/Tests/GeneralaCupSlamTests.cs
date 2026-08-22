using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Generala;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests del cubilete de La Generala (<see cref="AINode_GeneralaCupSlam"/>): el golpe melee
    /// directo con el que cobra estar pegado a ella cuando tira.
    /// </summary>
    [TestFixture]
    public class GeneralaCupSlamTests
    {
        private const int CupDamage = 18;

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
            _grid.LoadRoom(NavGraph.Rect(11, 7)); // La sala del juego.
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _pipeline = new SpyDamagePipeline();

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, BossTile);
        }

        [TearDown]
        public void TearDown()
        {
            _threat.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ======================================================================
        // Alcance
        // ======================================================================

        [Test]
        public void CupSlam_ChargesTheFullToll_ToThePlayerStandingNextToHer()
        {
            // Arrange
            PlacePlayer(5, 4);
            var node = NewNode();

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(CupDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_boss, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(AttackKind.BasicAttack, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void CupSlam_DoesNotReach_ThePlayerTwoTilesAway()
        {
            // Arrange — a un paso de la mesa, que es la distancia desde la que no se rompe nada.
            PlacePlayer(5, 5);
            var node = NewNode();

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result);
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void CupSlam_WithTheDefaultMetric_DoesNotReachTheDiagonal()
        {
            // Arrange — la diagonal es también la casilla desde la que el jugador NO puede pegarle:
            // su Base Attack es Range 1 en Manhattan. El cubilete cubre exactamente lo mismo.
            PlacePlayer(6, 4);
            var node = NewNode();

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result);
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void CupSlam_WithChebyshev_CoversTheWholeThreeByThree()
        {
            // Arrange — con la métrica Chebyshev el cubilete cubre el 3×3 entero.
            PlacePlayer(6, 4);
            var node = NewNode();
            node.Metric = DistanceMetric.Chebyshev;

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(CupDamage, _pipeline.Resolved[0].BaseDamage);
        }

        // ======================================================================
        // Compás
        // ======================================================================

        [Test]
        public void CupSlam_ChargesOnEveryRoll_WhileThePlayerStaysGlued()
        {
            // Arrange — el cubilete no tiene compás par/impar: quedarse pegado cuesta todos los turnos.
            PlacePlayer(5, 4);
            var node = NewNode();

            // Act
            for (int round = 1; round <= 3; round++) node.Tick(NewContext());

            // Assert
            Assert.AreEqual(3, _pipeline.Resolved.Count,
                "Tres tiradas pegado a la mesa son tres cubiletes.");
        }

        [Test]
        public void CupSlam_AnnouncesNothing_TheOnlyWarningIsTheDistance()
        {
            // Arrange
            PlacePlayer(5, 4);
            var node = NewNode();

            // Act
            node.Tick(NewContext());

            // Assert — el golpe entra en el acto: no deja área pendiente que cobrar el turno que viene.
            CollectionAssert.IsEmpty(_threat.SnapshotPending(),
                "El cubilete es melee directo — si marca un área, se convirtió en dos golpes.");
        }

        // ======================================================================
        // Contexto incompleto
        // ======================================================================

        [Test]
        public void CupSlam_WithoutADamagePipeline_FailsInsteadOfThrowing()
        {
            // Arrange
            PlacePlayer(5, 4);
            var context = NewContext();
            context.DamagePipeline = null;

            // Act + Assert
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(context));
        }

        [Test]
        public void CupSlam_WithThePlayerOffTheGrid_Fails()
        {
            // Arrange — el jugador no está registrado en la grilla (sala sin cargar, entity muerta).
            var node = NewNode();

            // Act + Assert
            Assert.AreEqual(AIResult.Failed, node.Tick(NewContext()));
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private void PlacePlayer(int x, int y) => _grid.Register(_player, new GridCoord(x, y));

        private AINode_GeneralaCupSlam NewNode() => new AINode_GeneralaCupSlam
        {
            Damage = CupDamage,
            Range = 1,
            Metric = DistanceMetric.Manhattan,
            Kind = AttackKind.BasicAttack,
        };

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            DamagePipeline = _pipeline,
            Rng = new System.Random(1),
        };

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx)
            {
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }
        }
    }
}
