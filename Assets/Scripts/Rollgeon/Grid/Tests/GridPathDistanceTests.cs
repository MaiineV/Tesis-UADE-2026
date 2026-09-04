using System;
using NUnit.Framework;
using Patterns;

namespace Rollgeon.Grid.Tests
{
    /// <summary>
    /// El mapa de costos de camino real. Lo que se fija acá es que un OCUPANTE no sea una pared
    /// permanente: antes lo era y dejaba sin costo a toda la región detrás de él, así que el nodo
    /// de movimiento del enemigo de atrás no encontraba ningún candidato puntuable y se congelaba
    /// hasta que el jugador se movía (BUG de playtest del Charger / bolas de pool).
    /// </summary>
    [TestFixture]
    public sealed class GridPathDistanceTests
    {
        private GridManager _grid;
        private Guid _mover;
        private Guid _target;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _grid = new GridManager();
            _mover = Guid.NewGuid();
            _target = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        [Test]
        public void Occupant_DoesNotWallOffTheRegionBehindIt()
        {
            // Arrange — pasillo 1 de ancho: target(0,0) … bloqueante(2,0) … mover(4,0).
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_target, new GridCoord(0, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 0)); // aliado tapando el corredor
            _grid.Register(_mover, new GridCoord(4, 0));

            // Act
            var cost = GridPathDistance.ComputeFrom(_grid, new GridCoord(0, 0), _mover, _target);

            // Assert — antes el BFS cortaba en (2,0) y (3,0)/(4,0) quedaban FUERA del mapa.
            Assert.IsTrue(cost.ContainsKey(new GridCoord(4, 0)),
                "La celda detrás del ocupante tiene que tener costo, no quedar inalcanzable.");
            Assert.Greater(cost[new GridCoord(4, 0)], 4,
                "Atravesar al ocupante tiene que costar más que los 4 tiles en línea recta.");
        }

        [Test]
        public void Occupant_IsRoutedAroundWhenADetourExists()
        {
            // Arrange — sala 3×3 con el bloqueante justo en el medio de la fila del target.
            _grid.LoadRoom(NavGraph.Rect(3, 3));
            _grid.Register(_target, new GridCoord(0, 1));
            _grid.Register(Guid.NewGuid(), new GridCoord(1, 1));
            _grid.Register(_mover, new GridCoord(2, 1));

            // Act
            var cost = GridPathDistance.ComputeFrom(_grid, new GridCoord(0, 1), _mover, _target);

            // Assert — el rodeo por arriba/abajo son 4 pasos limpios; atravesar costaría 1+4+1.
            Assert.AreEqual(4, cost[new GridCoord(2, 1)],
                "Con desvío disponible la ruta rodea al ocupante en vez de atravesarlo.");
        }

        [Test]
        public void IgnoredEntities_DoNotCostExtra()
        {
            // Arrange — la celda ocupada es la del PROPIO mover: no debe penalizarse a sí mismo.
            _grid.LoadRoom(NavGraph.Rect(3, 1));
            _grid.Register(_target, new GridCoord(0, 0));
            _grid.Register(_mover, new GridCoord(1, 0));

            // Act
            var cost = GridPathDistance.ComputeFrom(_grid, new GridCoord(0, 0), _mover, _target);

            // Assert
            Assert.AreEqual(1, cost[new GridCoord(1, 0)]);
            Assert.AreEqual(2, cost[new GridCoord(2, 0)]);
        }

        [Test]
        public void Terrain_StillBlocks()
        {
            // Arrange — una pared sí corta: sólo los ocupantes dejaron de ser muro.
            var graph = NavGraph.Rect(3, 1);
            graph.RemoveNode(new GridCoord(1, 0));
            _grid.LoadRoom(graph);
            _grid.Register(_target, new GridCoord(0, 0));

            // Act
            var cost = GridPathDistance.ComputeFrom(_grid, new GridCoord(0, 0), _mover, _target);

            // Assert
            Assert.IsFalse(cost.ContainsKey(new GridCoord(2, 0)),
                "Detrás de una pared no hay camino, y eso no cambia.");
        }
    }
}
