using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.Rooms.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_SpawnRoomObjects.MinSpacing"/>: la separación mínima (Chebyshev)
    /// entre ranuras de <see cref="AINode_SpawnRoomObjects.Placement.ScatteredFree"/>, y contra el
    /// jefe. El Croupier siembra 10 bombas sueltas — sin esto el sorteo pelado de hoy las apila
    /// pegadas entre sí y contra él.
    /// </summary>
    [TestFixture]
    public class AINode_SpawnRoomObjects_ScatterSpacingTests
    {
        private GridManager _grid;
        private AttributesManager _attributes;
        private AIContext _context;
        private Guid _boss;
        private RoomObjectDefinitionSO _definition;

        private static readonly GridCoord Self = new GridCoord(5, 5);

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 11));

            _boss = Guid.NewGuid();
            _grid.Register(_boss, Self);

            _attributes = new AttributesManager();

            _definition = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            _definition.Blocks = true;

            _context = new AIContext
            {
                SelfGuid = _boss,
                Grid = _grid,
                Attributes = _attributes,
                Rng = new System.Random(1234),
            };
        }

        [TearDown]
        public void TearDown()
        {
            _attributes.Dispose();
            UnityEngine.Object.DestroyImmediate(_definition);
            ServiceLocator.Clear();
        }

        private AINode_SpawnRoomObjects MakeNode(int count, int minSpacing) =>
            new AINode_SpawnRoomObjects
            {
                Definition = _definition,
                Count = count,
                Pattern = AINode_SpawnRoomObjects.Placement.ScatteredFree,
                MinSpacing = minSpacing,
            };

        private List<GridCoord> LiveCoords(AINode_SpawnRoomObjects node)
        {
            var coords = new List<GridCoord>();
            for (int i = 0; i < node.SlotCount; i++)
            {
                if (node.TryGetSlot(i, out var coord, out var guid) && guid != Guid.Empty)
                    coords.Add(coord);
            }
            return coords;
        }

        // ---- La separación se respeta -----------------------------------

        [Test]
        public void MinSpacingTwo_KeepsEveryPairAndTheBossAtLeastTwoTilesApart()
        {
            // Arrange — 10 bombas en una sala de sobra (11x11), como el Croupier.
            var node = MakeNode(10, minSpacing: 2);

            // Act
            node.Tick(_context);
            var coords = LiveCoords(node);

            // Assert
            Assert.AreEqual(10, coords.Count,
                "Con espacio de sobra las 10 bombas tienen que entrar todas: si esto falla antes de " +
                "llegar a chequear distancias, el problema es el sorteo, no la separación.");

            foreach (var c in coords)
                Assert.GreaterOrEqual(c.Chebyshev(Self), 2,
                    $"{c} queda pegada al jefe (Chebyshev < 2 de {Self}) — una bomba ahí se lee como " +
                    "que se la puso él mismo encima.");

            for (int i = 0; i < coords.Count; i++)
            {
                for (int j = i + 1; j < coords.Count; j++)
                {
                    Assert.GreaterOrEqual(coords[i].Chebyshev(coords[j]), 2,
                        $"{coords[i]} y {coords[j]} quedan a Chebyshev < 2 entre sí — dos bombas " +
                        "pegadas (o en diagonal) leen como una sola.");
                }
            }
        }

        [Test]
        public void MinSpacingTwo_RoomTooSmallForCount_ReturnsFewerInsteadOfThrowing()
        {
            // Arrange — sala 5x5, jefe en el centro: no entran 20 bombas separadas por 2.
            var smallGrid = new GridManager();
            smallGrid.LoadRoom(NavGraph.Rect(5, 5));
            var self = new GridCoord(2, 2);
            smallGrid.Register(_boss, self);
            _context.Grid = smallGrid;

            var node = MakeNode(20, minSpacing: 2);

            // Act
            AIResult result = default;
            Assert.DoesNotThrow(() => result = node.Tick(_context),
                "Un pool que se seca antes de juntar Count tiene que degradar en silencio, no tirar.");

            var coords = LiveCoords(node);

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.Less(coords.Count, 20,
                "La sala no da para 20 bombas separadas por 2 — el nodo tiene que devolver menos, no " +
                "inventar casillas fuera de la sala.");
            Assert.Greater(coords.Count, 0, "Algo tiene que entrar: una sala 5x5 da para varias.");

            for (int i = 0; i < coords.Count; i++)
            {
                Assert.GreaterOrEqual(coords[i].Chebyshev(self), 2);
                for (int j = i + 1; j < coords.Count; j++)
                    Assert.GreaterOrEqual(coords[i].Chebyshev(coords[j]), 2);
            }
        }

        // ---- El default no cambia nada -----------------------------------

        [Test]
        public void MinSpacingZero_AllowsSlotsTouchingTheBoss_SameAsBeforeTheFeature()
        {
            // Arrange — sala 3x3, jefe en el centro: las 8 casillas restantes tocan al jefe
            // (Chebyshev 1). Sin separación forzada (default) tienen que entrar las 8.
            var tinyGrid = new GridManager();
            tinyGrid.LoadRoom(NavGraph.Rect(3, 3));
            tinyGrid.Register(_boss, new GridCoord(1, 1));
            _context.Grid = tinyGrid;

            var node = MakeNode(8, minSpacing: 0);

            // Act
            node.Tick(_context);
            var coords = LiveCoords(node);

            // Assert
            Assert.AreEqual(8, coords.Count,
                "MinSpacing = 0 es el comportamiento de siempre: ScatteredFree no filtra por " +
                "distancia al jefe, así que las 8 casillas pegadas a él tienen que entrar todas.");
        }
    }
}
