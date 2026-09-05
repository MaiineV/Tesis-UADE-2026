using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Items.Active;
using Rollgeon.Items.Active.Choice;
using Rollgeon.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Eleccion post-tirada (Feature#0085 §A5, Probability Drive cara 4): se abre
    /// DESPUES de <c>OnResolved</c>, gatea <c>CanActivate</c>, y se resuelve por
    /// eleccion, abandono (fin de turno), descarte silencioso (fin de combate) o
    /// directo cuando hay una sola opcion.
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemChoiceFlowTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private EquippedActiveItemService _equipped;
        private ActiveItemActivationService _service;
        private FakeRollPool _rolls;
        private FakeDieRoller _roller;
        private FakeSelectionController _selection;
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

            _equipped = new EquippedActiveItemService(catalog: null);
            _roller = new FakeDieRoller { Next = 6 };
            _service = new ActiveItemActivationService(_equipped, _roller);
            _service.ResolveScheduler = (seconds, callback) => callback();

            FakeChoiceEffect.ChosenCoords.Clear();
            FakeChoiceEffect.AbandonedCount = 0;
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
        public void test_choice_opensAfterOnResolved_withTheRequestedOptions()
        {
            // Arrange
            EquipChoiceItem(new[] { new GridCoord(1, 1), new GridCoord(2, 2), new GridCoord(3, 3) });
            bool resolvedBeforeChoiceOpened = false;
            bool choicePending = false;
            _service.OnResolved += _ => resolvedBeforeChoiceOpened = !_service.IsAwaitingChoice;
            _service.OnChoicePending += () => choicePending = true;

            // Act
            _service.Confirm(selection: null);

            // Assert
            Assert.IsTrue(resolvedBeforeChoiceOpened, "OnResolved dispara ANTES de abrir la eleccion");
            Assert.IsTrue(choicePending);
            Assert.IsTrue(_service.IsAwaitingChoice);
            Assert.AreEqual(1, _selection.BeginCalls);
            Assert.AreEqual(3, _selection.LastRequest.ValidTargets.Count);
            Assert.AreEqual(SlotState.Empty, _selection.LastRequest.Settings.SlotState);
            Assert.IsTrue(_selection.LastRequest.Settings.IsGlobal);
            Assert.IsTrue(_selection.LastRequest.Settings.AutoAccept);
            Assert.AreEqual(1, _selection.LastRequest.Settings.SelectionCount);
        }

        [Test]
        public void test_choice_chosen_invokesOnChosenWithThePickedCoord()
        {
            // Arrange
            EquipChoiceItem(new[] { new GridCoord(1, 1), new GridCoord(2, 2) });
            _service.Confirm(selection: null);
            var picked = new GridCoord(2, 2);

            // Act
            _selection.Complete(TargetAt(picked));

            // Assert
            CollectionAssert.AreEqual(new[] { picked }, FakeChoiceEffect.ChosenCoords);
            Assert.AreEqual(0, FakeChoiceEffect.AbandonedCount);
            Assert.IsFalse(_service.IsAwaitingChoice);
        }

        [Test]
        public void test_choice_incompleteSelection_isTreatedAsAbandoned()
        {
            // Arrange
            EquipChoiceItem(new[] { new GridCoord(1, 1), new GridCoord(2, 2) });
            _service.Confirm(selection: null);

            // Act — ESC / click afuera.
            _selection.Complete(new TargetSelectionResult { WasCompleted = false });

            // Assert
            Assert.AreEqual(1, FakeChoiceEffect.AbandonedCount);
            CollectionAssert.IsEmpty(FakeChoiceEffect.ChosenCoords);
            Assert.IsFalse(_service.IsAwaitingChoice);
        }

        [Test]
        public void test_canActivate_whileChoicePending_reportsResolving()
        {
            // Arrange
            EquipChoiceItem(new[] { new GridCoord(1, 1), new GridCoord(2, 2) });
            _service.Confirm(selection: null);

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.Resolving, _service.CanActivate());
            Assert.IsFalse(_service.BeginActivation());
            Assert.AreEqual(4, _rolls.Current[_player], "no se cobra un segundo roll mientras se decide");
        }

        [Test]
        public void test_choice_withSingleOption_resolvesDirectlyWithoutOpeningSelection()
        {
            // Arrange — el efecto degrada solo: con 1 opcion no hay nada que elegir.
            EquipChoiceItem(new[] { new GridCoord(1, 1) });

            // Act
            _service.Confirm(selection: null);

            // Assert
            CollectionAssert.AreEqual(new[] { new GridCoord(1, 1) }, FakeChoiceEffect.ChosenCoords);
            Assert.AreEqual(0, _selection.BeginCalls, "no se abre seleccion para una sola opcion");
            Assert.IsFalse(_service.IsAwaitingChoice);
        }

        [Test]
        public void test_choice_turnFinished_abandonsThePendingChoice()
        {
            // Arrange
            EquipChoiceItem(new[] { new GridCoord(1, 1), new GridCoord(2, 2) });
            _service.Confirm(selection: null);
            bool choiceResolved = false;
            _service.OnChoiceResolved += () => choiceResolved = true;

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, _player);

            // Assert
            Assert.AreEqual(1, FakeChoiceEffect.AbandonedCount);
            Assert.IsTrue(choiceResolved);
            Assert.IsFalse(_service.IsAwaitingChoice);
            Assert.AreEqual(1, _selection.CancelCalls);
        }

        [Test]
        public void test_choice_combatEnd_discardsSilentlyWithoutCallbacks()
        {
            // Arrange
            EquipChoiceItem(new[] { new GridCoord(1, 1), new GridCoord(2, 2) });
            _service.Confirm(selection: null);
            bool choiceResolved = false;
            _service.OnChoiceResolved += () => choiceResolved = true;

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());

            // Assert — descarte silencioso: ni OnChosen ni OnAbandoned, ni el evento del HUD.
            Assert.AreEqual(0, FakeChoiceEffect.AbandonedCount);
            CollectionAssert.IsEmpty(FakeChoiceEffect.ChosenCoords);
            Assert.IsFalse(choiceResolved);
            Assert.IsFalse(_service.IsAwaitingChoice);
        }

        [Test]
        public void test_choice_secondRequestInSameActivation_isIgnored()
        {
            // Arrange — el efecto pide dos elecciones en la misma resolucion.
            EquipChoiceItem(new[] { new GridCoord(1, 1), new GridCoord(2, 2) }, requestTwice: true);

            // Act
            LogAssert.Expect(LogType.Warning, new Regex("eleccion"));
            _service.Confirm(selection: null);

            // Assert — solo el primer pedido se abre.
            Assert.AreEqual(1, _selection.BeginCalls);
            Assert.AreEqual(2, _selection.LastRequest.ValidTargets.Count);
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

        /// <summary>Item self-target (sin seleccion previa) cuya banda positiva pide la eleccion.</summary>
        private ItemSO EquipChoiceItem(GridCoord[] options, bool requestTwice = false)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.choice";
            item.DisplayName = "item.choice";
            item.Type = ItemType.Active;
            item.ActiveDie = DiceType.D6;
            item.OnNegativeBand = new EffectData();
            item.OnMixedBand = new EffectData();
            item.OnPositiveBand = new EffectData();
            item.OnPositiveBand.Effects.Add(new FakeChoiceEffect
            {
                Options = new List<GridCoord>(options),
                RequestTwice = requestTwice,
            });

            _spawned.Add(item);
            _equipped.Equip(item);
            return item;
        }

        // ------------------------------------------------------------------
        // Fakes
        // ------------------------------------------------------------------

        [Serializable]
        private sealed class FakeChoiceEffect : BaseEffect
        {
            public List<GridCoord> Options;
            public bool RequestTwice;

            public static readonly List<GridCoord> ChosenCoords = new List<GridCoord>();
            public static int AbandonedCount;

            public override string GetEffectName() => "FakeChoice";

            public override bool ApplyEffect(EffectContext context)
            {
                if (!ActiveItemRollTriggerContext.TryGet(context, out var rc) || rc.Choices == null) return false;

                var request = new ActiveItemChoiceRequest
                {
                    Options = Options,
                    OnChosen = c => ChosenCoords.Add(c),
                    OnAbandoned = () => AbandonedCount++,
                };
                rc.Choices.RequestChoice(request);

                if (RequestTwice)
                {
                    rc.Choices.RequestChoice(new ActiveItemChoiceRequest
                    {
                        Options = new List<GridCoord> { new GridCoord(9, 9) },
                        OnChosen = c => ChosenCoords.Add(c),
                        OnAbandoned = () => AbandonedCount++,
                    });
                }

                return true;
            }
        }

        private sealed class FakeSelectionController : ISelectionController
        {
            public int BeginCalls { get; private set; }
            public int CancelCalls { get; private set; }
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
                CancelCalls++;
                IsSelecting = false;
            }

            public void OnTargetClicked(TargetRef target) { }
            public void OnTargetHovered(TargetRef target) { }
            public void RefreshHighlights() { }
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
