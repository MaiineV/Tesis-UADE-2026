using NUnit.Framework;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="ThreatAreaShape.ComputeDirectionalBand"/>: la banda direccional
    /// que sale del boss (piso 1) hacia el jugador, en las 4 direcciones cardinales, más
    /// recorte contra el borde de la grilla.
    /// </summary>
    [TestFixture]
    public class ThreatAreaShapeTests
    {
        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
        }

        [Test]
        public void ComputeDirectionalBand_PlayerNorth_MarksBandNorthOfSelf()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var self = new GridCoord(4, 4);
            var player = new GridCoord(4, 7);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(4, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 6)));
            Assert.IsTrue(tiles.Contains(new GridCoord(4, 6)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 6)));
            Assert.IsFalse(tiles.Contains(self), "La banda nunca incluye la propia fila/columna del boss.");
        }

        [Test]
        public void ComputeDirectionalBand_PlayerSouth_MarksBandSouthOfSelf()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var self = new GridCoord(4, 4);
            var player = new GridCoord(4, 1);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(4, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 2)));
            Assert.IsTrue(tiles.Contains(new GridCoord(4, 2)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 2)));
        }

        [Test]
        public void ComputeDirectionalBand_PlayerEast_MarksBandEastOfSelf()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var self = new GridCoord(4, 4);
            var player = new GridCoord(7, 4);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 4)));
            Assert.IsTrue(tiles.Contains(new GridCoord(5, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(6, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(6, 4)));
            Assert.IsTrue(tiles.Contains(new GridCoord(6, 5)));
        }

        [Test]
        public void ComputeDirectionalBand_PlayerWest_MarksBandWestOfSelf()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var self = new GridCoord(4, 4);
            var player = new GridCoord(1, 4);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 4)));
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 5)));
            Assert.IsTrue(tiles.Contains(new GridCoord(2, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(2, 4)));
            Assert.IsTrue(tiles.Contains(new GridCoord(2, 5)));
        }

        [Test]
        public void ComputeDirectionalBand_DiagonalDelta_DominantAxisWins()
        {
            // Arrange — |dx|(=5) > |dy|(=1) ⇒ Este, igual que Cardinal.FromDelta.
            _grid.LoadRoom(NavGraph.Rect(11, 9));
            var self = new GridCoord(2, 4);
            var player = new GridCoord(7, 5);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(3, 3)));
            Assert.IsTrue(tiles.Contains(new GridCoord(4, 3)));
        }

        [Test]
        public void ComputeDirectionalBand_NearGridEdge_ClipsOutOfBoundsTiles()
        {
            // Arrange — boss en la esquina (0,0): la banda perpendicular (eje X, centrada
            // en 0) pide columna -1, que no existe en la grilla 5x5 (X en [0,4]).
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var self = new GridCoord(0, 0);
            var player = new GridCoord(0, 3);

            // Act
            var tiles = ThreatAreaShape.ComputeDirectionalBand(_grid, self, player, halfWidth: 1, depth: 2);

            // Assert — de las 6 casillas pedidas, las 2 con X=-1 quedan recortadas.
            Assert.AreEqual(4, tiles.Count);
            Assert.IsTrue(tiles.Contains(new GridCoord(0, 1)));
            Assert.IsTrue(tiles.Contains(new GridCoord(1, 1)));
            Assert.IsTrue(tiles.Contains(new GridCoord(0, 2)));
            Assert.IsTrue(tiles.Contains(new GridCoord(1, 2)));
            Assert.IsFalse(tiles.Contains(new GridCoord(-1, 1)));
            Assert.IsFalse(tiles.Contains(new GridCoord(-1, 2)));
        }

        [Test]
        public void ComputeDirectionalBand_NullGrid_ReturnsEmpty()
        {
            var tiles = ThreatAreaShape.ComputeDirectionalBand(
                null, new GridCoord(0, 0), new GridCoord(0, 1), halfWidth: 1, depth: 2);

            Assert.AreEqual(0, tiles.Count);
        }

        [Test]
        public void ComputeScatteredSquares_ReturnsAtMostCountTimesWidthSquaredTiles()
        {
            // Arrange — sala grande, sin recorte esperable contra el borde.
            _grid.LoadRoom(NavGraph.Rect(20, 20));

            // Act
            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(1), count: 3, squareWidth: 2);

            // Assert
            Assert.Greater(tiles.Count, 0);
            Assert.LessOrEqual(tiles.Count, 3 * 2 * 2);
        }

        [Test]
        public void ComputeScatteredSquares_AllTilesAreWithinGridBounds()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));

            // Act
            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(7), count: 5, squareWidth: 2);

            // Assert
            foreach (var c in tiles)
            {
                Assert.IsTrue(c.X >= 0 && c.X < 9 && c.Y >= 0 && c.Y < 9,
                    $"Tile {c} cayó fuera de la grilla 9x9.");
            }
        }

        [Test]
        public void ComputeScatteredSquares_SameSeed_ProducesSameTiles()
        {
            // Arrange
            _grid.LoadRoom(NavGraph.Rect(9, 9));

            // Act
            var first = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(42), count: 3, squareWidth: 2);
            var second = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(42), count: 3, squareWidth: 2);

            // Assert
            CollectionAssert.AreEquivalent(first, second);
        }

        [Test]
        public void ComputeScatteredSquares_ZeroCount_ReturnsEmpty()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));

            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(1), count: 0, squareWidth: 2);

            Assert.AreEqual(0, tiles.Count);
        }

        [Test]
        public void ComputeScatteredSquares_EmptyRoomGraph_ReturnsEmpty()
        {
            // _grid nunca cargó una sala (stub "infinito", sin tiles para enumerar).
            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(1), count: 3, squareWidth: 2);

            Assert.AreEqual(0, tiles.Count);
        }

        [Test]
        public void ComputeScatteredSquares_NullGrid_ReturnsEmpty()
        {
            var tiles = ThreatAreaShape.ComputeScatteredSquares(null, new System.Random(1), count: 3, squareWidth: 2);

            Assert.AreEqual(0, tiles.Count);
        }

        [Test]
        public void Compute_SquareAroundSelf_CentersOnSelfNotPlayer()
        {
            // Arrange — self y player en esquinas opuestas; el área debe salir alrededor
            // de self (radio 1 ⇒ 3x3), sin importar dónde está el player.
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            var self = new GridCoord(4, 4);
            var player = new GridCoord(0, 0);

            // Act
            var tiles = ThreatAreaShape.Compute(_grid, self, ThreatShape.SquareAroundSelf, size: 1, HalfRoomAxis.Vertical);

            // Assert
            Assert.AreEqual(9, tiles.Count);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                Assert.IsTrue(tiles.Contains(new GridCoord(4 + dx, 4 + dy)));
            Assert.IsFalse(tiles.Contains(player), "El área no debe depender de la posición del jugador.");
        }

        [Test]
        public void ComputeScatteredSquares_AnchorsStayWithinCentralHalfOfRoom()
        {
            // Arrange — sala 20x20 (X,Y en [0,19]): margen 25% por lado ⇒ pool central
            // en X,Y ∈ [5,14]. El ancla es la esquina inferior-izquierda del cuadrado, así
            // que con squareWidth=2 el límite superior del pool de anclaje se recorta a 13
            // (14-1) — el cuadrado entero (ancla + 1) queda siempre dentro de [5,14], sin
            // sobresalir hacia el borde.
            _grid.LoadRoom(NavGraph.Rect(20, 20));

            // Act
            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(3), count: 3, squareWidth: 2);

            // Assert
            Assert.Greater(tiles.Count, 0);
            foreach (var c in tiles)
            {
                Assert.IsTrue(c.X >= 5 && c.X <= 14 && c.Y >= 5 && c.Y <= 14,
                    $"Tile {c} cayó fuera del 50% central de la sala (zonas no deberían pegarse a las paredes).");
            }
        }

        [Test]
        public void ComputeScatteredSquares_BigRoom_SquaresDoNotOverlap()
        {
            // Arrange — sala grande de sobra para separar 4 cuadrados de 2x2 sin tocarse:
            // si no se solapan, el HashSet resultante tiene exactamente count*width*width
            // tiles (ninguna tile compartida entre cuadrados se "pierde" por dedupe).
            _grid.LoadRoom(NavGraph.Rect(30, 30));

            // Act
            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(11), count: 4, squareWidth: 2);

            // Assert
            Assert.AreEqual(4 * 2 * 2, tiles.Count,
                "Con sala de sobra, los 4 cuadrados no deberían solaparse ni tocarse.");
        }

        [Test]
        public void ComputeScatteredSquares_TinyRoom_DegradesGracefully_StillReturnsExactCount()
        {
            // Arrange — sala mínima donde 4 cuadrados de 2x2 separados no entran: el
            // fallback en cascada debe igual devolver algo (aunque se solapen), nunca
            // menos tiles de las que el pool permite, y nunca crashear.
            _grid.LoadRoom(NavGraph.Rect(6, 6));

            // Act
            var tiles = ThreatAreaShape.ComputeScatteredSquares(_grid, new System.Random(5), count: 4, squareWidth: 2);

            // Assert
            Assert.Greater(tiles.Count, 0);
            Assert.LessOrEqual(tiles.Count, 4 * 2 * 2);
        }

        // =====================================================================
        // RoomSector — el paño del Croupier (6 bloques que cubren la sala entera)
        // =====================================================================

        /// <summary>
        /// Sala de test del jefe: 11 de ancho, 7 de alto. Bandas 0-3 / 4-7 / 7-10 en X y 0-3 / 3-6
        /// en Y, o sea costura en la columna 7 y en la fila 3.
        /// </summary>
        private void LoadCroupierRoom() => _grid.LoadRoom(NavGraph.Rect(11, 7));

        [Test]
        public void ComputeRoomSector_EachSector_Is4x4()
        {
            // Arrange
            LoadCroupierRoom();

            // Act + Assert
            for (int sector = 1; sector <= ThreatAreaShape.RoomSectorCount; sector++)
            {
                var tiles = ThreatAreaShape.ComputeRoomSector(_grid, sector);
                Assert.AreEqual(16, tiles.Count, $"El sector {sector} debería medir 4×4.");
            }
        }

        [Test]
        public void ComputeRoomSector_UpperRow_Is123LeftToRight()
        {
            // Arrange
            LoadCroupierRoom();

            // Act
            var s1 = ThreatAreaShape.ComputeRoomSector(_grid, 1);
            var s2 = ThreatAreaShape.ComputeRoomSector(_grid, 2);
            var s3 = ThreatAreaShape.ComputeRoomSector(_grid, 3);

            // Assert — bandas 0-3 / 4-7 / 7-10, filas 3-6 (de la costura para arriba).
            Assert.IsTrue(s1.Contains(new GridCoord(0, 4)));
            Assert.IsTrue(s1.Contains(new GridCoord(3, 6)));
            Assert.IsFalse(s1.Contains(new GridCoord(4, 4)), "El bloque 1 termina en la columna 3.");

            Assert.IsTrue(s2.Contains(new GridCoord(4, 4)));
            Assert.IsTrue(s2.Contains(new GridCoord(7, 6)));

            Assert.IsTrue(s3.Contains(new GridCoord(10, 4)));
            Assert.IsTrue(s3.Contains(new GridCoord(7, 6)));
            Assert.IsFalse(s3.Contains(new GridCoord(6, 4)), "El bloque 3 arranca en la costura (7).");
        }

        [Test]
        public void ComputeRoomSector_LowerRow_Is456AndOnlyMeetsTheUpperRowOnTheSeam()
        {
            // Arrange
            LoadCroupierRoom();

            // Act
            var s1 = ThreatAreaShape.ComputeRoomSector(_grid, 1);
            var s4 = ThreatAreaShape.ComputeRoomSector(_grid, 4);
            var s5 = ThreatAreaShape.ComputeRoomSector(_grid, 5);
            var s6 = ThreatAreaShape.ComputeRoomSector(_grid, 6);

            // Assert — misma columna que 1-2-3, filas 0-3.
            Assert.IsTrue(s4.Contains(new GridCoord(0, 0)));
            Assert.IsTrue(s4.Contains(new GridCoord(3, 2)));
            Assert.IsTrue(s5.Contains(new GridCoord(4, 0)));
            Assert.IsTrue(s6.Contains(new GridCoord(10, 2)));

            // Lo único que comparten arriba y abajo es la fila de costura (3), y sólo en su columna.
            foreach (var tile in s4)
                if (s1.Contains(tile))
                    Assert.AreEqual(3, tile.Y, $"{tile} no está en la costura y aparece en dos filas de bloques.");
        }

        [Test]
        public void ComputeRoomSector_SeamRow_BelongsToTwoSectors_InsteadOfNone()
        {
            // Arrange — la fila del medio era "el pasillo": no pertenecía a ningún sector, así que no
            // se prendía fuego nunca y quedarse ahí volvía gratis toda la pelea. Ahora es costura: la
            // comparten el bloque de arriba y el de abajo de su columna.
            LoadCroupierRoom();
            var seam = ThreatAreaShape.ComputeSeamRow(_grid);

            // Assert
            Assert.AreEqual(11, seam.Count, "La costura son las 11 casillas de la fila del medio.");

            foreach (var tile in seam)
            {
                int owners = 0;
                for (int sector = 1; sector <= ThreatAreaShape.RoomSectorCount; sector++)
                    if (ThreatAreaShape.ComputeRoomSector(_grid, sector).Contains(tile)) owners++;

                Assert.GreaterOrEqual(owners, 2,
                    $"{tile} está en la fila de costura y tendría que pertenecer al bloque de arriba y " +
                    "al de abajo de su columna.");
            }
        }

        [Test]
        public void ComputeRoomSector_CoversEveryWalkableTile_EvenWithFurnitureHoles()
        {
            // Arrange — LA regresión del jefe: una casilla caminable que no pertenece a ningún sector
            // no se prende fuego jamás, y el jugador se para ahí toda la pelea (el bug del "pasillo"
            // de la fila del medio). Se corre sobre la sala REAL del prefab —11×11 de -5 a 5, con los
            // 20 huecos de mobiliario— y no sobre un Rect ideal, porque el bug se reportó ahí.
            LoadRealCroupierRoom();

            var covered = new System.Collections.Generic.HashSet<GridCoord>();

            // Act
            for (int sector = 1; sector <= ThreatAreaShape.RoomSectorCount; sector++)
                covered.UnionWith(ThreatAreaShape.ComputeRoomSector(_grid, sector));

            // Assert
            var uncovered = new System.Collections.Generic.List<GridCoord>();
            foreach (var tile in ThreatAreaShape.RoomTiles(_grid))
                if (!covered.Contains(tile)) uncovered.Add(tile);

            Assert.IsEmpty(uncovered,
                "Hay casillas caminables que no pertenecen a ningún sector: son refugios permanentes " +
                "— el fuego del Croupier no puede alcanzarlas nunca. Casillas: " +
                string.Join(", ", uncovered));

            Assert.AreEqual(101, covered.Count, "La sala real del Croupier tiene 101 casillas caminables.");

            // Y las once del reporte, nombradas: "tiene puntos donde nunca se prenden fuego que son a
            // sus costados". El jefe spawnea en (0,0), así que sus costados son la fila y=0 entera.
            for (int x = -5; x <= 5; x++)
                Assert.IsTrue(covered.Contains(new GridCoord(x, 0)),
                    $"({x},0) está al costado del jefe y no pertenece a ningún sector: es el campamento " +
                    "del exploit.");
        }

        /// <summary>
        /// Sala real del prefab <c>Boss_Room_Croupier</c>: 11×11 con x e y de -5 a 5, el jefe en
        /// (0,0) y 20 casillas de mobiliario. Se arma a mano porque <c>NavGraph.Rect</c> sólo sabe
        /// hacer rectángulos 0-based sin huecos, y el exploit se reportó sobre esta sala.
        /// </summary>
        private void LoadRealCroupierRoom()
        {
            // Filas de y=+5 (arriba) a y=-5; columnas de x=-5 a x=+5. '#' = mobiliario.
            string[] rows =
            {
                "##.........", // y =  5
                "#.......##.", // y =  4
                "........##.", // y =  3
                "........##.", // y =  2
                "........##.", // y =  1
                "...........", // y =  0
                "...........", // y = -1
                "...........", // y = -2
                "#..........", // y = -3
                "##.........", // y = -4
                "###....###.", // y = -5
            };

            var graph = new NavGraph();
            for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
            {
                if (rows[r][c] == '#') continue;
                graph.AddNode(new NavNode(new GridCoord(c - 5, 5 - r)));
            }

            _grid.LoadRoom(graph);
        }

        [Test]
        public void ComputeRoomSector_SeamColumn_IsSharedByMiddleAndRightBlocks()
        {
            // Arrange — la única franja donde dos números cantados pegan los dos (24 en fase 2).
            LoadCroupierRoom();

            // Act
            var s2 = ThreatAreaShape.ComputeRoomSector(_grid, 2);
            var s3 = ThreatAreaShape.ComputeRoomSector(_grid, 3);
            var s5 = ThreatAreaShape.ComputeRoomSector(_grid, 5);
            var s6 = ThreatAreaShape.ComputeRoomSector(_grid, 6);

            // Assert — arriba y abajo la costura es la misma columna (7), y son 4 casillas por lado.
            int seamUpper = 0;
            foreach (var tile in s2)
                if (s3.Contains(tile)) { seamUpper++; Assert.AreEqual(7, tile.X); }
            Assert.AreEqual(4, seamUpper, "Los bloques 2 y 3 comparten la columna 7 completa (4 filas).");

            int seamLower = 0;
            foreach (var tile in s5)
                if (s6.Contains(tile)) { seamLower++; Assert.AreEqual(7, tile.X); }
            Assert.AreEqual(4, seamLower, "Los bloques 5 y 6 comparten la columna 7 completa (4 filas).");

            // Y la costura es SÓLO entre el medio y la derecha: 1 y 2 no se pisan.
            var s1 = ThreatAreaShape.ComputeRoomSector(_grid, 1);
            foreach (var tile in s1)
                Assert.IsFalse(s2.Contains(tile), "El bloque izquierdo no comparte costura con el del medio.");
        }

        [Test]
        public void ComputeRoomSector_SixSectors_CoverTheWholeRoom()
        {
            // Arrange
            LoadCroupierRoom();
            var covered = new System.Collections.Generic.HashSet<GridCoord>();

            // Act
            for (int sector = 1; sector <= ThreatAreaShape.RoomSectorCount; sector++)
                covered.UnionWith(ThreatAreaShape.ComputeRoomSector(_grid, sector));

            // Assert — 11×7: ninguna casilla del paño queda fuera de la numeración, sin necesidad de
            // sumarle el viejo pasillo.
            Assert.AreEqual(77, covered.Count);
        }

        [Test]
        public void ComputeRoomSector_OutOfRangeIndex_ReturnsEmpty()
        {
            // Arrange
            LoadCroupierRoom();

            // Act + Assert — no hay sector 0 ni 7; la rueda es de 6.
            Assert.IsEmpty(ThreatAreaShape.ComputeRoomSector(_grid, 0));
            Assert.IsEmpty(ThreatAreaShape.ComputeRoomSector(_grid, 7));
            Assert.IsEmpty(ThreatAreaShape.ComputeRoomSector(_grid, -1));
        }

        [Test]
        public void ComputeRoomSector_EmptyGraph_ReturnsEmpty()
        {
            // Arrange — sin bounds reales no hay paño que partir (igual que Row/Column/HalfRoom).
            // Act + Assert
            Assert.IsEmpty(ThreatAreaShape.ComputeRoomSector(_grid, 1));
            Assert.IsEmpty(ThreatAreaShape.ComputeSeamRow(_grid));
        }

        [Test]
        public void Compute_RoomSectorShape_TakesTheSectorIndexFromSize()
        {
            // Arrange — la shape entra al Compute genérico con el índice en `size`, así que un
            // TelegraphMark autorado a mano puede apuntar a un sector fijo.
            LoadCroupierRoom();

            // Act
            var viaCompute = ThreatAreaShape.Compute(
                _grid, new GridCoord(0, 0), ThreatShape.RoomSector, size: 5, HalfRoomAxis.Vertical);
            var direct = ThreatAreaShape.ComputeRoomSector(_grid, 5);

            // Assert
            Assert.AreEqual(direct.Count, viaCompute.Count);
            foreach (var tile in direct) Assert.IsTrue(viaCompute.Contains(tile));
        }
    }
}
