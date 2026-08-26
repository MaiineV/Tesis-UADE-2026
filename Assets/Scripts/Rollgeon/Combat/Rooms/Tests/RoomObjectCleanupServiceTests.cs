using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.FSM;
using Rollgeon.Entities;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using UnityEngine;

namespace Rollgeon.Combat.Rooms.Tests
{
    /// <summary>
    /// Qué pasa con la mesa cuando la pelea termina. El barrido de fin de combate recorre el turn
    /// order y un objeto de sala con <c>HideFromTurnQueue</c> nunca entra ahí: matando al jefe con
    /// las bombas en pie quedaban paradas en la sala, con su casilla todavía ocupada.
    /// </summary>
    [TestFixture]
    public class RoomObjectCleanupServiceTests
    {
        private GridManager _grid;
        private AttributesManager _attributes;
        private SpyVisuals _visuals;
        private AIContext _context;
        private Guid _boss;
        private RoomObjectDefinitionSO _definition;

        private static readonly GridCoord Self = new GridCoord(5, 5);

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 11));

            _boss = Guid.NewGuid();
            _grid.Register(_boss, Self);

            _attributes = new AttributesManager();
            _visuals = new SpyVisuals();

            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            ServiceLocator.AddService<AttributesManager>(_attributes, ServiceScope.Global);
            ServiceLocator.AddService<IEntityVisualService>(_visuals, ServiceScope.Global);

            _definition = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            _definition.hideFlags = HideFlags.HideAndDontSave;
            _definition.Hp = 5;
            _definition.Blocks = true;
            _definition.HideFromTurnQueue = true;
            _definition.RespawnDelayTurns = 0;

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
            if (ServiceLocator.TryGetService<RoomObjectCleanupService>(out var cleanup)) cleanup?.Dispose();

            _attributes?.Dispose();
            if (_definition != null) UnityEngine.Object.DestroyImmediate(_definition);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private AINode_SpawnRoomObjects Node(int count) => new AINode_SpawnRoomObjects
        {
            Definition = _definition,
            Count = count,
            Pattern = AINode_SpawnRoomObjects.Placement.ScatteredFree,
            MinSpacing = 2,
        };

        private List<Guid> LiveGuids(AINode_SpawnRoomObjects node)
        {
            var guids = new List<Guid>();
            for (int i = 0; i < node.SlotCount; i++)
                if (node.TryGetSlot(i, out _, out var guid) && guid != Guid.Empty) guids.Add(guid);
            return guids;
        }

        [Test]
        public void WhenTheFightEnds_TheObjectsLeftStanding_LeaveTheRoom()
        {
            var node = Node(count: 4);
            node.Tick(_context);

            var placed = LiveGuids(node);
            Assert.AreEqual(4, placed.Count, "El escenario del test necesita las 4 en pie.");

            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory);

            foreach (var guid in placed)
            {
                Assert.IsTrue(_visuals.DespawnedGuids.Contains(guid),
                    "El objeto quedó parado en la sala después de terminada la pelea.");
                Assert.IsFalse(_grid.TryGetPosition(guid, out _),
                    "La casilla del objeto sigue ocupada: es un muro invisible en la sala ya limpia.");
            }
        }

        /// <summary>Nada que levantar en una sala sin objetos, y el jugador no se toca.</summary>
        [Test]
        public void WhenTheFightEnds_NothingElseIsSweptOut()
        {
            Node(count: 2).Tick(_context);

            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), CombatOutcome.Victory);

            Assert.IsFalse(_visuals.DespawnedGuids.Contains(_boss),
                "El barrido se llevó al jefe, que tiene su propio entierro.");
            Assert.IsTrue(_grid.TryGetPosition(_boss, out _));
        }

        /// <summary>
        /// Lo que el jugador rompió ya se fue por la vía de siempre. Anotarlo igual haría que el
        /// barrido le pisara la vida a una ranura que el nodo ya repuso con otro objeto.
        /// </summary>
        [Test]
        public void ABrokenObject_DropsOffTheList_WhenTheNodeCollectsIt()
        {
            var node = Node(count: 2);
            node.Tick(_context);

            var broken = LiveGuids(node)[0];
            _attributes.SetAttributeValue<Health, int>(broken, 0);
            node.Tick(_context);

            var cleanup = RoomObjectCleanupService.ResolveOrCreate();
            CollectionAssert.DoesNotContain(cleanup.Tracked, broken,
                "La bomba rota siguió anotada en el barrido de fin de combate.");
        }

        private sealed class SpyVisuals : IEntityVisualService
        {
            public HashSet<Guid> DespawnedGuids { get; } = new HashSet<Guid>();
            public EntityPawn SpawnHero(Guid guid, ClassHeroSO hero, GridCoord coord) => null;
            public EntityPawn SpawnEnemy(Guid guid, EnemyDataSO data, GridCoord coord) => null;
            public EntityPawn SpawnProp(Guid guid, GameObject prefab, GridCoord coord) => null;
            public void Despawn(Guid guid) => DespawnedGuids.Add(guid);
            public void DespawnAll() { }
            public bool TryGetPawn(Guid guid, out EntityPawn pawn) { pawn = null; return false; }
            public Vector3? TryGetWorldPosition(Guid entityId) => null;
            public System.Collections.IEnumerator WaitForMoveComplete(Guid entityId) => null;
        }
    }
}
