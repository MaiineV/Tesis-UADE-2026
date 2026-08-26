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

        private AINode_BombField MakeNode(
            int count,
            int spacing = 3,
            int fuse = 2,
            AINode_BombField.BlastShape shape = AINode_BombField.BlastShape.Orthogonal) =>
            new AINode_BombField
            {
                Definition = _bomb,
                Count = count,
                Spacing = spacing,
                FuseTurns = fuse,
                Shape = shape,
                IgnitionDamage = 20,
            };

        /// <summary>
        /// Qué dibujo tiene la cruz de una bomba, leído de la geometría y no de lo que se pidió. Un
        /// brazo diagonal es el único que está a Chebyshev 1 y Manhattan 2 del centro a la vez; uno
        /// ortogonal, el único que está a 1 de las dos. Sirve con la cruz recortada contra el borde.
        /// </summary>
        private static AINode_BombField.BlastShape ShapeOf(
            GridCoord center, IReadOnlyList<GridCoord> cross)
        {
            CollectionAssert.Contains(cross, center, "La cruz no incluye la casilla de la bomba.");

            var arms = cross.Where(c => !c.Equals(center)).ToList();
            Assert.IsNotEmpty(arms, "Una cruz de una sola casilla no dice nada de su forma.");

            bool diagonal = arms.All(c => c.Chebyshev(center) == 1 && c.Manhattan(center) == 2);
            bool orthogonal = arms.All(c => c.Manhattan(center) == 1);
            Assert.IsTrue(diagonal ^ orthogonal,
                $"La cruz de ({center.X},{center.Y}) no es ni + ni x: {Render(arms)}.");

            return diagonal
                ? AINode_BombField.BlastShape.Diagonal
                : AINode_BombField.BlastShape.Orthogonal;
        }

        private GridCoord CenterOf(Guid guid)
        {
            Assert.IsTrue(_grid.TryGetPosition(guid, out var center), "La bomba no está en el grid.");
            return center;
        }

        private static string Render(IEnumerable<GridCoord> coords) =>
            string.Join(" ", coords.Select(c => $"({c.X},{c.Y})"));

        /// <summary>
        /// Siembra, fotografía lo sembrado y vacía el paño para que la próxima sea otra generación.
        /// La foto lleva el centro y no el guid: al estallar, la bomba sale del grid.
        /// </summary>
        private List<(GridCoord Center, IReadOnlyList<GridCoord> Cross)> SowWave(AINode_BombField node)
        {
            node.Tick(_context);
            var wave = Live().Select(b => (CenterOf(b.Guid), b.Cross)).ToList();

            var detonator = new AINode_DetonateBombField { ChannelPrefix = node.ChannelPrefix };
            for (int turn = 0; turn < 8 && Live().Count > 0; turn++) detonator.Tick(_context);
            Assert.IsEmpty(Live(),
                "El paño quedó con bombas: la próxima siembra no sería una generación nueva.");

            return wave;
        }

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

        /// <summary>
        /// El overlay no está en los bootstrap: lo crea el primero que pinta. Consultándolo con
        /// TryGetService la primera siembra de la pelea caía antes de que existiera, y esas bombas
        /// —las únicas que el jugador ve aparecer sin haber visto nunca un aviso— quedaban sin cruz.
        /// </summary>
        [Test]
        public void TheFirstSowingOfTheFight_PaintsItsCrosses_WithNoPainterRegisteredYet()
        {
            ServiceLocator.RemoveService<IThreatOverlayService>();
            // Show necesita el grid para ubicar los quads; en la pelea lo trae el bootstrap.
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            var node = MakeNode(count: 4);
            node.Tick(_context);

            Assert.IsTrue(ServiceLocator.TryGetService<IThreatOverlayService>(out var painter),
                "La siembra tiene que levantar el overlay, no esperar a que otro nodo lo cree.");

            var overlay = (ThreatTelegraphOverlay)painter;
            foreach (var (guid, cross) in Live())
            {
                var channel = AINode_BombField.ChannelFor(_boss, node.ChannelPrefix, guid);
                Assert.AreEqual(cross.Count, overlay.ActiveQuadsOf(channel).Count,
                    "La bomba quedó sin cruz pintada.");
            }

            overlay.Dispose();
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

        /// <summary>
        /// El aspa: la casilla y sus 4 diagonales. Las dos formas cubren lo mismo, así que el fuego
        /// que queda pesa igual — lo que se invierte es dónde está el hueco seguro.
        /// </summary>
        [Test]
        public void TheDiagonalShape_TakesTheCornersAndLeavesTheSidesOpen()
        {
            var node = MakeNode(count: 4, shape: AINode_BombField.BlastShape.Diagonal);
            node.Tick(_context);

            foreach (var (guid, cross) in Live())
            {
                var center = CenterOf(guid);
                Assert.AreEqual(AINode_BombField.BlastShape.Diagonal, ShapeOf(center, cross));

                foreach (var side in center.Neighbors4())
                {
                    CollectionAssert.DoesNotContain(cross, side,
                        "La ortogonal es justo la casilla que el aspa deja abierta.");
                }
            }
        }

        [Test]
        public void ADiagonalBombNearTheCorner_ClipsItsArmsAgainstTheRoom()
        {
            var tinyGrid = new GridManager();
            tinyGrid.LoadRoom(NavGraph.Rect(2, 2));
            _context.Grid = tinyGrid;

            var node = MakeNode(count: 1, spacing: 0, shape: AINode_BombField.BlastShape.Diagonal);
            node.Tick(_context);

            var cross = Live().Single().Cross;

            Assert.AreEqual(2, cross.Count,
                "Cualquier casilla de una sala 2x2 es esquina: al aspa le queda centro + 1 diagonal.");
            foreach (var coord in cross)
                Assert.IsTrue(tinyGrid.InBounds(coord), $"{coord} quedó marcada fuera de la sala.");
        }

        /// <summary>
        /// La rotación es lo que hace que la esquiva no se memorice: la casilla que salvó de la
        /// generación anterior es exactamente la que mata en la siguiente.
        /// </summary>
        [Test]
        public void Alternating_SowsAPlus_ThenAnX_ThenAPlusAgain()
        {
            var node = MakeNode(count: 3, shape: AINode_BombField.BlastShape.Alternating);

            var expected = new[]
            {
                AINode_BombField.BlastShape.Orthogonal,
                AINode_BombField.BlastShape.Diagonal,
                AINode_BombField.BlastShape.Orthogonal,
                AINode_BombField.BlastShape.Diagonal,
            };

            for (int wave = 0; wave < expected.Length; wave++)
            {
                var sown = SowWave(node);
                Assert.IsNotEmpty(sown, $"La siembra {wave} no plantó nada.");

                foreach (var (center, cross) in sown)
                {
                    Assert.AreEqual(expected[wave], ShapeOf(center, cross),
                        $"La siembra {wave} salió con la forma de la otra.");
                }
            }
        }

        /// <summary>Una generación entera comparte forma: media siembra en aspa no se puede leer.</summary>
        [Test]
        public void EveryBombOfOneSowing_SharesTheSameShape()
        {
            var node = MakeNode(count: 6, shape: AINode_BombField.BlastShape.Alternating);
            SowWave(node);

            var second = SowWave(node);

            var shapes = second.Select(b => ShapeOf(b.Center, b.Cross)).Distinct().ToList();
            Assert.AreEqual(1, shapes.Count, "La misma siembra mezcló + y x.");
        }

        [Test]
        public void AFixedShape_NeverRotates()
        {
            var node = MakeNode(count: 3, shape: AINode_BombField.BlastShape.Orthogonal);

            for (int wave = 0; wave < 3; wave++)
            {
                foreach (var (center, cross) in SowWave(node))
                {
                    Assert.AreEqual(AINode_BombField.BlastShape.Orthogonal, ShapeOf(center, cross),
                        "Pedida una forma fija, la siembra rotó igual.");
                }
            }
        }

        /// <summary>
        /// El tiempo de bombas vuelve a pasar por las que siguen en pie, y con las formas rotando eso
        /// alcanzaba para pintarle a una bomba vieja el aspa de la generación nueva: el aviso decía
        /// una cosa y le estallaba otra.
        /// </summary>
        [Test]
        public void ABombLeftStanding_KeepsTheCrossItWasArmedWith()
        {
            var node = MakeNode(count: 3, shape: AINode_BombField.BlastShape.Alternating);
            node.Tick(_context);

            var first = Live().ToDictionary(b => b.Guid, b => b.Cross);

            // Sin detonar nada en el medio: la siembra siguiente rota la forma pero no planta nada
            // nuevo, porque las tres de la primera siguen en pie.
            node.Tick(_context);

            foreach (var (guid, cross) in Live())
            {
                CollectionAssert.AreEqual(first[guid], cross,
                    "Una bomba armada cambió de cruz porque la siembra siguiente rotó la forma.");

                var channel = AINode_BombField.ChannelFor(_boss, node.ChannelPrefix, guid);
                CollectionAssert.AreEquivalent(cross, _threat.GetPendingTiles(channel),
                    "Lo avisado dejó de ser lo que le va a estallar.");
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
