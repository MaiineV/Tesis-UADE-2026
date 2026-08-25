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
using UnityEngine;

namespace Rollgeon.Combat.Rooms.Tests
{
    /// <summary>
    /// La siembra: cuántas bombas caen, dónde, con qué cruz avisada y con qué mecha. Quien las hace
    /// estallar es <see cref="AINode_DetonateBombField"/>, y tiene sus propios tests.
    /// </summary>
    [TestFixture]
    public class AINode_BombFieldTests
    {
        private GridManager _grid;
        private AttributesManager _attributes;
        private AIContext _context;
        private ThreatenedAreaService _threat;
        private SpecialTileService _tiles;
        private SpyDamagePipeline _pipeline;
        private RoomObjectDefinitionSO _bomb;

        private Guid _boss;
        private Guid _player;

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

            _tiles?.Dispose();
            _threat?.Dispose();
            _attributes?.Dispose();
            if (_bomb != null) UnityEngine.Object.DestroyImmediate(_bomb);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private AINode_BombField MakeNode(int count, int spacing = 3, int fuse = 2) => new AINode_BombField
        {
            Definition = _bomb,
            Count = count,
            Spacing = spacing,
            FuseTurns = fuse,
            IgnitionDamage = 20,
        };

        private List<SpecialTileInfo> Instances() => new List<SpecialTileInfo>(_tiles.ActiveInstances());

        private List<(Guid Guid, IReadOnlyList<GridCoord> Cross)> Live() =>
            AINode_BombField.LiveCrosses(_attributes).ToList();

        [Test]
        public void Sowing_SeedsCountBombs_MarksTheirCrosses_AndIgnitesNothing()
        {
            var node = MakeNode(count: 4);

            var result = node.Tick(_context);

            Assert.AreEqual(AIResult.Succeeded, result,
                "Failed acá abortaría la Sequence del jefe en su turno de siembra.");

            var crosses = Live();
            Assert.AreEqual(4, crosses.Count, "Tienen que quedar armadas las 4 bombas sembradas.");

            foreach (var (guid, cross) in crosses)
            {
                Assert.GreaterOrEqual(cross.Count, 3,
                    "Hasta la esquina más cerrada de un rectángulo deja centro + 2 brazos.");
                Assert.LessOrEqual(cross.Count, 5, "Una cruz nunca puede tener más de 5 casillas.");
                Assert.IsTrue(_grid.TryGetPosition(guid, out _), "La bomba tiene que estar en el grid.");
            }

            Assert.IsEmpty(Instances(), "Sembrar marca, no prende: el estallido es otro nodo.");
            Assert.IsEmpty(_pipeline.Resolved, "Nada tiene que cobrar en un tick que sólo telegrafía.");
        }

        [Test]
        public void Sowing_BombNearTheCorner_ClipsItsCrossAgainstTheRoom()
        {
            var tinyGrid = new GridManager();
            tinyGrid.LoadRoom(NavGraph.Rect(2, 2));
            _context.Grid = tinyGrid;

            var node = MakeNode(count: 1, spacing: 0);
            node.Tick(_context);

            var cross = Live().Single().Cross;

            Assert.AreEqual(3, cross.Count,
                "Cualquier casilla de una sala 2x2 es esquina: centro + 2 brazos válidos, los otros " +
                "2 caen fuera de la sala y no se marcan.");
            foreach (var coord in cross)
                Assert.IsTrue(tinyGrid.InBounds(coord), $"{coord} quedó marcada fuera de la sala.");
        }

        /// <summary>La vida es la autoridad, no el registro: la rota deja de estar armada en el acto.</summary>
        [Test]
        public void BreakingOneBomb_DropsItFromTheArmedList_WithoutTouchingTheOthers()
        {
            var node = MakeNode(count: 4);
            node.Tick(_context);

            var before = Live();
            var broken = before[0];
            var survivors = before.Skip(1).ToList();

            _attributes.SetAttributeValue<Health, int>(broken.Guid, 0);

            var after = Live();

            Assert.AreEqual(survivors.Count, after.Count,
                "Romper una bomba tiene que sacarla de la lista de armadas al toque, sin esperar " +
                "ningún tick.");
            Assert.IsFalse(after.Any(c => c.Guid == broken.Guid), "La rota se sigue reportando armada.");
            foreach (var survivor in survivors)
            {
                var stillThere = after.Single(c => c.Guid == survivor.Guid);
                CollectionAssert.AreEqual(survivor.Cross, stillThere.Cross,
                    "La cruz de una bomba que sigue viva no puede cambiar porque otra se rompió.");
            }
        }

