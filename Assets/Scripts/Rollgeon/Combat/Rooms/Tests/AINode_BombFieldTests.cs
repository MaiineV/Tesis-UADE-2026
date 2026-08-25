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
using Rollgeon.Grid;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Combat.Rooms.Tests
{
    /// <summary>
    /// El tiempo del medio del Croupier: siembra, marca, y detona a ciclo cumplido. Contra los
    /// servicios reales de amenaza y de casillas — lo que se verifica es el comportamiento, no la
    /// forma de llamarlos.
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
        private SpecialTileDefinitionSO _fireTile;

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
            _tiles?.Dispose();
            _threat?.Dispose();
            _attributes?.Dispose();
            if (_bomb != null) UnityEngine.Object.DestroyImmediate(_bomb);
            if (_fireTile != null) UnityEngine.Object.DestroyImmediate(_fireTile);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private AINode_BombField MakeNode(int count, int spacing = 2) => new AINode_BombField
        {
            Definition = _bomb,
            FireTile = _fireTile,
            Count = count,
            Spacing = spacing,
            IgnitionDamage = 20,
            FireDurationRounds = 2,
        };

        private List<SpecialTileInfo> Instances() => new List<SpecialTileInfo>(_tiles.ActiveInstances());

        // =====================================================================
        // Primer tick: sólo siembra y marca
        // =====================================================================

        [Test]
        public void FirstTick_SeedsCountBombs_MarksTheirCrosses_AndIgnitesNothing()
        {
            var node = MakeNode(count: 3);

            var result = node.Tick(_context);

            Assert.AreEqual(AIResult.Succeeded, result,
                "El primer tick no tiene nada que detonar; Failed acá abortaría la Sequence del " +
                "jefe en su primer turno de siembra.");

            var crosses = node.LiveCrosses(_attributes).ToList();
            Assert.AreEqual(3, crosses.Count, "Tienen que quedar armadas las 3 bombas sembradas.");

            foreach (var (guid, cross) in crosses)
            {
                Assert.GreaterOrEqual(cross.Count, 3,
                    "Hasta la esquina más cerrada de un rectángulo deja centro + 2 brazos.");
                Assert.LessOrEqual(cross.Count, 5, "Una cruz nunca puede tener más de 5 casillas.");
                Assert.IsTrue(_grid.TryGetPosition(guid, out _), "La bomba tiene que estar en el grid.");
            }

            Assert.IsEmpty(Instances(), "El primer tick marca, no prende: no puede haber pasado un ciclo todavía.");
            Assert.IsEmpty(_pipeline.Resolved, "Nada tiene que cobrar en un tick que sólo telegrafía.");
        }

        [Test]
        public void FirstTick_BombNearTheCorner_ClipsItsCrossAgainstTheRoom()
        {
            var tinyGrid = new GridManager();
            tinyGrid.LoadRoom(NavGraph.Rect(2, 2));
            _context.Grid = tinyGrid;

            var node = MakeNode(count: 1, spacing: 0);
            node.Tick(_context);

            var cross = node.LiveCrosses(_attributes).Single().Cross;

            Assert.AreEqual(3, cross.Count,
                "Cualquier casilla de una sala 2x2 es esquina: centro + 2 brazos válidos, los otros " +
                "2 caen fuera de la sala y no se marcan.");
            foreach (var coord in cross)
                Assert.IsTrue(tinyGrid.InBounds(coord), $"{coord} quedó marcada fuera de la sala.");
        }

        // =====================================================================
        // Romper a mano entre ticks
        // =====================================================================

        [Test]
        public void BreakingOneBombBetweenTicks_LiftsOnlyItsOwnCross()
        {
            var node = MakeNode(count: 3);
            node.Tick(_context);

            var before = node.LiveCrosses(_attributes).ToList();
            var broken = before[0];
            var survivors = before.Skip(1).ToList();

            _attributes.SetAttributeValue<Health, int>(broken.Guid, 0);

            var after = node.LiveCrosses(_attributes).ToList();

            Assert.AreEqual(survivors.Count, after.Count,
                "Romper una bomba entre ticks tiene que sacarla de la lista de armadas al toque, " +
                "sin esperar al próximo tick.");
            Assert.IsFalse(after.Any(c => c.Guid == broken.Guid), "La rota se sigue reportando armada.");
            foreach (var survivor in survivors)
            {
                var stillThere = after.Single(c => c.Guid == survivor.Guid);
                CollectionAssert.AreEqual(survivor.Cross, stillThere.Cross,
                    "La cruz de una bomba que sigue viva no puede cambiar porque otra se rompió.");
            }
        }

        // =====================================================================
        // Segundo tick: detona lo vivo, no lo roto
        // =====================================================================

        [Test]
        public void SecondTick_SurvivorsIgniteTheirCrossAndVanish_BrokenOnesIgniteNothing()
        {
            var node = MakeNode(count: 3);
            node.Tick(_context);

            var wave1 = node.LiveCrosses(_attributes).ToList();
            var broken = wave1[0];
            var survivors = wave1.Skip(1).ToList();
            _attributes.SetAttributeValue<Health, int>(broken.Guid, 0);

            node.Tick(_context);

            var burnedTiles = new HashSet<GridCoord>(Instances().SelectMany(i => i.Coords));
            foreach (var survivor in survivors)
                foreach (var coord in survivor.Cross)
                    Assert.IsTrue(burnedTiles.Contains(coord),
                        $"{coord} era parte de la cruz de una bomba viva y no se prendió.");

            foreach (var coord in broken.Cross)
                Assert.IsFalse(burnedTiles.Contains(coord),
                    $"{coord} pertenecía a la bomba rota a mano — no debería haber ardido.");

            Assert.IsFalse(_grid.TryGetPosition(broken.Guid, out _), "La rota tiene que salir del grid igual.");
            foreach (var survivor in survivors)
                Assert.IsFalse(_grid.TryGetPosition(survivor.Guid, out _),
                    "La que detonó tiene que desaparecer del grid.");

            var newWave = node.LiveCrosses(_attributes).ToList();
            Assert.AreEqual(3, newWave.Count, "El ciclo siguiente vuelve a sembrar las 3 bombas.");
            var oldGuids = new HashSet<Guid>(wave1.Select(c => c.Guid));
            Assert.IsTrue(newWave.All(c => !oldGuids.Contains(c.Guid)),
                "Las bombas nuevas tienen que ser objetos distintos de los de la ola anterior.");
        }

        // =====================================================================
        // Separación entre cruces
        // =====================================================================

        [Test]
        public void WithSpacingTwo_NoTwoCrossesShareATile()
        {
            var node = MakeNode(count: 6, spacing: 2);
            node.Tick(_context);

            var crosses = node.LiveCrosses(_attributes).ToList();

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

        // =====================================================================
        // Degradado sin datos
        // =====================================================================

        [Test]
        public void DefinitionNull_DoesNotThrow_AndSucceeds()
        {
            var node = new AINode_BombField { Definition = null, FireTile = _fireTile };

            AIResult result = default;
            Assert.DoesNotThrow(() => result = node.Tick(_context));
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsEmpty(Instances());
        }

        [Test]
        public void FireTileNull_DoesNotThrow_AndSucceeds()
        {
            var node = new AINode_BombField { Definition = _bomb, FireTile = null };

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
