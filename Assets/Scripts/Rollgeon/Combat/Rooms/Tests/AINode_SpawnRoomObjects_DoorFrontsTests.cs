using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.Rooms.Tests
{
    /// <summary>
    /// Tests del merge de <see cref="AINode_SpawnRoomObjects.Placement.DoorFronts"/>: las cuatro
    /// tiles frente a puerta primero, en orden de sala, y lo que sobra de <c>Count</c> al anillo
    /// de <see cref="AINode_SpawnRoomObjects.Placement.RingAroundSelf"/>.
    /// </summary>
    /// <remarks>
    /// Se prueba contra el seam interno <c>BuildDoorFrontSlots</c> — leer las puertas de un
    /// <c>RoomLayout</c> real necesita un prefab de sala instanciado (no EditMode-testeable), pero
    /// el merge/orden/presupuesto/dedupe que puede romperse en silencio no depende del engine.
    /// </remarks>
    [TestFixture]
    public class AINode_SpawnRoomObjects_DoorFrontsTests
    {
        private GridManager _grid;
        private AIContext _context;
        private Guid _boss;
        private RoomObjectDefinitionSO _definition;

        // Sala 11x11 (0..10), jefe en el centro (5,5) — misma forma que la sala real de La
        // Generala (11x11, jefe en el origen), sólo que 0-based en vez de centrada en cero.
        private static readonly GridCoord Self = new GridCoord(5, 5);

        // Cuatro puertas a distancia 5 del jefe, orden N/S/E/W — el mismo orden en el que
        // RoomLayout.DoorSlots las autora.
        private static readonly List<GridCoord> FourDoorFronts = new List<GridCoord>
        {
            new GridCoord(5, 10), // Norte
            new GridCoord(5, 0),  // Sur
            new GridCoord(10, 5), // Este
            new GridCoord(0, 5),  // Oeste
        };

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 11));

            _boss = Guid.NewGuid();
            _grid.Register(_boss, Self);

            _definition = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            _definition.Blocks = true;

            _context = new AIContext { SelfGuid = _boss, Grid = _grid };
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_definition);
        }

        private AINode_SpawnRoomObjects MakeNode(int count) =>
            new AINode_SpawnRoomObjects { Definition = _definition, Count = count };

        // ---- El reparto base -----------------------------------------------

        [Test]
        public void FourDoorsAndCountFive_PlacesAllFourFrontsPlusOneFromTheRing()
        {
            // Arrange — 4 puertas + Count = 5: el caso de La Generala (cinco dados, cuatro salidas).
            var node = MakeNode(5);

            // Act
            var result = node.BuildDoorFrontSlots(FourDoorFronts, _context, _grid);

            // Assert
            Assert.AreEqual(5, result.Count,
                "Con 4 puertas y Count=5 tienen que salir los 5 dados: si faltara alguno, un dado " +
                "quedaría sin colocar y el jugador vería menos objetos de los que la definición pide.");

            for (int i = 0; i < FourDoorFronts.Count; i++)
                Assert.AreEqual(FourDoorFronts[i], result[i],
                    $"La tile frente a puerta {FourDoorFronts[i]} tiene que ir en el índice {i}: si el " +
                    "orden de sala no se respeta, dos runs del mismo jefe reparten los dados distinto.");

            Assert.AreEqual(new GridCoord(4, 4), result[4],
                "El quinto dado (el que sobra tras las 4 puertas) tiene que salir del anillo de " +
                "RingAroundSelf, pegado al jefe — es el que le cuesta el cubilete al jugador.");
        }

        [Test]
        public void OneDoorFrontBlocked_SkipsIt_AndTheRingFillsTheGap()
        {
            // Arrange — alguien (otro objeto de sala, un refuerzo) ya ocupa la puerta Oeste.
            var blocker = Guid.NewGuid();
            _grid.Register(blocker, new GridCoord(0, 5));
            var node = MakeNode(5);

            // Act
            var result = node.BuildDoorFrontSlots(FourDoorFronts, _context, _grid);

            // Assert
            Assert.AreEqual(5, result.Count,
                "Una puerta bloqueada no puede bajar la cuenta total de objetos — el resto se " +
                "reparte hasta juntar los 5 igual, o el jefe sale a la pelea con menos dados que los " +
                "que la definición pide.");
            Assert.IsFalse(result.Contains(new GridCoord(0, 5)),
                "La tile bloqueada no puede terminar en el resultado: ahí ya hay algo parado.");
            Assert.AreEqual(new GridCoord(5, 10), result[0]);
            Assert.AreEqual(new GridCoord(5, 0), result[1]);
            Assert.AreEqual(new GridCoord(10, 5), result[2]);
            CollectionAssert.AreEqual(
                new[] { new GridCoord(4, 4), new GridCoord(4, 5) },
                new[] { result[3], result[4] },
                "Con una de las cuatro puertas bloqueada sobran DOS dados para el anillo, no uno: si " +
                "el presupuesto del anillo se calculara sobre la cantidad de puertas autoradas en vez " +
                "de sobre las que entraron de verdad, el dado de la puerta bloqueada se perdería.");
        }

        // ---- La degradación que protege lo que ya existía -------------------

        [Test]
        public void NoDoors_DegradesToTheFullRing()
        {
            // Arrange — sala sin RoomLayout (o sin IDungeonService): el patrón no puede tener menos
            // objetos que RingAroundSelf sólo porque no encontró puertas.
            var node = MakeNode(3);

            // Act
            var withDoors = node.BuildDoorFrontSlots(new List<GridCoord>(), _context, _grid);
            var pureRing = node.BuildDoorFrontSlots(null, _context, _grid);

            // Assert
            Assert.AreEqual(3, withDoors.Count,
                "Sin puertas el patrón tiene que dar el mismo resultado que pedir RingAroundSelf " +
                "directo — degradar a 'nada' dejaría al jefe sin sus objetos en cualquier sala sin " +
                "RoomLayout (fixtures de test incluidos).");
            CollectionAssert.AreEqual(pureRing, withDoors,
                "Lista vacía y null tienen que degradar exactamente igual: ninguna de las dos trae " +
                "una puerta real.");

            var expectedRing = new List<GridCoord> { new GridCoord(4, 4), new GridCoord(4, 5), new GridCoord(4, 6) };
            CollectionAssert.AreEqual(expectedRing, withDoors,
                "El anillo alrededor del jefe tiene un orden fijo (radio creciente); si esto cambia, " +
                "el fallback ya no es 'el mismo RingAroundSelf de siempre'.");
        }

        // ---- El tope ---------------------------------------------------------

        [Test]
        public void CountSmallerThanDoorCount_NeverOverflows()
        {
            // Arrange — 4 puertas pero la definición sólo abre 2 ranuras.
            var node = MakeNode(2);

            // Act
            var result = node.BuildDoorFrontSlots(FourDoorFronts, _context, _grid);

            // Assert
            Assert.AreEqual(2, result.Count,
                "Count manda el techo siempre: de vuelta más objetos que Count sería spawnear dados " +
                "que la definición nunca pidió.");
            Assert.AreEqual(new GridCoord(5, 10), result[0]);
            Assert.AreEqual(new GridCoord(5, 0), result[1]);
        }

        // ---- Sin pisarse -----------------------------------------------------

        [Test]
        public void DoorTileAndRingTile_NeverCollide_InTheResult()
        {
            // Arrange — una puerta autorada justo donde también cae la primera tile del anillo:
            // sin dedupe cross-fuente el mismo dado terminaría "puesto" en dos ranuras a la vez.
            var overlappingDoor = new List<GridCoord> { new GridCoord(4, 4) };
            var node = MakeNode(2);

            // Act
            var result = node.BuildDoorFrontSlots(overlappingDoor, _context, _grid);

            // Assert
            var seen = new HashSet<GridCoord>();
            foreach (var c in result)
                Assert.IsTrue(seen.Add(c),
                    $"{c} aparece más de una vez en el resultado: sería el mismo dado ocupando dos " +
                    "casillas a la vez, algo que no puede pasar en la mesa real.");
        }

        // ---- Determinismo ------------------------------------------------------

        [Test]
        public void SameInput_TwiceInARow_ProducesTheSameOrderedResult()
        {
            // Arrange
            var node = MakeNode(5);

            // Act
            var first = node.BuildDoorFrontSlots(FourDoorFronts, _context, _grid);
            var second = node.BuildDoorFrontSlots(FourDoorFronts, _context, _grid);

            // Assert — si dos llamadas con el mismo estado de sala dieran layouts distintos, la
            // mesa de un jefe se reordenaría sola entre turnos sin que nada en el diseño lo pida.
            CollectionAssert.AreEqual(first, second,
                "Mismo jefe, misma sala, mismas puertas: el reparto tiene que salir idéntico las dos veces.");
        }
    }
}
