using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Dice;
using Rollgeon.Player;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_AuxTelegraph"/> — el canal secundario de telegraph. Lo que tiene
    /// que probar: que <b>no le pisa la marca al telegraph principal</b> (el bug que existiría si el
    /// cubilete de La Generala usara el <see cref="AINode_TelegraphMark"/> de siempre, porque
    /// <see cref="IThreatenedAreaService"/> guarda una sola marca por fuente).
    /// </summary>
    [TestFixture]
    public class AINode_AuxTelegraphTests
    {
        private const string Channel = "cubilete";

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

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, new GridCoord(5, 3));
            _grid.Register(_player, new GridCoord(8, 3));
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService { PlayerGuid = _player });
        }

        [TearDown]
        public void TearDown()
        {
            _threat.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Mark_MarksAroundSelf_UnderItsOwnChannelId()
        {
            // Arrange
            var node = NewMark(damage: 12);

            // Act
            var result = node.Tick(NewContext());

            // Assert — 3×3 centrado en el boss, guardado con el guid del canal (no el del boss).
            Assert.AreEqual(AIResult.Succeeded, result);
            var channel = AINode_AuxTelegraph.ChannelGuid(_boss, Channel);
            var tiles = _threat.GetPendingTiles(channel);
            Assert.AreEqual(9, tiles.Count, "SquareAroundSelf con Size 1 son 9 casillas.");
            Assert.Contains(new GridCoord(5, 3), new List<GridCoord>(tiles));
            Assert.Contains(new GridCoord(4, 2), new List<GridCoord>(tiles));
        }

        [Test]
        public void Mark_DoesNotOverwriteTheBossMainTelegraph()
        {
            // Arrange — el jefe marca su mano por el canal principal...
            new AINode_TelegraphMark
            {
                Shape = ThreatShape.Row,
                Size = 1,
                Damage = 45,
            }.Tick(NewContext());
            Assert.IsTrue(_threat.HasPending(_boss), "Precondición: la mano quedó marcada.");

            // Act — ...y después baja el cubilete.
            NewMark(damage: 12).Tick(NewContext());

            // Assert — las dos marcas coexisten con su propio daño.
            Assert.AreEqual(2, _threat.SnapshotPending().Count,
                "El cubilete no puede reemplazar la marca de la mano — son dos avisos distintos.");
            Assert.IsTrue(_threat.TryConsume(_boss, out var main));
            Assert.AreEqual(45, main.Damage);
            Assert.IsTrue(_threat.TryConsume(AINode_AuxTelegraph.ChannelGuid(_boss, Channel), out var cup));
            Assert.AreEqual(12, cup.Damage);
        }

        [Test]
        public void Execute_ChargesTheChannelMark_WhenThePlayerStayedInside()
        {
            // Arrange — el jugador se para dentro del anillo del cubilete.
            NewMark(damage: 12).Tick(NewContext());
            _grid.Move(_player, new GridCoord(5, 4));

            // Act
            var result = NewExecute().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, _pipeline.Resolved.Count, "El peaje del cubilete tenía que cobrarse.");
            Assert.AreEqual(12, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
        }

        [Test]
        public void Execute_ChargesNothing_WhenThePlayerLeftTheRing()
        {
            // Arrange — el jugador se aleja de la mesa antes de que baje el cubilete.
            NewMark(damage: 12).Tick(NewContext());
            _grid.Move(_player, new GridCoord(9, 6));

            // Act
            NewExecute().Tick(NewContext());

            // Assert
            Assert.IsEmpty(_pipeline.Resolved, "Salirse del anillo tiene que esquivar el peaje.");
        }

        [Test]
        public void Execute_WithNothingPending_Succeeds_SoTheTurnKeepsGoing()
        {
            // Act + Assert — el primer turno no tiene aviso previo que cobrar.
            Assert.AreEqual(AIResult.Succeeded, NewExecute().Tick(NewContext()));
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void ChannelGuid_IsStablePerBossAndChannel_AndNeverEqualsTheBossItself()
        {
            // Act
            var first = AINode_AuxTelegraph.ChannelGuid(_boss, Channel);
            var again = AINode_AuxTelegraph.ChannelGuid(_boss, Channel);
            var otherChannel = AINode_AuxTelegraph.ChannelGuid(_boss, "lapiz");
            var otherBoss = AINode_AuxTelegraph.ChannelGuid(Guid.NewGuid(), Channel);

            // Assert
            Assert.AreEqual(first, again, "El mismo canal tiene que resolver al mismo id todos los turnos.");
            Assert.AreNotEqual(first, _boss, "El canal secundario nunca puede coincidir con el principal.");
            Assert.AreNotEqual(first, otherChannel);
            Assert.AreNotEqual(first, otherBoss);
        }

        [Test]
        public void Mark_WithUnsupportedShape_Fails_WithoutMarkingAnything()
        {
            // Arrange — las shapes con anclaje propio (Scattered/DirectionalBand) no van por acá.
            var node = NewMark(damage: 12);
            node.Shape = ThreatShape.ScatteredSquares;
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("AINode_AuxTelegraph.*no soportada"));

            // Act
            var result = node.Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result);
            Assert.IsEmpty(_threat.SnapshotPending());
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static AINode_AuxTelegraph NewMark(int damage) => new AINode_AuxTelegraph
        {
            Step = AINode_AuxTelegraph.TelegraphStep.Mark,
            ChannelId = Channel,
            Shape = ThreatShape.SquareAroundSelf,
            Size = 1,
            Damage = damage,
            Kind = AttackKind.BasicAttack,
        };

        private static AINode_AuxTelegraph NewExecute() => new AINode_AuxTelegraph
        {
            Step = AINode_AuxTelegraph.TelegraphStep.Execute,
            ChannelId = Channel,
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

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }
    }
}
