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

        [Test]
        public void CupSlam_ChargesTheFullToll_ToThePlayerStandingNextToHer()
        {
            PlacePlayer(5, 4);
            var node = NewNode();

            var result = node.Tick(NewContext());

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
            PlacePlayer(5, 5);
            var node = NewNode();

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Failed, result);
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void CupSlam_WithTheDefaultMetric_DoesNotReachTheDiagonal()
        {
            // La diagonal es también la casilla desde la que el jugador NO puede pegarle:
            // su Base Attack es Range 1 en Manhattan. El cubilete cubre exactamente lo mismo.
            PlacePlayer(6, 4);
            var node = NewNode();

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Failed, result);
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void CupSlam_WithChebyshev_CoversTheWholeThreeByThree()
        {
            PlacePlayer(6, 4);
            var node = NewNode();
            node.Metric = DistanceMetric.Chebyshev;

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(CupDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void CupSlam_ChargesOnEveryRoll_WhileThePlayerStaysGlued()
        {
            // El cubilete no tiene compás par/impar: quedarse pegado cuesta todos los turnos.
            PlacePlayer(5, 4);
            var node = NewNode();

            for (int round = 1; round <= 3; round++) node.Tick(NewContext());

            Assert.AreEqual(3, _pipeline.Resolved.Count,
                "Tres tiradas pegado a la mesa son tres cubiletes.");
        }

        [Test]
        public void CupSlam_AnnouncesNothing_TheOnlyWarningIsTheDistance()
        {
            PlacePlayer(5, 4);
            var node = NewNode();

            node.Tick(NewContext());

            CollectionAssert.IsEmpty(_threat.SnapshotPending(),
                "El cubilete es melee directo — si marca un área, se convirtió en dos golpes.");
        }

        [Test]
        public void CupSlam_WithoutADamagePipeline_FailsInsteadOfThrowing()
        {
            PlacePlayer(5, 4);
            var context = NewContext();
            context.DamagePipeline = null;

            Assert.AreEqual(AIResult.Failed, NewNode().Tick(context));
        }

        [Test]
        public void CupSlam_WithThePlayerOffTheGrid_Fails()
        {
            // El jugador no está registrado en la grilla (sala sin cargar, entity muerta).
            var node = NewNode();

            Assert.AreEqual(AIResult.Failed, node.Tick(NewContext()));
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

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
