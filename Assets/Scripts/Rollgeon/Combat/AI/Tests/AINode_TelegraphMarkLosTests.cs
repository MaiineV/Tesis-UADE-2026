using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// El filtro de línea de visión del marcado: las formas dirigidas se recortan a lo que el
    /// atacante ve; las mecánicas de sala pasan enteras.
    /// </summary>
    [TestFixture]
    public sealed class AINode_TelegraphMarkLosTests
    {
        private GridManager _grid;
        private SpyThreatService _threat;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            // Arrange compartido: sala 9×9, boss (0,4), jugador (5,4), bloqueo en (2,4).
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, new GridCoord(0, 4));
            _grid.Register(_player, new GridCoord(5, 4));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 4));

            _threat = new SpyThreatService();
            ServiceLocator.AddService<IThreatenedAreaService>(_threat);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private AIContext Context() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
        };

        [Test]
        public void should_mark_only_visible_tiles_when_shape_is_directed()
        {
            // Arrange — Row (Size 1): la fila del jugador, de pared a pared.
            var node = new AINode_TelegraphMark
            {
                Shape = ThreatShape.Row,
                Size = 1,
                Damage = 10,
            };

            // Act
            var result = node.Tick(Context());

            // Assert — visible desde (0,4): su propia celda, (1,4) y la celda DEL bloqueo
            // (el destino de la línea nunca se bloquea a sí mismo). Todo lo de atrás, sombra.
            Assert.AreEqual(AIResult.Succeeded, result);
            CollectionAssert.AreEquivalent(
                new[] { new GridCoord(0, 4), new GridCoord(1, 4), new GridCoord(2, 4) },
                _threat.LastTiles,
                "La fila marcada tiene que cortarse en el bloqueo: la sombra no se marca ni cobra.");
        }

        [Test]
        public void should_fail_without_marking_when_every_tile_is_shadowed()
        {
            // Arrange — cuadrado sobre el jugador (3×3 alrededor de (5,4)), todo detrás del
            // bloqueo y de sus flancos: encajonamos la visual con una columna completa.
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 3));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 5));
            _grid.Register(Guid.NewGuid(), new GridCoord(1, 3));
            _grid.Register(Guid.NewGuid(), new GridCoord(1, 5));
            _grid.Register(Guid.NewGuid(), new GridCoord(0, 3));
            _grid.Register(Guid.NewGuid(), new GridCoord(0, 5));
            var node = new AINode_TelegraphMark
            {
                Shape = ThreatShape.SquareAroundPlayer,
                Size = 1,
                Damage = 10,
            };

            // Act
            var result = node.Tick(Context());

            // Assert — sin nada visible no hay marca: Failed deja que el árbol busque plan B.
            Assert.AreEqual(AIResult.Failed, result);
            Assert.IsNull(_threat.LastTiles, "No debería haberse marcado nada.");
        }

        [Test]
        public void should_mark_room_scale_shapes_untouched_by_line_of_sight()
        {
            // Arrange — GridPartition 1×1 celda 1 (índice 1-based) = toda la sala; mecánica
            // de sala, sin LOS.
            var node = new AINode_TelegraphMark
            {
                Shape = ThreatShape.GridPartition,
                Columns = 1,
                Rows = 1,
                Size = 1,
                Damage = 10,
            };

            // Act
            var result = node.Tick(Context());

            // Assert — las celdas detrás del bloqueo siguen marcadas.
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsNotNull(_threat.LastTiles);
            Assert.IsTrue(_threat.LastTiles.Contains(new GridCoord(6, 4)),
                "Una mecánica de sala no se agujerea por props: no apunta, reparte.");
        }

        // ==================================================================
        // Fixtures
        // ==================================================================

        private sealed class SpyThreatService : IThreatenedAreaService
        {
            public List<GridCoord> LastTiles;

            public void Mark(Guid sourceGuid, IEnumerable<GridCoord> tiles, int damage, AttackKind kind)
                => LastTiles = tiles.ToList();

            public bool HasPending(Guid sourceGuid) => false;
            public IReadOnlyCollection<GridCoord> GetPendingTiles(Guid sourceGuid)
                => Array.Empty<GridCoord>();
            public bool TryConsume(Guid sourceGuid, out ThreatenedArea pending)
            {
                pending = default;
                return false;
            }
            public bool TryPeek(Guid sourceGuid, out ThreatenedArea pending)
            {
                pending = default;
                return false;
            }
            public void Clear(Guid sourceGuid) { }
            public void ClearAll() { }
        }
    }
}
