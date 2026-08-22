using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="ThreatAreaShape.ComputeGridPartition"/>: la partición genérica en
    /// columnas × filas que no hereda la costura deliberada de <see cref="ThreatAreaShape.ComputeRoomSector"/>.
    /// La sala real de jefe (11×11, X e Y de -5 a 5) es justo el caso que rompía a
    /// <c>RoomSector</c> — 11 no divide justo por 3 ni por 2 — así que es la que se usa acá para
    /// probar que la partición nueva no repite ese doble-cobro.
    /// </summary>
    [TestFixture]
    public class ThreatAreaShapeGridPartitionTests
    {
        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
        }

        /// <summary>
        /// Sala cuadrada centrada en el origen, de <c>2·half+1</c> de lado (half=5 ⇒ 11×11,
        /// X e Y de -5 a 5 — la sala real de jefe). Se arma a mano porque <c>NavGraph.Rect</c>
        /// sólo sabe hacer rectángulos 0-based.
        /// </summary>
        private void LoadCenteredRoom(int half)
        {
            var graph = new NavGraph();
            for (int x = -half; x <= half; x++)
            for (int y = -half; y <= half; y++)
                graph.AddNode(new NavNode(new GridCoord(x, y)));

            _grid.LoadRoom(graph);
        }

        [Test]
        public void ComputeGridPartition_ElevenByEleven_UnionOfAllCellsCoversTheWholeGrid()
        {
            // Arrange — mismas dimensiones (3 columnas × 2 filas) que rompían a RoomSector.
            LoadCenteredRoom(5);
            var covered = new HashSet<GridCoord>();

            // Act
            for (int cell = 1; cell <= 6; cell++)
                covered.UnionWith(ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 2, cellIndex: cell));

            // Assert
            var missing = new List<GridCoord>();
            foreach (var tile in ThreatAreaShape.RoomTiles(_grid))
                if (!covered.Contains(tile)) missing.Add(tile);

            Assert.IsEmpty(missing,
                "Hay casillas caminables que no pertenecen a ninguna celda de la partición: " +
                string.Join(", ", missing));
            Assert.AreEqual(121, covered.Count, "La sala 11×11 tiene 121 casillas caminables.");
        }

        [Test]
        public void ComputeGridPartition_ElevenByEleven_NoCoordBelongsToTwoCells()
        {
            // Arrange — la regresión que motiva toda la shape: RoomSector con este mismo 3×2
            // sobre esta misma sala deja 20 casillas en dos sectores a la vez.
            LoadCenteredRoom(5);
            var cells = new List<HashSet<GridCoord>>();
            for (int cell = 1; cell <= 6; cell++)
                cells.Add(ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 2, cellIndex: cell));

            // Act + Assert — pairwise: ninguna celda comparte una casilla con otra.
            for (int i = 0; i < cells.Count; i++)
            for (int j = i + 1; j < cells.Count; j++)
            {
                foreach (var tile in cells[i])
                    Assert.IsFalse(cells[j].Contains(tile),
                        $"{tile} está a la vez en la celda {i + 1} y la {j + 1}: la partición se solapa.");
            }

            // Y en total: la suma de los tamaños individuales tiene que ser igual al tamaño de
            // la unión — si hubiera solape, la suma sería mayor.
            int sum = 0;
            var union = new HashSet<GridCoord>();
            foreach (var cell in cells)
            {
                sum += cell.Count;
                union.UnionWith(cell);
            }
            Assert.AreEqual(union.Count, sum, "La suma de tamaños no coincide con la unión: hay casillas repetidas.");
        }

        [Test]
        public void ComputeGridPartition_TileAtOldRoomSectorSeam_BelongsToExactlyOneCell()
        {
            // Arrange — (2,0) es justo la costura reportada de RoomSector (columna 2, fila 0) en
            // esta sala 11×11. Acá tiene que caer en una sola celda.
            LoadCenteredRoom(5);
            var seamTile = new GridCoord(2, 0);

            // Act
            int owners = 0;
            for (int cell = 1; cell <= 6; cell++)
                if (ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 2, cellIndex: cell).Contains(seamTile))
                    owners++;

            // Assert
            Assert.AreEqual(1, owners, $"{seamTile} tendría que pertenecer a una sola celda, no a {owners}.");
        }

        [Test]
        public void ComputeGridPartition_UnevenExtent_ColumnBandSizesDifferByAtMostOne()
        {
            // Arrange — 11 entre 3 columnas no divide justo (4/4/3).
            LoadCenteredRoom(5);

            // Act — con una sola fila (rows=1) cada celda es directamente una banda de columna.
            var sizes = new List<int>();
            for (int column = 1; column <= 3; column++)
                sizes.Add(ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 1, cellIndex: column).Count / 11);

            // Assert — 3 anchos de columna (en casillas), separados por a lo sumo 1 entre sí.
            sizes.Sort();
            Assert.AreEqual(11, sizes[0] + sizes[1] + sizes[2], "Las 3 bandas de columna tienen que sumar el ancho total.");
            Assert.LessOrEqual(sizes[2] - sizes[0], 1, "Las bandas de columna difieren en más de 1 casilla entre sí.");
        }

        [Test]
        public void ComputeGridPartition_UnevenExtent_RowBandSizesDifferByAtMostOne()
        {
            // Arrange — 11 entre 2 filas no divide justo (6/5).
            LoadCenteredRoom(5);

            // Act — con una sola columna (columns=1) cada celda es directamente una banda de fila.
            var sizes = new List<int>();
            for (int row = 1; row <= 2; row++)
                sizes.Add(ThreatAreaShape.ComputeGridPartition(_grid, columns: 1, rows: 2, cellIndex: row).Count / 11);

            // Assert
            sizes.Sort();
            Assert.AreEqual(11, sizes[0] + sizes[1], "Las 2 bandas de fila tienen que sumar el alto total.");
            Assert.LessOrEqual(sizes[1] - sizes[0], 1, "Las bandas de fila difieren en más de 1 casilla entre sí.");
        }

        [Test]
        public void ComputeGridPartition_DivisibleExtent_AllBandsAreEqualSize()
        {
            // Arrange — 9×9 partido en 3×3: divide justo, ninguna banda debería llevarse un extra.
            LoadCenteredRoom(4);

            // Act + Assert
            for (int cell = 1; cell <= 9; cell++)
            {
                var tiles = ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 3, cellIndex: cell);
                Assert.AreEqual(9, tiles.Count, $"La celda {cell} debería medir 3×3 al dividir justo.");
            }
        }

        [Test]
        public void ComputeGridPartition_CellIndexOne_IsTheLowestColumnAndLowestRowBand()
        {
            // Arrange — cellIndex 1-based: columna = (cellIndex-1)%columns, fila = (cellIndex-1)/columns,
            // fila 0 la más cercana al borde de Y mínimo.
            LoadCenteredRoom(5);

            // Act
            var firstCell = ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 2, cellIndex: 1);

            // Assert — banda de columna 0 (X: -5..-2) × banda de fila 0 (Y: -5..0).
            Assert.IsTrue(firstCell.Contains(new GridCoord(-5, -5)));
            Assert.IsTrue(firstCell.Contains(new GridCoord(-2, 0)));
            Assert.IsFalse(firstCell.Contains(new GridCoord(-1, 0)), "La celda 1 no cruza a la banda de columna 1.");
            Assert.IsFalse(firstCell.Contains(new GridCoord(-5, 1)), "La celda 1 no cruza a la banda de fila 1.");
        }

        [Test]
        public void ComputeGridPartition_CellIndexOutOfRange_ReturnsEmpty()
        {
            LoadCenteredRoom(5);

            Assert.IsEmpty(ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 2, cellIndex: 0));
            Assert.IsEmpty(ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 2, cellIndex: 7));
            Assert.IsEmpty(ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 2, cellIndex: -1));
        }

        [Test]
        public void ComputeGridPartition_NonPositiveColumnsOrRows_ReturnsEmpty()
        {
            LoadCenteredRoom(5);

            Assert.IsEmpty(ThreatAreaShape.ComputeGridPartition(_grid, columns: 0, rows: 2, cellIndex: 1));
            Assert.IsEmpty(ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 0, cellIndex: 1));
            Assert.IsEmpty(ThreatAreaShape.ComputeGridPartition(_grid, columns: -1, rows: 2, cellIndex: 1));
        }

        [Test]
        public void ComputeGridPartition_NullGrid_ReturnsEmpty()
        {
            Assert.IsEmpty(ThreatAreaShape.ComputeGridPartition(null, columns: 3, rows: 2, cellIndex: 1));
        }

        [Test]
        public void ComputeGridPartition_EmptyGraph_ReturnsEmpty()
        {
            // _grid nunca cargó una sala (stub "infinito", sin bounds para partir).
            Assert.IsEmpty(ThreatAreaShape.ComputeGridPartition(_grid, columns: 3, rows: 2, cellIndex: 1));
        }

        [Test]
        public void Compute_GridPartitionShape_IsNotWiredThroughGenericCompute()
        {
            // Arrange — GridPartition necesita 3 parámetros (columnas, filas, índice) que no
            // entran en el `size` único de Compute, así que el switch genérico no tiene case
            // para esta shape a propósito: el caller (AINode_TelegraphMark) llama directo a
            // ComputeGridPartition, igual que ya hace con ScatteredSquares/DirectionalBand.
            LoadCenteredRoom(5);

            // Act
            var viaCompute = ThreatAreaShape.Compute(
                _grid, new GridCoord(0, 0), ThreatShape.GridPartition, size: 1, HalfRoomAxis.Vertical);

            // Assert
            Assert.IsEmpty(viaCompute, "GridPartition no debería resolver nada a través del Compute genérico.");
        }
    }
}
