using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities.Behaviors;
using Rollgeon.GameCamera;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Exploration.Tests
{
    [TestFixture]
    public class ExplorationBehaviorServiceTests
    {
        private StubPlayerService _playerService;
        private StubSelectionController _selectionController;
        private StubGridManager _gridManager;
        private ExplorationBehaviorService _service;
        private ClassHeroSO _heroSO;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _playerGuid = Guid.NewGuid();

            _heroSO = ScriptableObject.CreateInstance<ClassHeroSO>();
            foreach (var slot in new[] { HeroBehaviorSlot.Movement, HeroBehaviorSlot.BaseAttack, HeroBehaviorSlot.ClassSkill, HeroBehaviorSlot.Healing })
                _heroSO.PhaseBehaviors.Add(new HeroActionBehavior { IsBaseBehavior = true, Slot = slot, AllowedPhases = GamePhaseMask.Combat });

            _playerService = new StubPlayerService
            {
                PlayerGuid = _playerGuid,
                CurrentHero = _heroSO,
            };
            ServiceLocator.AddService<IPlayerService>(_playerService, ServiceScope.Global);

            _selectionController = new StubSelectionController();
            ServiceLocator.AddService<ISelectionController>(_selectionController, ServiceScope.Global);


            _gridManager = new StubGridManager();
            _gridManager.Positions[_playerGuid] = new GridCoord(2, 2);
            ServiceLocator.AddService<IGridManager>(_gridManager, ServiceScope.Global);

            _service = ExplorationBehaviorService.CreateAndRegister();
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            if (_heroSO != null)
                UnityEngine.Object.DestroyImmediate(_heroSO);
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void IsActive_FalseBeforeExplorationPhase()
        {
            Assert.IsFalse(_service.IsActive);
        }

        [Test]
        public void IsActive_TrueAfterExplorationPhaseEnter()
        {
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);
            Assert.IsTrue(_service.IsActive);
        }

        [Test]
        public void IsActive_FalseAfterExplorationPhaseExit()
        {
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);
            EventManager.Trigger(EventName.OnPhaseExit, GamePhase.Exploration);
            Assert.IsFalse(_service.IsActive);
        }

        [Test]
        public void OnBehaviorSelected_WhenInactive_DoesNothing()
        {
            AddExplorationMovement();
            _service.OnBehaviorSelected(0);
            Assert.IsFalse(_selectionController.SelectionStarted);
        }

        [Test]
        public void OnBehaviorSelected_InvalidIndex_DoesNothing()
        {
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);
            _service.OnBehaviorSelected(99);
            Assert.IsFalse(_selectionController.SelectionStarted);
        }

        [Test]
        public void OnBehaviorSelected_WithSelection_BeginsSelection()
        {
            AddExplorationMovement();
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);

            _service.OnBehaviorSelected(0);

            Assert.IsTrue(_selectionController.SelectionStarted);
        }

        [Test]
        public void OnBehaviorSelected_ExplorationIsFree_NoResourceServiceNeeded()
        {
            // Feature#0050: las acciones de exploración son gratis — no hay pool
            // de rolls fuera de combate ni servicio de recursos que consultar.
            AddExplorationMovement();
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);

            _service.OnBehaviorSelected(0);

            Assert.IsTrue(_selectionController.SelectionStarted);
        }

        [Test]
        public void OnTutorialActionUnlocked_MovementSlot_DoesNotThrowWithoutGate()
        {
            // Arrange — BUG-068: el service escucha el unlock del tutorial para
            // re-armar el click-to-move. Fuera del tutorial (sin gate registrado) y
            // fuera de exploración, el handler degrada a no-op.
            // (Sin OnPhaseEnter: _state = Inactive.)

            // Act / Assert
            Assert.DoesNotThrow(() => EventManager.Trigger(
                EventName.OnTutorialActionUnlocked, HeroBehaviorSlot.Movement));
        }

        [Test]
        public void OnTutorialActionUnlocked_NonMovementSlot_IsIgnored()
        {
            // Arrange
            AddExplorationMovement();
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);
            _selectionController.SelectionStarted = false;

            // Act — un unlock de otra acción no debe re-armar el movimiento.
            EventManager.Trigger(EventName.OnTutorialActionUnlocked, HeroBehaviorSlot.Healing);

            // Assert
            Assert.IsFalse(_selectionController.SelectionStarted);
        }

        [Test]
        public void CancelSelection_ReturnsToIdle()
        {
            AddExplorationMovement();
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);

            _service.OnBehaviorSelected(0);
            Assert.IsTrue(_selectionController.SelectionStarted);

            _service.CancelSelection();
            Assert.IsTrue(_service.IsActive);
            Assert.IsTrue(_selectionController.CancelCalled);
        }

        // -------------------------------------------------------------------------
        // Guard "click en la casilla propia" (bug de puerta encadenada): el spawn al
        // entrar a una sala deja al player SOBRE la casilla frente-a-puerta; clickear
        // esa misma casilla cruzaba la puerta al instante. Ahora es no-op.
        // -------------------------------------------------------------------------

        [Test]
        public void OnSelectionCompleted_ClickOnOwnTile_DoesNotExecuteBehavior()
        {
            // Arrange — player parado en (2,2); clickea (2,2).
            var move = AddExplorationMovement();
            var effect = (FakeMoveEffect)move.Effects[0].Effects[0];
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);
            _service.OnBehaviorSelected(0);

            // Act
            _selectionController.SimulateSelectionDone(new TargetSelectionResult
            {
                WasCompleted = true,
                SelectedTargets = new List<TargetRef> { TargetRef.At(new GridCoord(2, 2)) },
            });

            // Assert — no-op: ni el movimiento ni (aguas abajo) el cruce de puerta.
            Assert.AreEqual(0, effect.ApplyCalls,
                "Clickear la casilla en la que ya estás parado no debe ejecutar el behavior.");
        }

        [Test]
        public void OnSelectionCompleted_ClickOnOtherTile_ExecutesBehavior()
        {
            // Arrange — player en (2,2); clickea (3,2).
            var move = AddExplorationMovement();
            var effect = (FakeMoveEffect)move.Effects[0].Effects[0];
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);
            _service.OnBehaviorSelected(0);

            // Act
            _selectionController.SimulateSelectionDone(new TargetSelectionResult
            {
                WasCompleted = true,
                SelectedTargets = new List<TargetRef> { TargetRef.At(new GridCoord(3, 2)) },
            });

            // Assert — el guard no debe comerse el movimiento normal.
            Assert.AreEqual(1, effect.ApplyCalls);
        }

        // -------------------------------------------------------------------------
        // Puerta bajo los pies (bug real que el guard de arriba destapó): el spawn al
        // entrar a una sala deja al player parado exactamente sobre la casilla
        // frente-a-puerta. Clickearla debe cruzar directo (sin "caminar" 0 pasos vía
        // ExecuteBehavior) y el latch _crossingDoor debe evitar que un segundo evento
        // repita el cruce.
        // -------------------------------------------------------------------------

        [Test]
        public void OnSelectionCompleted_ClickOnOwnTile_WhenDoorTile_StartsCrossingWithoutExecutingBehavior()
        {
            // Arrange — player parado en (2,2), que además es la casilla frente a una
            // puerta (dir North). DoorTileQuery.GetOpenDoorFrontTiles necesita un
            // SpawnedPrefab real con DoorController — montar esa jerarquía completa acá
            // no aporta nada a lo que este test cubre (el branching de
            // OnSelectionCompleted), así que inyectamos _doorTiles por reflection, tal
            // como lo dejaría ResolveDoorTiles si hubiese encontrado la puerta.
            var move = AddExplorationMovement();
            var effect = (FakeMoveEffect)move.Effects[0].Effects[0];

            var fakeDungeon = new FakeDungeonService
            {
                CurrentInstance = new RoomInstance { InstanceId = Guid.NewGuid() },
            };
            ServiceLocator.AddService<IDungeonService>(fakeDungeon, ServiceScope.Global);

            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);
            _service.OnBehaviorSelected(0);

            SetField(_service, "_doorTiles", new Dictionary<GridCoord, DoorDirection>
            {
                { new GridCoord(2, 2), DoorDirection.North },
            });

            // Act — clickea la casilla en la que ya está parado, que es la puerta.
            _selectionController.SimulateSelectionDone(new TargetSelectionResult
            {
                WasCompleted = true,
                SelectedTargets = new List<TargetRef> { TargetRef.At(new GridCoord(2, 2)) },
            });

            // Assert — no "caminó" (el behavior de movimiento nunca se ejecuta con 0
            // pasos) pero sí cruzó: EnterRoomByDoor(North) se invocó una vez. No hay
            // IEntityVisualService registrado en el fixture, así que CrossDoorAfterArrival
            // no tiene de qué esperar y corre hasta el final de forma sincrónica dentro
            // del mismo CoroutineHost.Run — no hace falta pumpear frames para este assert.
            Assert.AreEqual(0, effect.ApplyCalls,
                "Click en la puerta bajo los pies no debe ejecutar el behavior de movimiento (0 pasos).");
            CollectionAssert.AreEqual(new[] { DoorDirection.North }, fakeDungeon.EnterRoomByDoorCalls,
                "Debe cruzar directo, sin esperar animación de movimiento (ya está parado ahí).");

            // El fake dispara OnRoomEntered igual que DungeonManager.TransitionTo (real,
            // sincrónico) — eso debe haber soltado el latch.
            Assert.IsFalse((bool)GetField(_service, "_crossingDoor"),
                "OnRoomEntered debe soltar el latch tras un cruce exitoso.");
        }

        [Test]
        public void OnSelectionCompleted_WhileCrossingDoorLatchActive_IgnoresResult()
        {
            // Arrange — flow de selección en curso...
            var move = AddExplorationMovement();
            var effect = (FakeMoveEffect)move.Effects[0].Effects[0];
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);
            _service.OnBehaviorSelected(0);

            // ...pero el latch ya está activo (un cruce disparado por un resultado previo
            // sigue en vuelo). Lo seteamos a mano para no depender de correr la corrutina
            // de cruce completa solo para llegar a este estado.
            SetField(_service, "_crossingDoor", true);

            // Act — un segundo resultado de selección llega mientras el latch sigue activo.
            _selectionController.SimulateSelectionDone(new TargetSelectionResult
            {
                WasCompleted = true,
                SelectedTargets = new List<TargetRef> { TargetRef.At(new GridCoord(3, 2)) },
            });

            // Assert — el guard corta al tope de OnSelectionCompleted, antes de cualquier
            // otro efecto: ni ejecuta el behavior ni corre el cleanup normal (_state se
            // queda en Selecting en vez de resetear a Idle).
            Assert.AreEqual(0, effect.ApplyCalls,
                "Con el latch activo, un segundo resultado no debe ejecutar nada.");
            Assert.AreEqual("Selecting", GetField(_service, "_state").ToString(),
                "El guard debe retornar antes del cleanup normal de OnSelectionCompleted.");
        }

        // -------------------------------------------------------------------------
        // TryCancelPendingWalk (hotkey X): el camino feliz (pawn caminando de verdad)
        // es PlayMode-only — RequestStopAtStepEnd exige Application.isPlaying y una
        // corutina viva. Acá cubrimos los guards EditMode-testeables.
        // -------------------------------------------------------------------------

        [Test]
        public void TryCancelPendingWalk_WhenInactive_ReturnsFalse()
        {
            // Arrange — sin OnPhaseEnter el service está Inactive.

            // Act + Assert
            Assert.IsFalse(_service.TryCancelPendingWalk());
        }

        [Test]
        public void TryCancelPendingWalk_WithoutVisualService_ReturnsFalse()
        {
            // Arrange — fase activa pero sin IEntityVisualService registrado.
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);

            // Act + Assert
            Assert.IsFalse(_service.TryCancelPendingWalk());
        }

        [Test]
        public void TryCancelPendingWalk_PawnNotMoving_ReturnsFalseWithoutTouchingState()
        {
            // Arrange — hay pawn registrado pero quieto: no hay caminata que cancelar
            // y el cancel NO debe soltar latches ni matar corutinas de llegada.
            EventManager.Trigger(EventName.OnPhaseEnter, GamePhase.Exploration);
            var pawnGo = new GameObject("pawn");
            try
            {
                var pawn = pawnGo.AddComponent<Rollgeon.Entities.Visuals.EntityPawn>();
                pawn.Bind(_playerGuid, Rollgeon.Entities.Visuals.EntityPawn.PawnKind.Hero);
                var visuals = new StubVisualService();
                visuals.Pawns[_playerGuid] = pawn;
                ServiceLocator.AddService<Rollgeon.Entities.Visuals.IEntityVisualService>(
                    visuals, ServiceScope.Global);
                SetField(_service, "_crossingDoor", true);
                int genBefore = (int)GetField(_service, "_walkGeneration");

                // Act
                bool result = _service.TryCancelPendingWalk();

                // Assert
                Assert.IsFalse(result);
                Assert.IsTrue((bool)GetField(_service, "_crossingDoor"),
                    "Un cancel fallido no debe soltar el latch de cruce.");
                Assert.AreEqual(genBefore, (int)GetField(_service, "_walkGeneration"),
                    "Un cancel fallido no debe invalidar corutinas de llegada en vuelo.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pawnGo);
            }
        }

        private HeroActionBehavior AddExplorationMovement()
        {
            var move = new HeroActionBehavior
            {
                ActionName = "Movement",
                IsBaseBehavior = true,
                Slot = HeroBehaviorSlot.Movement,
                AllowedPhases = GamePhaseMask.Exploration,
                NeedsDiceRoll = false,
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        Effects = new List<IEffect> { new FakeMoveEffect() },
                    },
                },
            };
            _heroSO.PhaseBehaviors.Add(move);
            return move;
        }

        // ----- Stubs -----------------------------------------------------------

        private class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId { get; set; }
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }

            public void SetPlayer(ClassHeroSO hero, Guid runId)
            {
                CurrentHero = hero;
                RunId = runId;
            }
            public void SetDiceBag(DiceBagSO bag) => DiceBag = bag;
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }

        private class StubSelectionController : ISelectionController
        {
            public bool SelectionStarted;
            public bool CancelCalled;
            public bool IsSelecting => SelectionStarted && !CancelCalled;
            public bool CanOverlayHoverPreview => !IsSelecting;
            public SelectionSettings ActiveSettings => null;

            public void BeginSelection(SelectionRequest request)
            {
                SelectionStarted = true;
            }

            public void OnTargetClicked(TargetRef target) { }

            public void OnTargetHovered(TargetRef target) { }

            public void CancelSelection()
            {
                CancelCalled = true;
            }

            public void RefreshHighlights() { }

            public event Action<TargetSelectionResult> OnSelectionCompleted;

            public void SimulateSelectionDone(TargetSelectionResult result)
            {
                OnSelectionCompleted?.Invoke(result);
            }
        }

        private class StubGridManager : IGridManager
        {
            public readonly Dictionary<Guid, GridCoord> Positions = new Dictionary<Guid, GridCoord>();

            // SelectionSettings.ResolveValidTiles itera Graph.AllCoords() en el path
            // de selección global — un Graph null tira NRE, así que el stub expone
            // una grilla real (mismo patrón que PlayerTurnStateSelectionTests).
            public NavGraph Graph { get; } = NavGraph.Rect(5, 5);
            public Vector3 GridOrigin => Vector3.zero;
            public float TileSize => 1f;

            public void LoadRoom(NavGraph graph, Vector3 origin = default, float tileSize = 1f) { }
            public bool InBounds(GridCoord c) => true;
            public bool IsWalkable(GridCoord c) => true;
            public bool IsOccupied(GridCoord c) => false;
            public bool IsFree(GridCoord c) => true;

            public bool TryGetOccupant(GridCoord c, out Guid entityGuid)
            {
                entityGuid = Guid.Empty;
                return false;
            }

            public bool TryGetPosition(Guid entityGuid, out GridCoord coord)
            {
                return Positions.TryGetValue(entityGuid, out coord);
            }

            public void Register(Guid entityGuid, GridCoord coord)
                => Positions[entityGuid] = coord;
            public void Unregister(Guid entityGuid)
                => Positions.Remove(entityGuid);
            public bool Move(Guid entityGuid, GridCoord to)
            {
                Positions[entityGuid] = to;
                return true;
            }

            public Vector3 GridToWorld(GridCoord c) => new Vector3(c.X, 0, c.Y);
            public GridCoord WorldToGrid(Vector3 world) => new GridCoord((int)world.x, (int)world.z);
            public IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants() => Positions;
        }

        private class FakeMoveEffect : IEffect
        {
            public int ApplyCalls;

            public string GetEffectName() => "FakeMove";

            public SelectionSettings GetSelection() => new SelectionSettings
            {
                SlotState = SlotState.Empty,
                IsGlobal = true,
                Timing = SelectionTiming.BeforeRoll,
                AutoAccept = true,
            };

            public bool HasSelectionRequirement() => true;
            public bool RequiresSelectionAt(SelectionTiming timing) => timing == SelectionTiming.BeforeRoll;
            public bool ValidateSelection(TargetSelectionResult result, Guid ownerGuid, out string error)
            {
                error = null;
                return true;
            }

            public bool Apply(EffectContext context)
            {
                ApplyCalls++;
                return true;
            }
        }

        // Fake mínimo de IEntityVisualService — solo TryGetPawn responde de verdad.
        private class StubVisualService : Rollgeon.Entities.Visuals.IEntityVisualService
        {
            public readonly Dictionary<Guid, Rollgeon.Entities.Visuals.EntityPawn> Pawns = new();

            public Rollgeon.Entities.Visuals.EntityPawn SpawnHero(Guid guid, ClassHeroSO hero, GridCoord coord) => null;
            public Rollgeon.Entities.Visuals.EntityPawn SpawnEnemy(Guid guid, Rollgeon.Entities.EnemyDataSO data, GridCoord coord) => null;
            public Rollgeon.Entities.Visuals.EntityPawn SpawnProp(Guid guid, GameObject prefab, GridCoord coord) => null;
            public void Despawn(Guid guid) { }
            public void DespawnAll() { }
            public bool TryGetPawn(Guid guid, out Rollgeon.Entities.Visuals.EntityPawn pawn)
                => Pawns.TryGetValue(guid, out pawn) && pawn != null;
            public System.Collections.IEnumerator WaitForMoveComplete(Guid entityId) => null;
            public Vector3? TryGetWorldPosition(Guid entityId) => null;
        }

        // Fake mínimo de IDungeonService para los tests de cruce de puerta — solo
        // necesitamos CurrentRoomInstance (chequeo de "la sala no cambió durante el
        // wait" en CrossDoorAfterArrival) y EnterRoomByDoor (registra la llamada y
        // espeja el efecto real de DungeonManager: dispara OnRoomEntered sincrónico
        // antes de retornar cuando la transición "sale bien").
        private class FakeDungeonService : IDungeonService
        {
            public RoomInstance CurrentInstance;
            public bool EnterRoomByDoorSucceeds = true;
            public readonly List<DoorDirection> EnterRoomByDoorCalls = new List<DoorDirection>();

            public RoomSO CurrentRoom => CurrentInstance?.Template;
            public RoomInstance CurrentRoomInstance => CurrentInstance;
            public DoorDirection? LastEntryDirection => null;

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => new Dictionary<Guid, RoomInstance>();
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => new Dictionary<Guid, FloorShell>();

            public bool CanEnterRoomByDoor(DoorDirection direction, out Guid neighborInstanceId)
            {
                neighborInstanceId = Guid.Empty;
                return EnterRoomByDoorSucceeds;
            }

            public bool EnterRoomByDoor(DoorDirection direction)
            {
                EnterRoomByDoorCalls.Add(direction);
                if (EnterRoomByDoorSucceeds)
                    EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid());
                return EnterRoomByDoorSucceeds;
            }

            public bool EnterRoomByInstanceId(Guid instanceId) => false;
            public bool SetRoomState(Guid instanceId, RoomState state) => false;
            public void ResyncDoorVisuals(Guid instanceId) { }
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders() => Array.Empty<WallOccluder>();
        }

        // ----- reflection helpers (mismo patrón que ActionRollServiceTests) --------

        private static void SetField(object instance, string name, object value)
        {
            var t = instance.GetType();
            while (t != null)
            {
                var f = t.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) { f.SetValue(instance, value); return; }
                t = t.BaseType;
            }
            Assert.Fail($"Field '{name}' not found on {instance.GetType().Name}.");
        }

        private static object GetField(object instance, string name)
        {
            var t = instance.GetType();
            while (t != null)
            {
                var f = t.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) return f.GetValue(instance);
                t = t.BaseType;
            }
            Assert.Fail($"Field '{name}' not found on {instance.GetType().Name}.");
            return null;
        }
    }
}