        [Test]
        public void AtTheAuthoredSpacing_NoTwoCrossesShareATile()
        {
            var node = MakeNode(count: 6);
            node.Tick(_context);

            var crosses = Live();

            for (int i = 0; i < crosses.Count; i++)
            {
                for (int j = i + 1; j < crosses.Count; j++)
                {
                    var shared = crosses[i].Cross.Intersect(crosses[j].Cross).ToList();
                    Assert.IsEmpty(shared,
                        $"Las cruces de {crosses[i].Guid} y {crosses[j].Guid} comparten {string.Join(",", shared)}.");
                }
            }
        }

        /// <summary>Es el número que el nodo de la detonación descuenta: sembrar sin mecha deja bombas
        /// que no estallan nunca.</summary>
        [Test]
        public void Sowing_StampsTheAuthoredFuse()
        {
            var node = MakeNode(count: 2, fuse: 3);
            node.Tick(_context);

            var detonator = new AINode_DetonateBombField { ChannelPrefix = node.ChannelPrefix };

            detonator.Tick(_context);
            Assert.AreEqual(2, Live().Count, "Con mecha 3, el primer descuento no puede estallar nada.");

            detonator.Tick(_context);
            Assert.AreEqual(2, Live().Count, "Ni el segundo.");

            detonator.Tick(_context);
            Assert.IsEmpty(Live(), "Con mecha 3 tienen que estallar al tercer descuento.");
        }

        /// <summary>El jugador entra a la sala con el paño ya sembrado.</summary>
        [Test]
        public void Opening_SowsAndMarks_BeforeAnyTick()
        {
            var node = MakeNode(count: 4);

            node.Opening(_context);

            Assert.AreEqual(4, Live().Count, "La apertura no dejó las bombas puestas.");
            Assert.IsEmpty(Instances(), "La apertura instala amenaza, no fuego.");
            Assert.IsEmpty(_pipeline.Resolved,
                "La apertura cobró daño: corre ANTES del primer turno del jugador, así que ahí " +
                "no puede cobrar nada.");
        }

        /// <summary>
        /// En régimen la siembra cae <i>en</i> el turno del tiempo de bombas; la apertura cae uno
        /// antes de que ese turno llegue. Sin el +1 la generación de entrada estalla corrida y su
        /// fuego se le encima al del cono.
        /// </summary>
        [Test]
        public void Opening_GivesTheFirstGenerationOneExtraTurn()
        {
            var node = MakeNode(count: 2, fuse: 2);
            node.Opening(_context);

            var detonator = new AINode_DetonateBombField { ChannelPrefix = node.ChannelPrefix };

            detonator.Tick(_context);
            detonator.Tick(_context);
            Assert.AreEqual(2, Live().Count,
                "La generación de entrada estalló con la mecha de régimen: le falta el turno que " +
                "compensa haber nacido antes del primer tiempo de bombas.");

            detonator.Tick(_context);
            Assert.IsEmpty(Live(), "Con mecha 2 + 1 tiene que estallar al tercer descuento.");
        }

        /// <summary>El tiempo de bombas vuelve a pasar por las que siguen en pie: si les refrescara
        /// la mecha, no estallarían nunca.</summary>
        [Test]
        public void SowingAgainOverALiveBomb_DoesNotRefreshItsFuse()
        {
            var node = MakeNode(count: 2, fuse: 2);
            node.Tick(_context);

            var detonator = new AINode_DetonateBombField { ChannelPrefix = node.ChannelPrefix };
            detonator.Tick(_context);

            // El nodo vuelve a correr con las dos bombas todavía en pie: no siembra nada nuevo, pero
            // sí vuelve a pasar por ellas.
            node.Tick(_context);
            detonator.Tick(_context);

            Assert.IsEmpty(Live(),
                "Re-sembrar sobre una bomba viva le refrescó la mecha: así nunca llega al plazo.");
        }

        [Test]
        public void TheCoroutinePath_SowsAndMarksLikeTheSyncPath()
        {
            var node = MakeNode(count: 4);

            var routine = node.TickCoroutine(_context, null);
            while (routine.MoveNext()) { }

            Assert.AreEqual(4, Live().Count, "El camino coroutine sembró otra cantidad que el síncrono.");
            Assert.IsEmpty(Instances(), "Sembrar no prende nada.");
        }

        [Test]
        public void DefinitionNull_DoesNotThrow_AndSucceeds()
        {
            var node = new AINode_BombField { Definition = null };

            AIResult result = default;
            Assert.DoesNotThrow(() => result = node.Tick(_context));
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsEmpty(Instances());
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
