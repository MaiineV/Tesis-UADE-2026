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
    /// Tests de <see cref="AINode_SpawnRoomObjects.ResolveSlotsEachSpawn"/>: apagado (default), el
    /// objeto repuesto vuelve a la MISMA ranura — el comportamiento que necesita La Generala para sus
    /// dados. Prendido, cada reposición re-sortea contra <see cref="AINode_SpawnRoomObjects.Pattern"/>
    /// — lo que va a necesitar la ola de bombas del Croupier.
    /// </summary>
    [TestFixture]
    public class AINode_SpawnRoomObjects_ResolveSlotsEachSpawnTests
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
            _definition.RespawnDelayTurns = 0; // repone en el mismo Tick en el que se rompió.

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

        private void Kill(Guid guid) => _attributes.SetAttributeValue<Health, int>(guid, 0);

        private HashSet<GridCoord> LiveCoords(AINode_SpawnRoomObjects node)
        {
            var coords = new HashSet<GridCoord>();
            for (int i = 0; i < node.SlotCount; i++)
            {
                if (node.TryGetSlot(i, out var coord, out var guid) && guid != Guid.Empty)
                    coords.Add(coord);
            }
            return coords;
        }

        // ---- Default: la memoria de La Generala sigue intacta -----------

        [Test]
        public void Default_RespawnedObject_ReturnsToTheExactSameSlot()
        {
            // Arrange
            var node = new AINode_SpawnRoomObjects
            {
                Definition = _definition,
                Count = 3,
                Pattern = AINode_SpawnRoomObjects.Placement.RowNextToSelf,
                Side = AINode_SpawnRoomObjects.RowSide.Down,
            };

            node.Tick(_context);
            Assert.IsTrue(node.TryGetSlot(0, out var originalCoord, out var originalGuid));
            Assert.AreNotEqual(Guid.Empty, originalGuid);

            // Act — se rompe el objeto y el tick siguiente lo repone.
            Kill(originalGuid);
            node.Tick(_context);

            // Assert
            Assert.IsTrue(node.TryGetSlot(0, out var respawnCoord, out var respawnGuid));
            Assert.AreEqual(originalCoord, respawnCoord,
                "Sin ResolveSlotsEachSpawn el objeto repuesto tiene que volver exactamente donde " +
                "estaba: es la mecánica que sostiene los dados de La Generala.");
            Assert.AreNotEqual(Guid.Empty, respawnGuid);
            Assert.AreNotEqual(originalGuid, respawnGuid,
                "El guid roto no puede reciclarse — el repuesto es un objeto nuevo.");
        }

        // ---- Prendido: cada ola re-sortea ---------------------------------

        [Test]
        public void ResolveSlotsEachSpawnTrue_SecondWave_PicksADifferentSetOfTiles()
        {
            // Arrange
            var node = new AINode_SpawnRoomObjects
            {
                Definition = _definition,
                Count = 5,
                Pattern = AINode_SpawnRoomObjects.Placement.ScatteredFree,
                ResolveSlotsEachSpawn = true,
            };

            node.Tick(_context);
            var firstWave = LiveCoords(node);
            Assert.AreEqual(5, firstWave.Count);

            // Act — muere la ola entera y el siguiente tick la repone toda junta.
            var guidsToKill = new List<Guid>();
            for (int i = 0; i < node.SlotCount; i++)
            {
                node.TryGetSlot(i, out _, out var guid);
                guidsToKill.Add(guid);
            }
            foreach (var guid in guidsToKill) Kill(guid);

            node.Tick(_context);
            var secondWave = LiveCoords(node);

            // Assert
            Assert.AreEqual(5, secondWave.Count);
            Assert.IsFalse(secondWave.SetEquals(firstWave),
                "Con ResolveSlotsEachSpawn = true la segunda ola tiene que re-sortear contra Pattern " +
                "en vez de volver a las casillas que tenía la primera — mismo Rng sembrado, dos " +
                "resultados distintos.");
        }
    }
}
