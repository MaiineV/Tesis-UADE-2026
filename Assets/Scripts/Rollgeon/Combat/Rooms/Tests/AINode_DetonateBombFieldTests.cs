using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.Rooms.Tests
{
    /// <summary>
    /// La mecha y el estallido. Lo que se verifica acá es el plazo en <b>turnos</b>: es la razón de
    /// que esto sea un nodo aparte de <see cref="AINode_BombField"/> y no la segunda mitad de su tick.
    /// </summary>
    [TestFixture]
    public class AINode_DetonateBombFieldTests
    {
        private GridManager _grid;
        private AttributesManager _attributes;
        private AIContext _context;
        private ThreatenedAreaService _threat;
        private SpecialTileService _tiles;
        private SpyDamagePipeline _pipeline;
        private RoomObjectDefinitionSO _bomb;
        private SpecialTileDefinitionSO _fireTile;

        private Guid _boss;
        private Guid _player;

        private const string Prefix = "test.bomb.";

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(15, 15));

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, new GridCoord(0, 0));
            _grid.Register(_player, new GridCoord(14, 14));

            _attributes = new AttributesManager();

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _pipeline = new SpyDamagePipeline();

            _tiles = new SpecialTileService();
            _tiles.ConfigureForTests(() => _player);
            ServiceLocator.AddService<ISpecialTileService>(_tiles, ServiceScope.Global);
            ServiceLocator.AddService<IThreatOverlayService>(new SpyThreatOverlay(), ServiceScope.Global);

            _bomb = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            _bomb.hideFlags = HideFlags.HideAndDontSave;
            _bomb.Hp = 1;
            _bomb.Blocks = true;
            _bomb.RespawnDelayTurns = 0;

            _fireTile = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            _fireTile.hideFlags = HideFlags.HideAndDontSave;
            _fireTile.TileId = "TILE_TEST_BOMBFIRE";
            _fireTile.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            _fireTile.DefaultDurationRounds = 3;

            _context = new AIContext
            {
                SelfGuid = _boss,
                PlayerGuid = _player,
                Grid = _grid,
                Attributes = _attributes,
                DamagePipeline = _pipeline,
                Rng = new System.Random(1234),
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (ServiceLocator.TryGetService<BombFieldService>(out var field)) field?.Dispose();
            if (ServiceLocator.TryGetService<RoomObjectCleanupService>(out var cleanup)) cleanup?.Dispose();

            _tiles?.Dispose();
            _threat?.Dispose();
            _attributes?.Dispose();
            if (_bomb != null) UnityEngine.Object.DestroyImmediate(_bomb);
            if (_fireTile != null) UnityEngine.Object.DestroyImmediate(_fireTile);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private AINode_BombField Sower(int count, int fuse) => new AINode_BombField
        {
            Definition = _bomb,
            Count = count,
            Spacing = 3,
            FuseTurns = fuse,
            IgnitionDamage = 20,
            ChannelPrefix = Prefix,
        };

        private AINode_DetonateBombField Detonator(int ignitionDamage = 20) => new AINode_DetonateBombField
        {
            FireTile = _fireTile,
            FireDurationRounds = 2,
            IgnitionDamage = ignitionDamage,
            ChannelPrefix = Prefix,
        };

        private List<SpecialTileInfo> Instances() => new List<SpecialTileInfo>(_tiles.ActiveInstances());

        private List<(Guid Guid, IReadOnlyList<GridCoord> Cross)> Live() =>
            AINode_BombField.LiveCrosses(_attributes).ToList();

        /// <summary>
        /// La bomba que estalló ya se fue por su cuenta. Quien la desanotaba era el CollectBroken del
        /// nodo que siembra, que sólo tickea cuando le toca su tiempo del ciclo: hasta entonces el
        /// barrido de fin de combate arrastraba guids de bombas que ya no existen.
        /// </summary>
        [Test]
        public void ADetonatedBomb_DropsOffTheEndOfFightSweep_OnTheSpot()
        {
            Sower(count: 4, fuse: 1).Tick(_context);
            var cleanup = RoomObjectCleanupService.ResolveOrCreate();
            var sown = Live().Select(b => b.Guid).ToList();
            CollectionAssert.IsSubsetOf(sown, cleanup.Tracked,
                "Precondición: las sembradas tienen que estar anotadas en el barrido.");

            Detonator().Tick(_context);

            Assert.IsEmpty(Live(), "Precondición: con mecha 1 tienen que haber estallado.");
            foreach (var guid in sown)
                CollectionAssert.DoesNotContain(cleanup.Tracked, guid,
                    "La bomba estallada siguió anotada en el barrido de fin de combate.");
        }

        /// <summary>El plazo se mide en turnos del jefe, no en ciclos: es todo el punto del nodo.</summary>
        [Test]
        public void WithAFuseOfTwo_ItBlowsOnTheSecondTurn_NotTheFirst()
        {
            Sower(count: 4, fuse: 2).Tick(_context);
            var sown = Live();
            Assert.AreEqual(4, sown.Count, "Precondición: tenían que quedar cuatro sembradas.");

            var detonator = Detonator();

            detonator.Tick(_context);
            Assert.AreEqual(4, Live().Count,
                "Estallaron al primer turno: con mecha 2 el jugador tiene que llegar a tener dos " +
                "acciones antes de que le prenda el paño.");
            Assert.IsEmpty(Instances(), "No puede haber fuego todavía.");

            detonator.Tick(_context);

            Assert.IsEmpty(Live(), "Al segundo turno tienen que haber estallado las cuatro.");
            var burning = new HashSet<GridCoord>(Instances().SelectMany(i => i.Coords));
            foreach (var (_, cross) in sown)
                foreach (var coord in cross)
                    Assert.IsTrue(burning.Contains(coord), $"{coord} era parte de una cruz y no ardió.");
        }

        [Test]
        public void ABlownBomb_LeavesTheGridAndTheTurnQueue()
        {
            Sower(count: 2, fuse: 1).Tick(_context);
            var sown = Live();

            Detonator().Tick(_context);

            foreach (var (guid, _) in sown)
            {
                Assert.IsFalse(_grid.TryGetPosition(guid, out _), "La que estalló sigue en el grid.");
                var health = _attributes.GetAttribute<Health>(guid);
                Assert.AreEqual(0, health?.Value ?? 0,
                    "Sin bajarle la vida a 0 el spawner no nota la baja y la ranura no se resiembra.");
            }
        }

        /// <summary>Romperla a mano no deja fuego: el fuego es el premio por haberla dejado madurar.</summary>
        [Test]
        public void ABombBrokenByHand_LiftsItsCrossWithoutLeavingFire()
        {
            Sower(count: 4, fuse: 2).Tick(_context);
            var sown = Live();
            var broken = sown[0];
            var survivors = sown.Skip(1).ToList();

            _attributes.SetAttributeValue<Health, int>(broken.Guid, 0);

            var detonator = Detonator();
            detonator.Tick(_context);
            detonator.Tick(_context);

            var burning = new HashSet<GridCoord>(Instances().SelectMany(i => i.Coords));

            foreach (var coord in broken.Cross)
                Assert.IsFalse(burning.Contains(coord),
                    $"{coord} pertenecía a la bomba rota a mano — romperla tiene que ser gratis.");

            foreach (var survivor in survivors)
                foreach (var coord in survivor.Cross)
                    Assert.IsTrue(burning.Contains(coord),
                        $"{coord} era de una que llegó al plazo y no ardió.");
        }

        [Test]
        public void TheIgnitionCharges_OnlyWhenThePlayerIsStandingInTheCross()
        {
            Sower(count: 1, fuse: 1).Tick(_context);
            var bomb = Live().Single();
            _grid.TryGetPosition(bomb.Guid, out var bombCoord);

            // No la casilla de la bomba: la bomba bloquea, asi que ahi el jugador no entra. El brazo
            // de la cruz es justo el caso que importa — estar al lado y no encima.
            var arm = bomb.Cross.First(c => !c.Equals(bombCoord));
            Assert.IsTrue(_grid.Move(_player, arm), "Precondición: el jugador no pudo pararse en la cruz.");

            Detonator(ignitionDamage: 20).Tick(_context);

            Assert.AreEqual(1, _pipeline.Resolved.Count, "El estallido no cobró al que estaba adentro.");
            Assert.AreEqual(20, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(AttackKind.Environmental, _pipeline.Resolved[0].Kind,
                "El estallido es daño de terreno, no un golpe del jefe: con otro Kind entra por la " +
                "mitigación que no le corresponde.");
        }

        [Test]
        public void WithNothingArmed_ItIsANoOp()
        {
            var result = Detonator().Tick(_context);

            Assert.AreEqual(AIResult.Succeeded, result,
                "Failed acá cortaría el turno del jefe en cada turno sin bombas en pie.");
            Assert.IsEmpty(Instances());
        }

        /// <summary>El beat es lo que separa el fuego de lo que el jefe haga después en el turno.</summary>
        [Test]
        public void TheBlastBeat_PlaysOnlyOnTheTurnSomethingActuallyBlows()
        {
            var feedback = new SpyFeedback();
            ServiceLocator.AddService<IFeedbackService>(feedback, ServiceScope.Global);

            Sower(count: 2, fuse: 2).Tick(_context);

            var detonator = Detonator();
            detonator.DetonationVfxId = "vfx.test.blast";

            Drain(detonator);
            Assert.AreEqual(0, feedback.Requests,
                "Pidió el beat en un turno en que sólo descontó la mecha: el jefe detonando al aire.");

            Drain(detonator);
            Assert.AreEqual(1, feedback.Requests, "El estallido no pidió su beat.");

            Drain(detonator);
            Assert.AreEqual(1, feedback.Requests,
                "Volvió a pedir el beat sin nada que estallar.");
        }

        [Test]
        public void WithoutAFireTile_ItWarnsAndStillClearsTheBomb()
        {
            Sower(count: 1, fuse: 1).Tick(_context);
            var guid = Live().Single().Guid;

            var detonator = Detonator();
            detonator.FireTile = null;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("FireTile"));
            detonator.Tick(_context);

            Assert.IsEmpty(Instances(), "Sin casilla de fuego no puede quedar fuego.");
            Assert.IsFalse(_grid.TryGetPosition(guid, out _),
                "La bomba tiene que salir del paño igual: si no, queda una bomba muerta bloqueando.");
        }

        /// <summary>
        /// El arbol que corre en combate NO es el del asset: <c>EnemyDataSO.CreateRuntimeAIRoot</c>
        /// pasa por <c>SerializationUtility.CreateCopy</c>, y Odin <b>no corre field initializers</b>.
        /// Un nodo que dependa de un campo inicializado en la declaracion revienta con un
        /// NullReferenceException en el primer turno del jefe, y ningun test que lo construya con
        /// <c>new</c> lo ve.
        /// </summary>
        [Test]
        public void AfterTheRuntimeCopy_ItStillTicks()
        {
            Sower(count: 2, fuse: 1).Tick(_context);

            var copy = SerializationUtility.CreateCopy(Detonator()) as AINode_DetonateBombField;
            Assert.IsNotNull(copy, "La copia de runtime no devolvio el nodo.");

            AIResult result = default;
            Assert.DoesNotThrow(() => result = copy.Tick(_context),
                "El nodo revento sobre la copia de runtime: hay un campo que depende de un field " +
                "initializer que Odin no corre.");
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsNotEmpty(Instances(), "La copia tickeo sin estallar nada.");
        }

        private void Drain(AINode_DetonateBombField node)
        {
            var routine = node.TickCoroutine(_context, null);
            while (routine.MoveNext()) { }
        }

        private sealed class SpyFeedback : IFeedbackService
        {
            public int Requests;

            public void RequestFeedbackBlocking(FeedbackRequest request, Action onComplete)
            {
                Requests++;

                // El contrato de la interfaz: onComplete se invoca exactamente una vez, incluso con
                // un id inválido.
                onComplete?.Invoke();
            }
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
    }
}
