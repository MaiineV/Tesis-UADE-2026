using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Pinea el invariante del que vive la sala de La Generala: <b>sus dados son las paredes</b>.
    /// La sala del piso 3 arranca sin un solo obstáculo fijo y se llena con los cinco dados que
    /// ella misma tira, así que la geometría de la pelea es la mesa.
    /// </summary>
    /// <remarks>
    /// No hay código nuevo acá: los dados se spawnean con <c>grid.Register</c> y
    /// <see cref="MovementService"/> descarta las casillas ocupadas. El test existe porque ese
    /// encadenado es el único motivo por el que romper un dado abre camino — si alguien hace que
    /// los dados dejen de ocupar casilla (por ejemplo migrándolos a un objeto de sala que no
    /// bloquea), la sala se vuelve un campo abierto y esto cae antes que el playtest.
    /// </remarks>
    [TestFixture]
    public class GeneralaDiceBlockTests
    {
        private static readonly GridCoord PlayerTile = new GridCoord(0, 1);
        private static readonly GridCoord FarSide = new GridCoord(4, 1);
        private static readonly GridCoord MiddleDieTile = new GridCoord(2, 1);

        private GridManager _grid;
        private MovementService _movement;
        private Guid _player;
        private Guid _middleDie;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 3));
            ServiceLocator.AddService<IGridManager>(_grid);
            _movement = new MovementService(_grid);

            _player = Guid.NewGuid();
            _grid.Register(_player, PlayerTile);

            // Tres dados en columna: la mesa parte la sala en dos.
            _middleDie = Register(MiddleDieTile);
            Register(new GridCoord(2, 0));
            Register(new GridCoord(2, 2));
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Dice_BlockThePathAcrossTheTable()
        {
            // Act
            var path = _movement.FindPath(PlayerTile, FarSide);

            // Assert
            CollectionAssert.IsEmpty(path,
                "Con la columna de dados en pie no hay ruta al otro lado de la mesa.");
        }

        [Test]
        public void Dice_CutTheRoomInTwo_ForMovementRange()
        {
            // Act — rango de sobra para cruzar la sala entera si no hubiera dados.
            var reachable = _movement.GetReachableTiles(PlayerTile, range: 6);

            // Assert
            foreach (var tile in reachable)
                Assert.Less(tile.X, 2,
                    $"La casilla {tile} está del lado de allá de la mesa o encima de un dado.");
        }

        [Test]
        public void BreakingADie_OpensThePath_Through_ItsTile()
        {
            // Arrange — el jugador le rompe el dado del medio: CombatDeathWatcher lo saca de la grilla.
            _grid.Unregister(_middleDie);

            // Act
            var path = _movement.FindPath(PlayerTile, FarSide);

            // Assert — un golpe, dos consecuencias: le borra una categoría y le abre la sala.
            CollectionAssert.IsNotEmpty(path);
            CollectionAssert.Contains(path, MiddleDieTile,
                "El hueco que dejó el dado roto es el único paso al otro lado.");
        }

        private Guid Register(GridCoord coord)
        {
            var die = Guid.NewGuid();
            _grid.Register(die, coord);
            return die;
        }
    }
}
