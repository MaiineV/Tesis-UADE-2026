using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Player;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Tiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// El Croupier siembra bombas que ocupan casillas y recién después marca su cono, así que con
    /// el gate de visión se cegaba solo: dos de sus tres tiempos salían mudos y la pelea quedaba
    /// en puras bombas. Acá se corre el árbol del ASSET —no el del builder, que es lo que los
    /// otros fixtures miran— con un bloqueante pegado al jefe, y al lado los mismos nodos sin el
    /// opt-out para que se vea que el bloqueante bloquea de verdad.
    /// </summary>
    [TestFixture]
    public class CroupierLineOfSightTests
    {
        private const int RoomSide = 11;

        private static readonly GridCoord BossTile = new GridCoord(8, 5);
        private static readonly GridCoord PlayerTile = new GridCoord(1, 5);

        /// <summary>Pegada al jefe y en la línea al jugador: es dónde puede caerle una de sus
        /// propias bombas (<c>ScatteredFree</c>), y desde ahí la sombra le tapa casi todo.</summary>
        private static readonly GridCoord BlockerTile = new GridCoord(7, 5);

        private GridManager _grid;
        private MovementService _movement;
        private ThreatenedAreaService _threat;
        private SpecialTileService _tiles;
        private DiceBlockService _blocks;
        private AttributesManager _attributes;
        private SpyDamagePipeline _pipeline;
        private StubPlayerService _playerService;
        private DiceBagSO _bag;

        private Guid _boss;
        private Guid _player;
        private Guid _blocker;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            // Los pasos que no son el objeto del test (siembra, teleports) resuelven servicios que
            // este fixture no para: sus errores son ruido, no el resultado.
            LogAssert.ignoreFailingMessages = true;

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomSide, RoomSide));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline, ServiceScope.Global);

            ServiceLocator.AddService<IThreatOverlayService>(new SpyThreatOverlay(), ServiceScope.Global);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _blocker = Guid.NewGuid();

            _tiles = new SpecialTileService();
            _tiles.ConfigureForTests(() => _player);
            ServiceLocator.AddService<ISpecialTileService>(_tiles, ServiceScope.Global);

            _bag = ScriptableObject.CreateInstance<DiceBagSO>();
            _bag.hideFlags = HideFlags.HideAndDontSave;
            _bag.Dice = new List<DiceType>
            {
                DiceType.D4, DiceType.D6, DiceType.D8, DiceType.D10, DiceType.D12,
            };
            _playerService = new StubPlayerService { Guid = _player, Bag = _bag };
            ServiceLocator.AddService<IPlayerService>(_playerService, ServiceScope.Global);

            _blocks = new DiceBlockService();
            _blocks.Register();

            _attributes = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attributes);
            var stats = new ModifiableAttributes();
            stats.EnsureInitialized();
            _attributes.Register(_boss, stats);
            stats.SetAttribute<Health>(new Health(CroupierAssetBuilder.MaxHp));

            _grid.Register(_boss, BossTile);
            _grid.Register(_player, PlayerTile);
            _grid.Register(_blocker, BlockerTile);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            _blocks?.Dispose();
            _tiles?.Dispose();
            _threat?.Dispose();
            _attributes?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            if (_bag != null) UnityEngine.Object.DestroyImmediate(_bag);
        }

        // =====================================================================
        // El bloqueante bloquea: los mismos nodos sin el opt-out
        // =====================================================================

        [Test]
        public void AConeThatRespectsSight_LosesTheTilesBehindTheBlocker()
        {
            int withSight = TilesMarkedByACone(ignoreLineOfSight: false);
            _threat.Clear(_boss);
            int ignoringIt = TilesMarkedByACone(ignoreLineOfSight: true);

            Assert.Less(withSight, ignoringIt,
                $"El bloqueante tiene que comerse casillas del cono ({withSight} contra {ignoringIt}): " +
                "eso es lo que le recortaba el fuego al tiempo de quema del turno siguiente.");
        }

        private int TilesMarkedByACone(bool ignoreLineOfSight)
        {
            var cone = new AINode_TelegraphMark
            {
                Shape = ThreatShape.DirectionalCone,
                Size = CroupierAssetBuilder.ConeApexHalfWidth,
                Depth = CroupierAssetBuilder.ConeDepth,
                Damage = 0,
                Kind = AttackKind.Environmental,
                IgnoreLineOfSight = ignoreLineOfSight,
            };

            cone.Tick(NewContext(1));
            return _threat.GetPendingTiles(_boss).Count;
        }

        [Test]
        public void AShotThatRespectsSight_DoesNotFireThroughTheBlocker()
        {
            var shot = new AINode_RangedShot
            {
                Damage = CroupierAssetBuilder.ShotDamage,
                Range = CroupierAssetBuilder.ShotRange,
                Kind = AttackKind.BasicAttack,
            };

            var result = shot.Tick(NewContext(1));

            Assert.AreEqual(AIResult.Failed, result, "El bloqueante tiene que tapar la línea.");
            CollectionAssert.IsEmpty(Hits(AttackKind.BasicAttack), "No debería haber salido el tiro.");
        }

        // =====================================================================
        // El árbol que corre el juego, con el mismo bloqueante
        // =====================================================================

        [Test]
        public void TheCroupiersCone_GetsMarked_EvenWithSomethingInFrontOfHim()
        {
            var root = LiveRoot();

            root.Tick(NewContext(1));

            CollectionAssert.IsNotEmpty(_threat.GetPendingTiles(_boss).ToList(),
                "El tiempo de bombas siembra y después marca el cono: si sus propias bombas se lo " +
                "recortan a cero, el tiempo de quema del turno siguiente no prende nada.");
        }

        [Test]
        public void TheCroupiersShot_Lands_EvenWithSomethingInFrontOfHim()
        {
            var root = LiveRoot();

            root.Tick(NewContext(1));
            root.Tick(NewContext(2));
            root.Tick(NewContext(3));

            var hits = Hits(AttackKind.BasicAttack);
            CollectionAssert.IsNotEmpty(hits, "El tiempo de reparto es el único ataque directo del " +
                                              "ciclo: sin él el jefe pasa el turno en Wait.");
            Assert.AreEqual(CroupierAssetBuilder.ShotDamage, hits[0].BaseDamage,
                "El disparo tiene que salir con su daño de siempre.");
        }

        [Test]
        public void TheCroupiersFlee_Triggers_EvenWithSomethingInFrontOfHim()
        {
            // Pegado al jefe, dentro del radio de fuga y con el bloqueante todavía en el medio.
            Assert.IsTrue(_grid.Move(_player, new GridCoord(BossTile.X - 2, BossTile.Y)),
                "La sala del fixture no llega a la casilla desde la que se mide la fuga.");

            var gate = new PcTargetInRange
            {
                Range = CroupierAssetBuilder.FleeTriggerRange,
                Metric = DistanceMetric.Manhattan,
                IgnoreLineOfSight = true,
            };

            Assert.IsTrue(gate.Evaluate(new PreConditionContext
            {
                OwnerGuid = _boss,
                OpponentGuid = _player,
            }), "Lo que dispara la fuga es tenerlo cerca: detrás de una bomba sigue estando cerca.");
        }

        // =====================================================================

        /// <summary>El árbol como lo carga el juego: copia runtime del asset, no del builder.</summary>
        private static AIDecisionNode LiveRoot()
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(CroupierAssetBuilder.BossAssetPath);
            Assert.IsNotNull(data, $"No se pudo cargar {CroupierAssetBuilder.BossAssetPath}.");

            var root = data.CreateRuntimeAIRoot();
            Assert.IsNotNull(root, "El asset del Croupier no tiene AIRoot.");
            return root;
        }

        private List<DamageContext> Hits(AttackKind kind) =>
            _pipeline.Resolved.Where(c => c.Kind == kind).ToList();

        private AIContext NewContext(int roundIndex) => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            SelfMaxHp = CroupierAssetBuilder.MaxHp,
            Grid = _grid,
            Movement = _movement,
            Attributes = _attributes,
            DamagePipeline = _pipeline,
            PlayerService = _playerService,
            RoundIndex = roundIndex,
            // Fijo: la ruleta de fuga es la única rama al azar de estos turnos y el test no la mide.
            Rng = new FixedRoll(0.99),
        };

        private sealed class FixedRoll : System.Random
        {
            private readonly double _value;
            public FixedRoll(double value) : base(1) { _value = value; }
            public override double NextDouble() => _value;
        }

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

        private sealed class SpyThreatOverlay : IThreatOverlayService
        {
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles) { }
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint) { }
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
                Color? tint = null) { }
            public void Clear(Guid sourceGuid) { }
            public void ClearAll() { }
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid Guid;
            public DiceBagSO Bag;

            public Guid PlayerGuid => Guid;
            public Guid RunId => System.Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => Bag;

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) => Bag = bag;
            public void ClearPlayer() { }

#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}

