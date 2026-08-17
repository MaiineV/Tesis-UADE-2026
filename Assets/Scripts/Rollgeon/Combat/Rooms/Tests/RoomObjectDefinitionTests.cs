using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.Rooms.Tests
{
    /// <summary>
    /// Hazard service fake: registra qué definición se activó y sobre qué casillas, sin overlays, sin
    /// rondas y sin eventos. Lo que interesa del nodo es qué le PIDE al servicio; lo que el servicio
    /// hace después ya lo cubren los tests de <see cref="HazardService"/>.
    /// </summary>
    internal sealed class FakeHazardService : IHazardService
    {
        public readonly List<HazardDefinitionSO> ActivatedDefinitions = new List<HazardDefinitionSO>();
        public readonly List<List<GridCoord>> ActivatedTiles = new List<List<GridCoord>>();

        public int ActivationCount => ActivatedDefinitions.Count;

        public void Activate(HazardDefinitionSO definition) => Record(definition, null);

        public Guid Activate(HazardDefinitionSO definition, IEnumerable<GridCoord> tiles)
        {
            Record(definition, tiles);
            return Guid.NewGuid();
        }

        public bool IsActive(HazardDefinitionSO definition) => ActivatedDefinitions.Contains(definition);

        public bool IsActive(Guid sourceId) => false;

        public bool TryGetHazardAt(GridCoord coord, out HazardInstanceInfo info)
        {
            info = default;
            return false;
        }

        public IEnumerable<HazardInstanceInfo> ActiveInstances() => Array.Empty<HazardInstanceInfo>();

        public void Deactivate(Guid instanceId) { }

        public void SkipNextTick(Guid instanceId) { }

        private void Record(HazardDefinitionSO definition, IEnumerable<GridCoord> tiles)
        {
            ActivatedDefinitions.Add(definition);
            ActivatedTiles.Add(tiles == null ? new List<GridCoord>() : new List<GridCoord>(tiles));
        }
    }

    /// <summary>
    /// Tests de <see cref="RoomObjectDefinitionSO"/>: los guards de la data, que son lo único con
    /// lógica en un data bag. Cada uno cubre una definición que un builder de editor puede escribir
    /// aunque el drawer del Inspector no la deje autorar a mano.
    /// </summary>
    [TestFixture]
    public class RoomObjectDefinitionTests
    {
        private RoomObjectDefinitionSO _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            _definition.name = "ReelDefinition";
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_definition);

        [Test]
        public void EffectiveDisplayName_FallsBackToAssetName_WhenNotAuthored()
        {
            _definition.DisplayName = "   ";

            Assert.AreEqual("ReelDefinition", _definition.EffectiveDisplayName);
        }

        [Test]
        public void EffectiveDisplayName_PrefersAuthoredName()
        {
            _definition.DisplayName = "Rodillo";

            Assert.AreEqual("Rodillo", _definition.EffectiveDisplayName);
        }

        [Test]
        public void EffectiveHp_FloorsAtOne_WhenAuthoredBelowOne()
        {
            _definition.Hp = 0;

            Assert.AreEqual(1, _definition.EffectiveHp,
                "Un objeto spawneado en 0 HP nace muerto: el spawner lo rompería y repondría sin fin.");
        }

        [Test]
        public void EffectiveHp_KeepsAuthoredValue()
        {
            _definition.Hp = 70;

            Assert.AreEqual(70, _definition.EffectiveHp);
        }

        [Test]
        public void Respawns_IsFalse_WhenDelayIsNegative()
        {
            _definition.RespawnDelayTurns = -1;

            Assert.IsFalse(_definition.Respawns);
        }

        [Test]
        public void Respawns_IsTrue_WhenDelayIsZero()
        {
            _definition.RespawnDelayTurns = 0;

            Assert.IsTrue(_definition.Respawns, "Delay 0 es 'vuelve enseguida', no 'no vuelve'.");
        }

        [Test]
        public void Defaults_BlockThePathAndStayOutOfTheTurnQueue()
        {
            Assert.IsTrue(_definition.Blocks);
            Assert.IsTrue(_definition.HideFromTurnQueue,
                "El default del tipo es objeto, no bicho: sin slot en la cola de turnos.");
        }
    }

    /// <summary>
    /// Tests de <see cref="AINode_SpawnRoomObjects"/>: coloca las ranuras según el patrón, mantiene
    /// los objetos vivos, deja el hazard de muerte donde cayó el roto y lo repone en la misma casilla.
    /// </summary>
    [TestFixture]
    public class AINode_SpawnRoomObjectsTests
    {
        private GridManager _grid;
        private AttributesManager _attributes;
        private InMemoryEntityRegistry _registry;
        private TurnOrderService _turnOrder;
        private FakeHazardService _hazards;
        private RoomObjectDefinitionSO _definition;
        private HazardDefinitionSO _fire;
        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _attributes = new AttributesManager();
            _registry = new InMemoryEntityRegistry();
            _turnOrder = new TurnOrderService();
            _hazards = new FakeHazardService();

            ServiceLocator.AddService<InMemoryEntityRegistry>(_registry);
            ServiceLocator.AddService<TurnOrderService>(_turnOrder);
            ServiceLocator.AddService<IHazardService>(_hazards);

            _definition = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            _definition.name = "ReelDefinition";
            _definition.Hp = 50;
            _definition.RespawnDelayTurns = 2;

            _fire = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _fire.name = "FireHazard";

            _boss = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _attributes.Dispose();
            UnityEngine.Object.DestroyImmediate(_definition);
            UnityEngine.Object.DestroyImmediate(_fire);
        }

        // --- Colocación --------------------------------------------------------------

        [Test]
        public void Tick_ExplicitCoords_OpensOneSlotPerAuthoredTile()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = ExplicitNode(new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1));

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(3, node.SlotCount);
        }

        [Test]
        public void Tick_FirstTick_FillsEverySlot()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = ExplicitNode(new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1));

            node.Tick(NewContext());

            for (int i = 0; i < node.SlotCount; i++)
            {
                Assert.IsTrue(node.TryGetSlot(i, out var coord, out var guid));
                Assert.AreNotEqual(Guid.Empty, guid, $"La ranura {coord} quedó vacía en el primer tick.");
                Assert.IsTrue(_attributes.IsRegistered(guid));
                Assert.AreEqual(50, _attributes.GetAttributeValue<Health, int>(guid));
            }
        }

        [Test]
        public void Tick_BlockingObject_TakesItsTile()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _definition.Blocks = true;
            var node = ExplicitNode(new GridCoord(2, 2));

            node.Tick(NewContext());

            Assert.IsTrue(node.TryGetSlot(0, out var coord, out var guid));
            Assert.IsTrue(_grid.IsOccupied(coord));
            Assert.IsTrue(_grid.TryGetOccupant(coord, out var occupant));
            Assert.AreEqual(guid, occupant);
        }

        [Test]
        public void Tick_NonBlockingObject_LeavesItsTileFree()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _definition.Blocks = false;
            var node = ExplicitNode(new GridCoord(2, 2));

            node.Tick(NewContext());

            Assert.IsTrue(node.TryGetSlot(0, out var coord, out var guid));
            Assert.AreNotEqual(Guid.Empty, guid, "El objeto existe aunque no ocupe la casilla.");
            Assert.IsTrue(_attributes.IsRegistered(guid), "Sigue siendo dañable: sólo no bloquea.");
            Assert.IsFalse(_grid.IsOccupied(coord));
        }

        [Test]
        public void Tick_HiddenFromTurnQueue_NeverAppendsToTurnOrder()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _definition.HideFromTurnQueue = true;
            var node = ExplicitNode(new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1));

            node.Tick(NewContext());

            Assert.AreEqual(0, _turnOrder.ParticipantCount,
                "Un objeto de sala no ocupa slot en la cola: ese es el punto del tipo.");
        }

        [Test]
        public void Tick_VisibleInTurnQueue_AppendsOneParticipantPerObject()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _definition.HideFromTurnQueue = false;
            var node = ExplicitNode(new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1));

            node.Tick(NewContext());

            Assert.AreEqual(3, _turnOrder.ParticipantCount);
        }

        [Test]
        public void Tick_ObjectsAlive_SpawnsNothingOnLaterTicks()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = ExplicitNode(new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1));

            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out _, out var firstGuid));

            for (int i = 0; i < 5; i++) Assert.AreEqual(AIResult.Succeeded, node.Tick(NewContext()));

            Assert.AreEqual(3, node.SlotCount, "No se abren ranuras nuevas.");
            Assert.IsTrue(node.TryGetSlot(0, out _, out var stillFirstGuid));
            Assert.AreEqual(firstGuid, stillFirstGuid, "El objeto vivo no se re-spawnea encima de sí mismo.");
        }

        // --- Rotura y reposición -----------------------------------------------------

        [Test]
        public void Tick_BrokenObject_FreesItsTileOnTheTickThatNoticesIt()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = ExplicitNode(new GridCoord(2, 2));
            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out var coord, out var guid));

            Break(guid);
            node.Tick(NewContext());

            Assert.IsFalse(_grid.IsOccupied(coord),
                "El objeto roto no puede seguir siendo un muro invisible en su casilla.");
            Assert.IsTrue(node.TryGetSlot(0, out _, out var afterGuid));
            Assert.AreEqual(Guid.Empty, afterGuid);
        }

        [Test]
        public void Tick_BrokenObject_ReturnsToTheSameTile_AfterRespawnDelay()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _definition.RespawnDelayTurns = 2;
            var node = ExplicitNode(new GridCoord(2, 2));
            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out var coord, out var brokenGuid));

            Break(brokenGuid);

            node.Tick(NewContext()); // Turno que detecta la rotura: la ranura queda vacía.
            Assert.IsTrue(node.TryGetSlot(0, out _, out var afterNotice));
            Assert.AreEqual(Guid.Empty, afterNotice, "Turno de espera 1: no repone todavía.");

            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out _, out var afterWait));
            Assert.AreEqual(Guid.Empty, afterWait, "Turno de espera 2: no repone todavía.");

            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out var backCoord, out var backGuid));
            Assert.AreNotEqual(Guid.Empty, backGuid);
            Assert.AreNotEqual(brokenGuid, backGuid, "Vuelve un objeto nuevo, no el cadáver.");
            Assert.AreEqual(coord, backCoord, "Vuelve alineado a su ranura original.");
            Assert.IsTrue(_grid.TryGetOccupant(coord, out var occupant));
            Assert.AreEqual(backGuid, occupant);
        }

        [Test]
        public void Tick_BrokenObject_WithZeroDelay_ReturnsOnTheSameTick()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _definition.RespawnDelayTurns = 0;
            var node = ExplicitNode(new GridCoord(2, 2));
            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out _, out var brokenGuid));

            Break(brokenGuid);
            node.Tick(NewContext());

            Assert.IsTrue(node.TryGetSlot(0, out _, out var backGuid));
            Assert.AreNotEqual(Guid.Empty, backGuid);
            Assert.AreNotEqual(brokenGuid, backGuid);
        }

        [Test]
        public void Tick_BrokenObject_WithNegativeDelay_NeverReturns()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _definition.RespawnDelayTurns = -1;
            var node = ExplicitNode(new GridCoord(2, 2));
            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out var coord, out var brokenGuid));

            Break(brokenGuid);
            for (int i = 0; i < 6; i++) node.Tick(NewContext());

            Assert.IsTrue(node.TryGetSlot(0, out _, out var afterGuid));
            Assert.AreEqual(Guid.Empty, afterGuid, "Delay negativo = roto es para siempre.");
            Assert.IsFalse(_grid.IsOccupied(coord), "El hueco que abrió el jugador queda abierto.");
        }

        [Test]
        public void Tick_BrokenObject_ActivatesOnDeathHazardOverItsTile()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _definition.OnDeathHazard = _fire;
            var node = ExplicitNode(new GridCoord(2, 2));
            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out var coord, out var guid));

            Break(guid);
            node.Tick(NewContext());

            Assert.AreEqual(1, _hazards.ActivationCount);
            Assert.AreSame(_fire, _hazards.ActivatedDefinitions[0]);
            CollectionAssert.AreEqual(new[] { coord }, _hazards.ActivatedTiles[0],
                "El fuego que deja el objeto roto va en SU casilla, no en la forma autorada del hazard.");
        }

        [Test]
        public void Tick_BrokenObject_WithoutOnDeathHazard_ActivatesNothing()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _definition.OnDeathHazard = null;
            var node = ExplicitNode(new GridCoord(2, 2));
            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out _, out var guid));

            Break(guid);
            node.Tick(NewContext());

            Assert.AreEqual(0, _hazards.ActivationCount);
        }

        [Test]
        public void Tick_RespawnTileOccupied_WaitsWithoutConsumingTheClock()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _definition.RespawnDelayTurns = 1;
            var slotCoord = new GridCoord(2, 2);
            var node = ExplicitNode(slotCoord);
            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out _, out var brokenGuid));

            Break(brokenGuid);
            node.Tick(NewContext()); // Detecta la rotura, libera la casilla y baja el reloj a 0.

            var player = Guid.NewGuid();
            _grid.Register(player, slotCoord);

            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out _, out var blockedGuid));
            Assert.AreEqual(Guid.Empty, blockedGuid, "No repone encima del jugador.");

            _grid.Unregister(player);

            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out _, out var backGuid));
            Assert.AreNotEqual(Guid.Empty, backGuid,
                "Con la casilla libre repone al turno siguiente: la espera no acumuló deuda extra.");
        }

        // --- El contrato con la mano de La Generala -----------------------------------

        [Test]
        public void Tick_SpawnedObjects_CountAsAlliesOfTheirOwner()
        {
            // Arrange — es lo único que la migración de la mesa de La Generala puede romper en
            // silencio: AINode_RollHand con SizeSource = AliveAllies resuelve el tamaño de su mano
            // por IEntityQueryService, que itera AttributesManager.EnumerateEntries() y trata como
            // aliado a toda entidad registrada que no sea el player. Si los objetos de sala dejaran
            // de registrarse ahí, la jefa tiraría cero dados y su ataque desaparecería sin que
            // ninguna excepción lo delate.
            _grid.LoadRoom(NavGraph.Rect(7, 7));
            _grid.Register(_boss, new GridCoord(3, 3));
            _definition.HideFromTurnQueue = true;

            ServiceLocator.AddService<AttributesManager>(_attributes);
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService());

            var node = new AINode_SpawnRoomObjects
            {
                Definition = _definition,
                Count = 5,
                Pattern = AINode_SpawnRoomObjects.Placement.RingAroundSelf,
            };

            // Act
            Assert.AreEqual(AIResult.Succeeded, node.Tick(NewContext()));

            // Assert
            var allies = new List<Guid>();
            foreach (var ally in new EntityQueryService().GetAllAlliesOf(_boss))
            {
                var hp = _attributes.GetAttribute<Health>(ally.Guid);
                if (hp != null && hp.Value > 0) allies.Add(ally.Guid);
            }

            Assert.AreEqual(5, allies.Count,
                "Los cinco objetos tienen que contar como aliados vivos del jefe aunque estén fuera " +
                "de la cola de turnos: HideFromTurnQueue les saca el slot, no el registro.");
        }

        [Test]
        public void Tick_BrokenObject_StopsCountingAsAnAlly_Immediately()
        {
            // Arrange — y la cuenta tiene que bajar en el turno del jugador, no en el del jefe: la
            // mano se arma con los dados vivos EN EL MOMENTO de tirar.
            _grid.LoadRoom(NavGraph.Rect(7, 7));
            _grid.Register(_boss, new GridCoord(3, 3));
            ServiceLocator.AddService<AttributesManager>(_attributes);
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService());

            var node = new AINode_SpawnRoomObjects
            {
                Definition = _definition,
                Count = 5,
                Pattern = AINode_SpawnRoomObjects.Placement.RingAroundSelf,
            };
            node.Tick(NewContext());
            Assert.IsTrue(node.TryGetSlot(0, out _, out var victim));

            // Act — el jugador le rompe uno; el árbol del jefe todavía no volvió a tickear.
            Break(victim);

            // Assert
            int alive = 0;
            foreach (var ally in new EntityQueryService().GetAllAlliesOf(_boss))
            {
                var hp = _attributes.GetAttribute<Health>(ally.Guid);
                if (hp != null && hp.Value > 0) alive++;
            }

            Assert.AreEqual(4, alive,
                "Romper un dado le tiene que borrar una categoría ya, sin esperar su turno.");
        }

        /// <summary>
        /// Sólo existe para que <see cref="EntityQueryService"/> pueda clasificar facciones: sin
        /// player conocido devuelve listas vacías. Convención del repo — cada fixture declara el suyo.
        /// </summary>
        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }

        // --- Patrones ----------------------------------------------------------------

        [Test]
        public void Tick_RowNextToSelf_AlignsTheRowBesideTheBoss()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _grid.Register(_boss, new GridCoord(2, 4)); // Contra la pared norte.
            var node = new AINode_SpawnRoomObjects
            {
                Definition = _definition,
                Count = 3,
                Pattern = AINode_SpawnRoomObjects.Placement.RowNextToSelf,
                Side = AINode_SpawnRoomObjects.RowSide.Auto,
            };

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(3, node.SlotCount);
            for (int i = 0; i < node.SlotCount; i++)
            {
                Assert.IsTrue(node.TryGetSlot(i, out var coord, out _));
                Assert.AreEqual(3, coord.Y, "Auto elige el lado que no da a la pared y alinea la fila ahí.");
            }
        }

        [Test]
        public void Tick_RingAroundSelf_PlacesEveryObjectNextToTheBoss()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var bossCoord = new GridCoord(2, 2);
            _grid.Register(_boss, bossCoord);
            var node = new AINode_SpawnRoomObjects
            {
                Definition = _definition,
                Count = 5,
                Pattern = AINode_SpawnRoomObjects.Placement.RingAroundSelf,
            };

            node.Tick(NewContext());

            Assert.AreEqual(5, node.SlotCount);
            for (int i = 0; i < node.SlotCount; i++)
            {
                Assert.IsTrue(node.TryGetSlot(i, out var coord, out _));
                Assert.AreEqual(1, coord.Chebyshev(bossCoord),
                    $"La ranura {coord} no está en el anillo del jefe.");
            }
        }

        // --- Bordes ------------------------------------------------------------------

        [Test]
        public void Tick_NoDefinition_ReturnsFailed()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = new AINode_SpawnRoomObjects { Definition = null, Count = 3 };

            Assert.AreEqual(AIResult.Failed, node.Tick(NewContext()));
        }

        [Test]
        public void Tick_NullGrid_ReturnsFailed()
        {
            var node = ExplicitNode(new GridCoord(2, 2));
            var ctx = new AIContext { SelfGuid = _boss, Grid = null, Attributes = _attributes };

            Assert.AreEqual(AIResult.Failed, node.Tick(ctx));
            Assert.AreEqual(0, node.SlotCount);
        }

        [Test]
        public void Tick_PatternFindsNoValidTile_ReturnsFailedAndRetriesLater()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = ExplicitNode(new GridCoord(99, 99));

            Assert.AreEqual(AIResult.Failed, node.Tick(NewContext()));
            Assert.AreEqual(0, node.SlotCount, "Sin ranuras resueltas el nodo reintenta el próximo turno.");
        }

        [Test]
        public void Tick_RuntimeStateResetsForFreshCombat()
        {
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            var node = ExplicitNode(new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1));
            node.Tick(NewContext());
            Assert.AreEqual(3, node.SlotCount);

            // Combate nuevo = copia deep del árbol (mismo path que EnemyDataSO.CreateRuntimeAIRoot).
            var fresh = SerializationUtility.CreateCopy(node) as AINode_SpawnRoomObjects;
            Assert.IsNotNull(fresh);
            Assert.AreEqual(0, fresh.SlotCount, "El clon runtime no hereda las ranuras del combate previo.");

            _grid.LoadRoom(NavGraph.Rect(5, 5)); // Sala nueva: ocupancia limpia.

            fresh.Tick(NewContext());

            Assert.AreEqual(3, fresh.SlotCount);
            for (int i = 0; i < fresh.SlotCount; i++)
            {
                Assert.IsTrue(fresh.TryGetSlot(i, out _, out var guid));
                Assert.AreNotEqual(Guid.Empty, guid);
            }
        }

        // --- Helpers -----------------------------------------------------------------

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            Grid = _grid,
            Attributes = _attributes,
            Rng = new System.Random(1),
        };

        private AINode_SpawnRoomObjects ExplicitNode(params GridCoord[] coords) => new AINode_SpawnRoomObjects
        {
            Definition = _definition,
            Count = coords.Length,
            Pattern = AINode_SpawnRoomObjects.Placement.ExplicitCoords,
            Coords = new List<GridCoord>(coords),
        };

        /// <summary>
        /// Rompe el objeto espejando el entierro de <c>CombatDeathWatcher</c> en lo único que el nodo
        /// mira: Health en 0. Deja a propósito el registro del grid en pie para probar que el nodo
        /// libera la casilla por su cuenta (el watcher no corre en EditMode).
        /// </summary>
        private void Break(Guid guid) => _attributes.SetAttributeValue<Health, int>(guid, 0);
    }
}
