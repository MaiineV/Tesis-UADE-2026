using System;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Movement;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Cubre BUG de alcanzabilidad: el rango marcaba celdas por distancia Manhattan
    /// pura sin verificar que exista camino caminable (una celda aislada a rango
    /// máximo se mostraba como destino válido de movimiento).
    /// </summary>
    [TestFixture]
    public sealed class SelectionSettingsRangeModeTests
    {
        private GridManager _grid;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _grid = new GridManager();
            _owner = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        private void RegisterGridServices(bool withMovement = true)
        {
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            if (withMovement)
                ServiceLocator.AddService<IMovementService>(new MovementService(_grid), ServiceScope.Global);
        }

        // 5x3 con la columna x=2 entera bloqueada: el pocket x>=3 es caminable
        // pero inalcanzable desde x<=1.
        private void LoadRoomWithDisconnectedPocket()
        {
            var walkable = Enumerable.Repeat(true, 15).ToArray();
            walkable[2] = false;  // (2,0)
            walkable[7] = false;  // (2,1)
            walkable[12] = false; // (2,2)
            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(5, 3, walkable)));
            _grid.Register(_owner, new GridCoord(0, 1));
        }

        private static SelectionSettings MovementSelection(RangeMode mode, int range)
        {
            return new SelectionSettings
            {
                SlotState = SlotState.Empty,
                Timing = SelectionTiming.BeforeRoll,
                Range = range,
                RangeMode = mode,
            };
        }

        private static SelectionSettings AttackSelection(int range)
        {
            return new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                EntityFilter = EntityFilterMask.Enemies,
                Timing = SelectionTiming.BeforeRoll,
                Range = range,
                RangeMode = RangeMode.Manhattan,
            };
        }

        private static bool Contains(System.Collections.Generic.List<GridCoord> tiles, int x, int z)
        {
            return tiles.Any(c => c == new GridCoord(x, z));
        }

        private static bool Contains(System.Collections.Generic.List<TargetRef> tiles, int x, int z)
        {
            return tiles.Any(t => t.Coord == new GridCoord(x, z));
        }

        [Test]
        public void RangeMode_DefaultValue_IsManhattan()
        {
            // Arrange + Act
            var settings = new SelectionSettings();

            // Assert — back-compat: assets serializados sin el campo deben caer en Manhattan.
            Assert.AreEqual(RangeMode.Manhattan, settings.RangeMode);
        }

        [Test]
        public void ResolveValidTiles_Manhattan_IncludesDisconnectedWalkableTile()
        {
            // Arrange — regresión: los ataques siguen ignorando conectividad.
            LoadRoomWithDisconnectedPocket();
            RegisterGridServices();
            var settings = MovementSelection(RangeMode.Manhattan, 4);

            // Act
            var tiles = settings.ResolveValidTiles(new GridCoord(0, 1), _owner);

            // Assert — (3,1) está a Manhattan 3, caminable pero desconectada.
            Assert.IsTrue(Contains(tiles, 3, 1),
                "Manhattan debe incluir la celda del pocket desconectado (comportamiento de ataques).");
        }

        [Test]
        public void ResolveValidTiles_PathReachable_ExcludesDisconnectedTile()
        {
            // Arrange — el bug reportado.
            LoadRoomWithDisconnectedPocket();
            RegisterGridServices();
            var settings = MovementSelection(RangeMode.PathReachable, 4);

            // Act
            var tiles = settings.ResolveValidTiles(new GridCoord(0, 1), _owner);

            // Assert
            Assert.IsFalse(Contains(tiles, 3, 1),
                "PathReachable no debe incluir celdas sin camino caminable conectado.");
            Assert.IsTrue(Contains(tiles, 1, 1),
                "PathReachable debe incluir las celdas conectadas dentro del rango.");
            Assert.IsFalse(Contains(tiles, 0, 1),
                "El origen nunca es destino válido.");
        }

        [Test]
        public void ResolveValidTiles_PathReachable_OccupiedChokepointBlocksTilesBeyond()
        {
            // Arrange — pasillo 5x1 con un aliado en (2,0): corta la propagación.
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 0));
            RegisterGridServices();
            var settings = MovementSelection(RangeMode.PathReachable, 4);

            // Act
            var tiles = settings.ResolveValidTiles(new GridCoord(0, 0), _owner);

            // Assert
            Assert.IsTrue(Contains(tiles, 1, 0), "La celda antes del chokepoint es alcanzable.");
            Assert.IsFalse(Contains(tiles, 2, 0), "La celda ocupada no es destino válido.");
            Assert.IsFalse(Contains(tiles, 3, 0),
                "Las celdas detrás del chokepoint ocupado no son alcanzables.");
        }

        [Test]
        public void ResolveValidTiles_PathReachable_WithoutMovementService_FallsBackToManhattan()
        {
            // Arrange — sin IMovementService registrado degrada a Manhattan (con warning).
            LoadRoomWithDisconnectedPocket();
            RegisterGridServices(withMovement: false);
            var settings = MovementSelection(RangeMode.PathReachable, 4);

            // Act
            var tiles = settings.ResolveValidTiles(new GridCoord(0, 1), _owner);

            // Assert
            Assert.IsTrue(Contains(tiles, 3, 1),
                "Sin servicio de movimiento el fallback Manhattan mantiene el comportamiento previo.");
        }

        [Test]
        public void ResolveRangeTiles_Manhattan_CoversWholeRangeExcludingOwner()
        {
            // Arrange — pasillo 5x1, owner en (0,0), rango Manhattan 3.
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            RegisterGridServices();
            var settings = AttackSelection(3);

            // Act
            var range = settings.ResolveRangeTiles(new GridCoord(0, 0), _owner);

            // Assert — todo el alcance geométrico salvo el owner; (4,0) queda a Manhattan 4.
            Assert.IsTrue(Contains(range, 1, 0));
            Assert.IsTrue(Contains(range, 2, 0));
            Assert.IsTrue(Contains(range, 3, 0));
            Assert.IsFalse(Contains(range, 0, 0), "El owner nunca entra al rango.");
            Assert.IsFalse(Contains(range, 4, 0), "Fuera del rango Manhattan no se incluye.");
        }

        [Test]
        public void ResolveRangeTiles_ShowsFullRange_WhileValidTiles_OnlyEnemyOccupant()
        {
            // Arrange — un enemigo en (2,0); las demás celdas del rango están vacías.
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 0));
            RegisterGridServices();
            var settings = AttackSelection(3);

            // Act — rango completo vs targets clickeables.
            var range = settings.ResolveRangeTiles(new GridCoord(0, 0), _owner);
            var valid = settings.ResolveValidTiles(new GridCoord(0, 0), _owner);

            // Assert — el rango cubre casillas vacías Y la del enemigo…
            Assert.IsTrue(Contains(range, 1, 0), "El rango incluye casillas vacías dentro del alcance.");
            Assert.IsTrue(Contains(range, 2, 0), "El rango incluye la casilla del enemigo.");
            // …pero solo el enemigo es un target válido (clickeable).
            Assert.AreEqual(1, valid.Count, "Solo el enemigo es target válido.");
            Assert.IsTrue(valid.Any(t => t.Coord == new GridCoord(2, 0)));
        }

        [Test]
        public void ResolveRangeTiles_Self_ReturnsEmpty()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            RegisterGridServices();
            var settings = new SelectionSettings { SlotState = SlotState.Self };

            // Act
            var range = settings.ResolveRangeTiles(new GridCoord(0, 0), _owner);

            // Assert — Self no pinta rango.
            Assert.AreEqual(0, range.Count);
        }

        [Test]
        public void TargetsEnemies_TrueForOccupiedEnemyFilter_FalseOtherwise()
        {
            // Assert — ataque/ataque especial (occupied + enemies) pinta rango completo.
            Assert.IsTrue(AttackSelection(1).TargetsEnemies);
            // Movimiento (slot vacío) no.
            Assert.IsFalse(MovementSelection(RangeMode.Manhattan, 1).TargetsEnemies);
            // Self no.
            Assert.IsFalse(new SelectionSettings { SlotState = SlotState.Self }.TargetsEnemies);
            // Occupied pero filtrando aliados (no enemigos) no.
            Assert.IsFalse(new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                EntityFilter = EntityFilterMask.Allies,
            }.TargetsEnemies);
        }
    }
}
