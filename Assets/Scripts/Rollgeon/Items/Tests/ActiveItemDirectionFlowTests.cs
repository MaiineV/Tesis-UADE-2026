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
using Rollgeon.Items.Active.Targeting;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Targeting por direccion (Feature#0085 §A4): Justa de Justicia / Grapple Claw. Los
    /// 4 proxies adyacentes, el underlay de rango, el hover preview y que la cardinal
    /// elegida llegue al efecto via <see cref="ActiveItemRollTriggerContext.Direction"/>.
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemDirectionFlowTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private EquippedActiveItemService _equipped;
        private ActiveItemActivationService _service;
        private FakeRollPool _rolls;
        private FakeDieRoller _roller;
        private FakeSelectionController _selection;
        private StubGrid _grid;
        private Guid _player;
        private GridCoord _origin;

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

            _origin = new GridCoord(2, 2);
            _grid = new StubGrid();
            _grid.Register(_player, _origin);
            ServiceLocator.AddService<IGridManager>(_grid);

            _equipped = new EquippedActiveItemService(catalog: null);
            _roller = new FakeDieRoller { Next = 6 };
            _service = new ActiveItemActivationService(_equipped, _roller);

            FakeDirectionEffect.LastAppliedDirection = null;
            FakeDirectionEffect.LastAppliedOrigin = null;
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _equipped?.Dispose();
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void test_begin_directionItem_opensSelectionWithFourProxies()
        {
            // Arrange
            EquipDirectionItem(out _);

            // Act
            bool started = _service.BeginActivation();

            // Assert
            Assert.IsTrue(started);
            Assert.IsNotNull(_selection.LastRequest);
            Assert.AreEqual(4, _selection.LastRequest.ValidTargets.Count);
            Assert.AreEqual(SlotState.Both, _selection.LastRequest.Settings.SlotState);
        }

        [Test]
        public void test_canActivate_withNoValidDirection_reportsNoValidTarget()
        {
            // Arrange — ninguna cardinal tiene trayectoria.
            EquipDirectionItem(out var effect, allEnabled: false);

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.NoValidTarget, _service.CanActivate());
            Assert.IsFalse(_service.BeginActivation());
            Assert.AreEqual(0, _selection.BeginCalls);
        }

        [Test]
        public void test_rangeTiles_unionsAllTrajectories()
        {
            // Arrange — 4 direcciones × 2 tiles cada una, todas distintas en un grid 5x5.
            EquipDirectionItem(out _);

            // Act
            _service.BeginActivation();

            // Assert
            Assert.IsNotNull(_selection.LastRequest.RangeTiles);
            Assert.AreEqual(8, _selection.LastRequest.RangeTiles.Count);
            Assert.AreEqual("range", _selection.LastRequest.RangeHighlightStyle);
        }

        [Test]
        public void test_hoverPreview_recomputesTrajectoryForTheHoveredProxy()
        {
            // Arrange
            EquipDirectionItem(out var effect);
            _service.BeginActivation();
            var eastProxy = Cardinal.East.Step(_origin);

            // Act
            var preview = _selection.LastRequest.HoverPreview(eastProxy);

            // Assert
            CollectionAssert.AreEqual(effect.PreviewTrajectory(_player, _origin, Cardinal.East), preview);
        }

        [Test]
        public void test_accept_directionReachesTheEffectContext()
        {
            // Arrange
            EquipDirectionItem(out _);
            _service.BeginActivation();
            var eastProxy = Cardinal.East.Step(_origin);

            // Act
            _selection.Complete(TargetAt(eastProxy));
            _service.AcceptRoll();

            // Assert
            Assert.AreEqual(Cardinal.East, FakeDirectionEffect.LastAppliedDirection);
            Assert.AreEqual(_origin, FakeDirectionEffect.LastAppliedOrigin);
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

        private ItemSO EquipDirectionItem(out FakeDirectionEffect effect, bool allEnabled = true)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.direction";
            item.DisplayName = "item.direction";
            item.Type = ItemType.Active;
            item.ActiveDie = DiceType.D6;
            item.OnNegativeBand = new EffectData();
            item.OnMixedBand = new EffectData();
            item.OnPositiveBand = new EffectData();

            effect = new FakeDirectionEffect { Enabled = new[] { allEnabled, allEnabled, allEnabled, allEnabled } };
            item.OnPositiveBand.Effects.Add(effect);
            item.OnNegativeBand.Effects.Add(effect);
            item.OnMixedBand.Effects.Add(effect);

            _spawned.Add(item);
            _equipped.Equip(item);
            return item;
        }

        // ------------------------------------------------------------------
        // Fakes
        // ------------------------------------------------------------------

        [Serializable]
        private sealed class FakeDirectionEffect : BaseEffect, IDirectionTargetedEffect
        {
            public bool[] Enabled = { true, true, true, true };
            public int TrajectoryLength = 2;

            public static Cardinal? LastAppliedDirection;
            public static GridCoord? LastAppliedOrigin;

            public override string GetEffectName() => "FakeDirection";

            public IReadOnlyList<GridCoord> PreviewTrajectory(Guid owner, GridCoord origin, Cardinal dir)
            {
                if (!Enabled[(int)dir]) return Array.Empty<GridCoord>();

                var list = new List<GridCoord>();
                var cur = origin;
                for (int i = 0; i < TrajectoryLength; i++)
                {
                    cur = dir.Step(cur);
                    list.Add(cur);
                }
                return list;
            }

            public override bool ApplyEffect(EffectContext context)
            {
                ActiveItemRollTriggerContext.TryGet(context, out var rc);
                LastAppliedDirection = rc?.Direction;
                LastAppliedOrigin = rc?.Origin;
                return true;
            }
        }

        private sealed class FakeSelectionController : ISelectionController
        {
            public int BeginCalls { get; private set; }
            public bool IsSelecting { get; private set; }
            public bool CanOverlayHoverPreview => true;
            public SelectionSettings ActiveSettings => null;
            public SelectionRequest LastRequest { get; private set; }

            public event Action<TargetSelectionResult> OnSelectionCompleted;

            public void BeginSelection(SelectionRequest request)
            {
                BeginCalls++;
                LastRequest = request;
                IsSelecting = true;
            }

            public void Complete(TargetSelectionResult result)
            {
                IsSelecting = false;
                OnSelectionCompleted?.Invoke(result);
            }

            public void CancelSelection()
            {
                IsSelecting = false;
            }

            public void OnTargetClicked(TargetRef target) { }
            public void OnTargetHovered(TargetRef target) { }
            public void RefreshHighlights() { }
        }

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
