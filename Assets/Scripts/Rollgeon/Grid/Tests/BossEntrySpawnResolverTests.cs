using System;
using NUnit.Framework;
using Rollgeon.Grid;

namespace Rollgeon.Grid.Tests
{
    /// <summary>
    /// Dónde arranca el jefe según por qué puerta entraste. La sala de prueba es 11x11 con
    /// coordenadas 0..10, así que el bounding box va de 0 a 10 en los dos ejes.
    /// </summary>
    [TestFixture]
    public class BossEntrySpawnResolverTests
    {
        private const int Inset = 2;

        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 11));
        }

        private GridCoord Resolve(GridCoord entry)
        {
            Assert.IsTrue(
                BossEntrySpawnResolver.TryResolveAwayFromEntry(_grid, entry, Inset, out var coord),
                $"La sala 11x11 tiene que ofrecer una casilla para una entrada en {entry}.");
            return coord;
        }

        /// <summary>Entrando por el sur, el jefe cruza a la pared norte — y al revés.</summary>
        [Test]
        public void EnteringFromTheSouth_PutsTheBossAgainstTheNorthWall()
        {
            Assert.AreEqual(new GridCoord(5, 8), Resolve(new GridCoord(5, 1)));
        }

        [Test]
        public void EnteringFromTheNorth_PutsTheBossAgainstTheSouthWall()
        {
            Assert.AreEqual(new GridCoord(5, 2), Resolve(new GridCoord(5, 9)));
        }

        [Test]
        public void EnteringFromTheWest_PutsTheBossAgainstTheEastWall()
        {
            Assert.AreEqual(new GridCoord(8, 5), Resolve(new GridCoord(1, 5)));
        }

        [Test]
        public void EnteringFromTheEast_PutsTheBossAgainstTheWestWall()
        {
            Assert.AreEqual(new GridCoord(2, 5), Resolve(new GridCoord(9, 5)));
        }

        /// <summary>El jefe queda enfilado con la puerta y no en una esquina: es lo que separa
        /// "lejos" de "arrinconado".</summary>
        [Test]
        public void TheBoss_LinesUpWithTheDoor_NotWithTheRoomAxis()
        {
            Assert.AreEqual(new GridCoord(2, 8), Resolve(new GridCoord(2, 1)),
                "Entrando por una puerta corrida al oeste, el jefe tiene que quedar en esa misma " +
                "columna, no en el medio de la pared de enfrente.");
        }

        /// <summary>Es el punto del cambio: la casilla del centro deja de ser la de arranque.</summary>
        [Test]
        public void TheBoss_NeverStartsOnTheRoomCentre()
        {
            var centre = new GridCoord(5, 5);

            foreach (var entry in new[]
                     {
                         new GridCoord(5, 1), new GridCoord(5, 9),
                         new GridCoord(1, 5), new GridCoord(9, 5),
                     })
            {
                Assert.AreNotEqual(centre, Resolve(entry),
                    $"Entrando por {entry} el jefe volvió a arrancar en el centro.");
            }
        }

        [Test]
        public void TheBoss_NeverStartsOnTheEntryTile()
        {
            var entry = new GridCoord(5, 1);
            Assert.AreNotEqual(entry, Resolve(entry));
        }

        /// <summary>Register desaloja al ocupante previo de la casilla: elegir una ocupada le
        /// sacaría del grid a quien ya estaba.</summary>
        [Test]
        public void AnOccupiedTarget_FallsBackToTheNearestFreeTile()
        {
            var target = new GridCoord(5, 8);
            _grid.Register(Guid.NewGuid(), target);

            var coord = Resolve(new GridCoord(5, 1));

            Assert.AreNotEqual(target, coord, "Eligió una casilla ocupada.");
            Assert.LessOrEqual(coord.Manhattan(target), 2,
                "Con la de enfrente ocupada tiene que caer en una pegada, no del otro lado de la sala.");
        }

        /// <summary>Un nodo caminable puede tener grado 0 —una isla del NavGraph— y ahí el jefe
        /// arranca encerrado.</summary>
        [Test]
        public void ATargetWithoutNeighbours_IsSkipped()
        {
            var walkable = new bool[11 * 11];
            for (int i = 0; i < walkable.Length; i++) walkable[i] = true;

            // Aísla (5,8): se le apagan los cuatro ortogonales, así queda caminable y sin aristas.
            foreach (var n in new[]
                     {
                         new GridCoord(5, 7), new GridCoord(5, 9),
                         new GridCoord(4, 8), new GridCoord(6, 8),
                     })
            {
                walkable[n.Y * 11 + n.X] = false;
            }

            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(11, 11, walkable)));

            var coord = Resolve(new GridCoord(5, 1));

            Assert.AreNotEqual(new GridCoord(5, 8), coord,
                "Eligió una isla del NavGraph: el jefe arrancaría sin poder moverse.");
        }

        /// <summary>El grafo vacío dice que TODO es caminable mientras no enumera ninguna casilla.
        /// Sin esta guarda el jefe se coloca en una sala que no existe.</summary>
        [Test]
        public void AnEmptyRoom_ResolvesToNothing()
        {
            var empty = new GridManager();

            Assert.IsFalse(
                BossEntrySpawnResolver.TryResolveAwayFromEntry(
                    empty, new GridCoord(5, 1), Inset, out _),
                "Con el grafo stub tiene que devolver false y dejar que el llamador use la celda autorada.");
        }

        [Test]
        public void NullGrid_ResolvesToNothing()
        {
            Assert.IsFalse(
                BossEntrySpawnResolver.TryResolveAwayFromEntry(
                    null, new GridCoord(5, 1), Inset, out _));
        }

        /// <summary>El inset no puede empujar la casilla fuera de la sala.</summary>
        [Test]
        public void AnInsetLargerThanTheRoom_StaysInside()
        {
            Assert.IsTrue(BossEntrySpawnResolver.TryResolveAwayFromEntry(
                _grid, new GridCoord(5, 1), wallInset: 40, out var coord));

            Assert.IsTrue(_grid.IsWalkable(coord), $"{coord} quedó fuera de la sala.");
        }
    }
}
