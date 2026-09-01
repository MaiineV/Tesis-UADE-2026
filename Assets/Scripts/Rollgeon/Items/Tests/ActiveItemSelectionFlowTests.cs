using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Items.Active;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// El paso de selección de objetivo (GDD "Ítems Activos" §10, §12 y §22). Lo que el
    /// doc pide y acá se fija:
    /// <list type="bullet">
    ///   <item>Tocar la ficha no cuesta nada.</item>
    ///   <item>El roll se cobra recién al confirmar el target — ese es el punto de no
    ///         retorno.</item>
    ///   <item>Cancelar antes de confirmar es gratis y el ítem no se gasta.</item>
    ///   <item>Los ítems sin selección activan directo, sin ventana de cancelación.</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemSelectionFlowTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private EquippedActiveItemService _equipped;
        private ActiveItemActivationService _service;
        private FakeRollPool _rolls;
        private FakeDieRoller _roller;
        private FakeSelectionController _selection;
        private StubGrid _grid;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _player = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_player));

            _rolls = new FakeRollPool { InCombat = true };
            _rolls.Current[_player] = 5;
            ServiceLocator.AddService<IRollPoolService>(_rolls);

            _selection = new FakeSelectionController();
            ServiceLocator.AddService<ISelectionController>(_selection);

            // Sin grilla un item que pide target no se puede resolver y se rechaza, asi
            // que hace falta una aunque estos tests miren el flujo y no el targeting.
            _grid = new StubGrid();
            _grid.Register(_player, new GridCoord(2, 2));
            ServiceLocator.AddService<IGridManager>(_grid);

            _equipped = new EquippedActiveItemService(catalog: null);
            _roller = new FakeDieRoller { Next = 6 };
            _service = new ActiveItemActivationService(_equipped, _roller);

            Eff_CaptureTarget.LastCoord = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ------------------------------------------------------------------
        // Item sin seleccion — activa directo
        // ------------------------------------------------------------------

        [Test]
        public void test_begin_selfTargetItem_resolvesImmediatelyWithoutSelection()
        {
            // Arrange — "algunos ítems activan de forma directa, sin paso de selección".
            EquipSelfTarget();

            // Act
            bool started = _service.BeginActivation();

            // Assert
            Assert.IsTrue(started);
            Assert.IsFalse(_service.IsSelecting, "no queda esperando target");
            Assert.AreEqual(0, _selection.BeginCalls, "no abre el selector");
            Assert.AreEqual(4, _rolls.Current[_player], "cobra en el acto");
        }

        [Test]
        public void test_begin_whenBlocked_doesNothing()
        {
            // Arrange
            EquipSelfTarget();
            _rolls.Current[_player] = 0;

            // Act
            bool started = _service.BeginActivation();

            // Assert
            Assert.IsFalse(started);
            Assert.AreEqual(0, _roller.Calls);
        }

        // ------------------------------------------------------------------
        // Item con seleccion — tocar es gratis
        // ------------------------------------------------------------------

        [Test]
        public void test_begin_targetedItem_opensSelectionAndChargesNothing()
        {
            // Arrange
            EquipTargeted();

            // Act
            bool started = _service.BeginActivation();

            // Assert — el paso 1 del GDD es explicito: "no se descuenta ningún recurso
            // todavía".
            Assert.IsTrue(started);
            Assert.IsTrue(_service.IsSelecting);
            Assert.AreEqual(1, _selection.BeginCalls);
            Assert.AreEqual(5, _rolls.Current[_player], "todavia no se cobro nada");
            Assert.AreEqual(0, _roller.Calls, "el dado no se tira antes de confirmar");
        }

        [Test]
        public void test_confirmingTheTarget_chargesTheRollAndRolls()
        {
            // Arrange
            EquipTargeted();
            _service.BeginActivation();

            // Act — el jugador elige.
            _selection.Complete(TargetAt(new GridCoord(1, 0)));

            // Assert
            Assert.IsFalse(_service.IsSelecting);
            Assert.AreEqual(4, _rolls.Current[_player]);
            Assert.AreEqual(1, _roller.Calls);
        }

        [Test]
        public void test_cancellingTheSelection_costsNothing()
        {
            // Arrange — "el jugador puede cancelar la activación en cualquier momento
            // antes de confirmar el target, sin costo alguno".
            EquipTargeted();
            _service.BeginActivation();

            // Act
            _service.CancelActivation();

            // Assert
            Assert.IsFalse(_service.IsSelecting);
            Assert.AreEqual(5, _rolls.Current[_player], "el roll nunca se cobro");
            Assert.AreEqual(0, _roller.Calls);
            Assert.IsTrue(_equipped.HasItem, "el item no se gasta");
        }

        [Test]
        public void test_anIncompleteSelectionResult_isTreatedAsCancel()
        {
            // Arrange — el controller puede cerrar sin eleccion (ESC, click afuera).
            EquipTargeted();
            _service.BeginActivation();

            // Act
            _selection.Complete(new TargetSelectionResult { WasCompleted = false });

            // Assert
            Assert.AreEqual(5, _rolls.Current[_player]);
            Assert.AreEqual(0, _roller.Calls);
        }

        [Test]
        public void test_touchingTheChipAgainWhileArmed_cancels()
        {
            // Arrange — mismo gesto que el resto de las acciones del combate.
            EquipTargeted();
            _service.BeginActivation();

            // Act
            _service.BeginActivation();

            // Assert
            Assert.IsFalse(_service.IsSelecting);
            Assert.AreEqual(5, _rolls.Current[_player]);
        }

        [Test]
        public void test_cancel_raisesTheCancelledEventForTheHud()
        {
            // Arrange
            EquipTargeted();
            int cancels = 0, starts = 0;
            _service.OnSelectionStarted += () => starts++;
            _service.OnSelectionCancelled += () => cancels++;

            // Act
            _service.BeginActivation();
            _service.CancelActivation();

            // Assert
            Assert.AreEqual(1, starts);
            Assert.AreEqual(1, cancels);
        }

        [Test]
        public void test_selectionResult_reachesTheEffectsAsTheTarget()
        {
            // Arrange — sin esto el efecto se aplicaria al jugador en vez de al enemigo.
            EquipTargeted();
            _service.BeginActivation();
            var picked = new GridCoord(2, 3);

            // Act
            _selection.Complete(TargetAt(picked));

            // Assert
            Assert.AreEqual(picked, Eff_CaptureTarget.LastCoord);
        }

        [Test]
        public void test_cancel_withNoSelectionOpen_isNoOp()
        {
            // Act + Assert — no explota ni dispara eventos espurios.
            int cancels = 0;
            _service.OnSelectionCancelled += () => cancels++;
            _service.CancelActivation();
            Assert.AreEqual(0, cancels);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static TargetSelectionResult TargetAt(GridCoord coord)
            => new TargetSelectionResult
            {
                WasCompleted = true,
                SelectedTargets = new List<TargetRef> { TargetRef.At(coord) },
            };

        /// <summary>Item que activa directo: sus efectos no piden seleccion.</summary>
        private void EquipSelfTarget()
        {
            var item = NewItem("item.self");
            foreach (var band in Bands(item)) band.Effects.Add(new Eff_CaptureTarget());
            _equipped.Equip(item);
        }

        /// <summary>Item que pide elegir un objetivo antes de tirar.</summary>
        private void EquipTargeted()
        {
            var item = NewItem("item.targeted");
            foreach (var band in Bands(item))
            {
                var eff = new Eff_CaptureTarget { NeedsSelection = true };
                eff.Selection.SlotState = SlotState.Empty;
                eff.Selection.Range = 5;
                band.Effects.Add(eff);
            }
            _equipped.Equip(item);
        }

        private static IEnumerable<EffectData> Bands(ItemSO item)
        {
            yield return item.OnNegativeBand;
            yield return item.OnMixedBand;
            yield return item.OnPositiveBand;
        }

        private ItemSO NewItem(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Active;
            item.ActiveDie = DiceType.D6;
            item.OnNegativeBand = new EffectData();
            item.OnMixedBand = new EffectData();
            item.OnPositiveBand = new EffectData();
            _spawned.Add(item);
            return item;
        }

        // ------------------------------------------------------------------
        // Fakes
        // ------------------------------------------------------------------

        /// <summary>Anota el tile que recibio, para verificar que el target viaja.</summary>
        [Serializable]
        private sealed class Eff_CaptureTarget : BaseEffect
        {
            public static GridCoord? LastCoord;
            public bool NeedsSelection;

            public override string GetEffectName() => "CaptureTarget";
            public override bool HasSelectionRequirement() => NeedsSelection;

            public override bool ApplyEffect(EffectContext context)
            {
                LastCoord = context?.SelectionResult?.FirstSelectedCoord;
                return true;
            }
        }

        private sealed class FakeSelectionController : ISelectionController
        {
            public int BeginCalls { get; private set; }
            public int CancelCalls { get; private set; }
            public bool IsSelecting { get; private set; }
            public bool CanOverlayHoverPreview => true;

            public event Action<TargetSelectionResult> OnSelectionCompleted;

            public void BeginSelection(SelectionRequest request)
            {
                BeginCalls++;
                IsSelecting = true;
            }

            /// <summary>Simula la eleccion del jugador.</summary>
            public void Complete(TargetSelectionResult result)
            {
                IsSelecting = false;
                OnSelectionCompleted?.Invoke(result);
            }

            public void CancelSelection()
            {
                CancelCalls++;
                IsSelecting = false;
            }

            public void OnTargetClicked(TargetRef target) { }
            public void OnTargetHovered(TargetRef target) { }
            public void RefreshHighlights() { }
        }

        /// <summary>Grilla minima: unos pocos tiles libres y el jugador registrado.</summary>
        private sealed class StubGrid : IGridManager
        {
            private readonly Dictionary<Guid, GridCoord> _positions = new Dictionary<Guid, GridCoord>();
            private readonly NavGraph _graph = new NavGraph();

            public StubGrid()
            {
                for (int x = 0; x <= 4; x++)
                    for (int y = 0; y <= 4; y++)
                        _graph.AddNode(new NavNode(new GridCoord(x, y)));
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
                => _positions.TryGetValue(entityGuid, out coord);

            public void Register(Guid entityGuid, GridCoord coord) => _positions[entityGuid] = coord;
            public void Unregister(Guid entityGuid) => _positions.Remove(entityGuid);

            public bool Move(Guid entityGuid, GridCoord to)
            {
                _positions[entityGuid] = to;
                return true;
            }

            public Vector3 GridToWorld(GridCoord c) => new Vector3(c.X, 0f, c.Y);
            public GridCoord WorldToGrid(Vector3 world) => new GridCoord((int)world.x, (int)world.z);
            public IEnumerable<KeyValuePair<Guid, GridCoord>> Occupants() => _positions;
        }

        private sealed class FakeDieRoller : IActiveItemDieRoller
        {
            public int Next = 1;
            public int Calls { get; private set; }

            public int Roll(DiceType die)
            {
                Calls++;
                return Next;
            }
        }

        private sealed class FakeRollPool : IRollPoolService
        {
            public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();
            public bool InCombat = true;

            public bool IsCombatActive => InCombat;

            public void InitializeForEntity(Guid entityId) => Current[entityId] = 5;

            public bool TrySpendRolls(Guid entityId, int count)
            {
                if (!Current.TryGetValue(entityId, out var have) || count > have) return false;
                Current[entityId] = have - count;
                return true;
            }

            public int Drain(Guid entityId, int amount) => 0;
            public void AddRolls(Guid entityId, int amount) { }
            public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;
            public int GetMax(Guid entityId) => 15;
            public int GetRollsPerTurn(Guid entityId) => 5;
            public void AddRollPoolBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value) => Current[entityId] = value;
        }

        private sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid guid) { PlayerGuid = guid; }

            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }

#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
