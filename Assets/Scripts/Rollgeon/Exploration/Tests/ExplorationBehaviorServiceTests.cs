using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities.Behaviors;
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
            foreach (var slot in new[] { HeroBehaviorSlot.Movement, HeroBehaviorSlot.BaseAttack, HeroBehaviorSlot.SpecialAttack, HeroBehaviorSlot.Healing })
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
    }
}
