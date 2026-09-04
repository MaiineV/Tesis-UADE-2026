using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Grid;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Las islas de 1 celda del NavGraph (tile suelto bajo una decoración, esquina
    /// huérfana del autorado) no son destino válido de teleport: quien cae ahí no puede
    /// volver a salir caminando (bug de playtest 03/09, "bug sala.jpeg").
    /// </summary>
    [TestFixture]
    public sealed class TeleportCellFilterTests
    {
        private static GridManager GridWith(NavGraph graph)
        {
            var grid = new GridManager();
            grid.LoadRoom(graph);
            return grid;
        }

        [Test]
        public void should_flag_stranded_when_node_has_no_edges()
        {
            // Arrange — dos celdas conectadas + una isla sin edges.
            var graph = new NavGraph();
            graph.AddNode(new NavNode(new GridCoord(0, 0), 0f));
            graph.AddNode(new NavNode(new GridCoord(1, 0), 0f));
            graph.AddNode(new NavNode(new GridCoord(5, 5), 0f));
            graph.AddBidirectionalEdge(new GridCoord(0, 0), new GridCoord(1, 0), 1f);
            var grid = GridWith(graph);

            // Act + Assert
            Assert.IsTrue(TeleportCellFilter.IsStrandedCell(grid, new GridCoord(5, 5)),
                "Un nodo sin edges es una isla: teleportar ahí deja al bicho atrapado.");
            Assert.IsFalse(TeleportCellFilter.IsStrandedCell(grid, new GridCoord(0, 0)),
                "Un nodo con edges no es isla.");
        }

        [Test]
        public void should_not_flag_anything_when_graph_is_empty_stub()
        {
            // Arrange — el stub "infinito" de los tests no tiene nodos ni edges.
            var grid = GridWith(new NavGraph());

            // Act + Assert — vetar todo con el stub rompería los fakes de EditMode.
            Assert.IsFalse(TeleportCellFilter.IsStrandedCell(grid, new GridCoord(3, 3)));
        }

        [Test]
        public void should_not_flag_cells_outside_the_graph()
        {
            // Arrange — celda que ni siquiera es nodo (no caminable): la filtra CanPlace,
            // no este filtro.
            var graph = new NavGraph();
            graph.AddNode(new NavNode(new GridCoord(0, 0), 0f));
            graph.AddNode(new NavNode(new GridCoord(1, 0), 0f));
            graph.AddBidirectionalEdge(new GridCoord(0, 0), new GridCoord(1, 0), 1f);
            var grid = GridWith(graph);

            // Act + Assert
            Assert.IsFalse(TeleportCellFilter.IsStrandedCell(grid, new GridCoord(9, 9)));
        }
    }
}
