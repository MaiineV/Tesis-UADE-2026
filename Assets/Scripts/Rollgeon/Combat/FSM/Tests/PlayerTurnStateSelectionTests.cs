using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FSM.States;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using UnityEngine;

namespace Rollgeon.Combat.FSM.Tests
{
    /// <summary>
    /// Regresión de <b>BUG-013</b> ("moverme mientras ataco produce bug").
    /// <para>
    /// El movimiento es una acción sin tirada que requiere elegir el tile destino: ejecuta
    /// de forma <b>asíncrona</b> cuando el jugador clickea. <see cref="PlayerTurnState.RequestAction"/>
    /// debe avisar la finalización vía su callback <c>onComplete</c> SÓLO cuando la acción
    /// realmente corrió — no antes. Si el callback se disparara al abrir la selección, el
    /// handoff soltaría el lock de la UI y nullaría su guard, dejando que el jugador inicie
    /// un ataque EN PARALELO al movimiento pendiente (el bug reportado).
    /// </para>
    /// </summary>
    [TestFixture]
    public class PlayerTurnStateSelectionTests
    {
        private Guid _playerId;
        private Guid _roomId;
        private StubGridManager _grid;
        private StubSelectionController _selection;
        private FakeEnergyService _energy;
        private TurnOrderService _turnOrder;
        private PlayerTurnState _playerState;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _playerId = Guid.NewGuid();
            _roomId = Guid.NewGuid();

            _grid = new StubGridManager();
            _grid.Positions[_playerId] = new GridCoord(2, 2);
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _selection = new StubSelectionController();
            ServiceLocator.AddService<ISelectionController>(_selection, ServiceScope.Global);

            _energy = new FakeEnergyService();
            _energy.Current[_playerId] = _energy.MaxPerEntity;

            // TurnManager null: la FSM no lo necesita para este test (Executing cae al
            // path action.Execute). TurnOrder no se construye su cola — sólo se referencia.
            _turnOrder = new TurnOrderService();

            var ctx = new CombatContext(_turnOrder, null, _energy, _playerId, _roomId, null);
            _playerState = new PlayerTurnState(ctx);
            _playerState.Enter(CombatInput.None);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void RequestAction_SelectionPending_DoesNotInvokeOnCompleteUntilSelectionResolves()
        {
            // Arrange
            var move = BuildSelectionMove();
            var completed = false;
            var behaviorCtx = new HeroBehaviorContext { SourceEntity = new Entity { Guid = _playerId } };

            // Act — pedimos la acción: abre la selección de tile y queda esperando el click.
            _playerState.RequestAction(move, behaviorCtx, () => completed = true);

            // Assert — la selección arrancó pero la acción NO terminó: el callback no debe
            // haberse disparado todavía. Si lo hiciera, el handoff soltaría el lock y dejaría
            // iniciar otra acción en paralelo al movimiento pendiente (BUG-013).
            Assert.IsTrue(_selection.SelectionStarted, "Debe haber comenzado la selección de target.");
            Assert.IsFalse(completed, "OnComplete NO debe dispararse mientras la selección está pendiente.");

            // Act — el jugador clickea un tile válido → la selección se resuelve.
            _selection.SimulateSelectionDone(new TargetSelectionResult
            {
                WasCompleted = true,
                SelectedTargets = new List<TargetRef> { TargetRef.At(new GridCoord(3, 2)) },
            });

            // Assert — recién ahora la acción ejecutó y el callback se disparó.
            Assert.IsTrue(completed, "OnComplete debe dispararse cuando la acción terminó de ejecutarse.");
        }

        [Test]
        public void RequestAction_DirectActionWithoutSelection_InvokesOnCompleteImmediately()
        {
            // Arrange — una acción sin selección (no requiere elegir tile) ejecuta directo.
            var direct = new HeroActionBehavior
            {
                ActionName = "Direct",
                NeedsDiceRoll = false,
                Effects = new List<EffectData>(),
            };
            var completed = false;
            var behaviorCtx = new HeroBehaviorContext { SourceEntity = new Entity { Guid = _playerId } };

            // Act
            _playerState.RequestAction(direct, behaviorCtx, () => completed = true);

            // Assert — sin selección de por medio, el callback se dispara sincrónicamente y
            // no se abre ninguna selección.
            Assert.IsTrue(completed, "Una acción directa debe invocar OnComplete inmediatamente.");
            Assert.IsFalse(_selection.SelectionStarted, "No debe abrirse ninguna selección para una acción directa.");
        }

        // ----- Helpers ---------------------------------------------------------

        private static HeroActionBehavior BuildSelectionMove()
        {
            return new HeroActionBehavior
            {
                ActionName = "Movement",
                IsBaseBehavior = true,
                Slot = HeroBehaviorSlot.Movement,
                EnergyCost = 1,
                NeedsDiceRoll = false,
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        Effects = new List<IEffect> { new FakeMoveEffect() },
                    },
                },
            };
        }

        // ----- Stubs -----------------------------------------------------------

        private sealed class FakeMoveEffect : IEffect
        {
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

            public bool Apply(EffectContext context) => true;
        }

        private sealed class StubSelectionController : ISelectionController
        {
            public bool SelectionStarted;
            public bool CancelCalled;
            public bool IsSelecting => SelectionStarted && !CancelCalled;

            public event Action<TargetSelectionResult> OnSelectionCompleted;

            public void BeginSelection(SelectionRequest request) => SelectionStarted = true;
            public void OnTargetClicked(TargetRef target) { }
            public void OnTargetHovered(TargetRef target) { }
            public void CancelSelection() => CancelCalled = true;

            public void SimulateSelectionDone(TargetSelectionResult result) => OnSelectionCompleted?.Invoke(result);
        }

        private sealed class StubGridManager : IGridManager
        {
            public readonly Dictionary<Guid, GridCoord> Positions = new Dictionary<Guid, GridCoord>();
            private readonly NavGraph _graph;

            public StubGridManager()
            {
                _graph = new NavGraph();
                _graph.AddNode(new NavNode(new GridCoord(2, 2)));
                _graph.AddNode(new NavNode(new GridCoord(3, 2)));
            }

            public NavGraph Graph => _graph;
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
                => Positions.TryGetValue(entityGuid, out coord);

            public void Register(Guid entityGuid, GridCoord coord) => Positions[entityGuid] = coord;
            public void Unregister(Guid entityGuid) => Positions.Remove(entityGuid);
            public bool Move(Guid entityGuid, GridCoord to)
            {
                Positions[entityGuid] = to;
                return true;
            }

            public Vector3 GridToWorld(GridCoord c) => new Vector3(c.X, 0, c.Y);
            public GridCoord WorldToGrid(Vector3 world) => new GridCoord((int)world.x, (int)world.z);
            public IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants() => Positions;
        }
    }
}
