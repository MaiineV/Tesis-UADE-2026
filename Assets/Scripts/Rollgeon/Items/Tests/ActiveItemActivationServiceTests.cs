using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Rolls;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Items.Active;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Gating (§6/§7) y secuencia de activacion (§22) del item activo: tocar es gratis,
    /// confirmar cobra 1 roll, la tirada va inmediatamente despues y la banda decide que
    /// efecto corre.
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemActivationServiceTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private EquippedActiveItemService _equipped;
        private ActiveItemActivationService _service;
        private FakeRollPool _rolls;
        private FakeDieRoller _roller;
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

            _equipped = new EquippedActiveItemService(catalog: null);
            _roller = new FakeDieRoller();
            _service = new ActiveItemActivationService(_equipped, _roller);

            Eff_Tag.Log.Clear();
            Eff_CaptureRollContext.Last = null;
            Eff_CaptureRollContext.LastItem = null;
            Eff_CaptureRollContext.LastSourceItemId = null;
            Pc_CapturePreCtx.LastEffect = null;
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _equipped?.Dispose();
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            Eff_Tag.Log.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ------------------------------------------------------------------
        // Gating
        // ------------------------------------------------------------------

        [Test]
        public void test_canActivate_withEmptySlot_reportsNoItemEquipped()
        {
            // Act + Assert — PRE-02.
            Assert.AreEqual(ActiveItemBlock.NoItemEquipped, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_outOfCombat_reportsNotInCombat()
        {
            // Arrange — el GDD: "no existe ni se acumula durante la exploración".
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.InCombat = false;

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.NotInCombat, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_outOfCombatWithEmptySlot_reportsNotInCombatNotEmptySlot()
        {
            // Arrange — "completamente oculta fuera de combate" es la regla mas externa:
            // manda sobre el slot vacio. Con la precedencia al reves la ficha se mostraba
            // en exploracion diciendo "sin item equipado", que es justo lo que el GDD
            // prohibe.
            _rolls.InCombat = false;

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.NotInCombat, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_withNoRolls_reportsNotEnoughRolls()
        {
            // Arrange — PRE-03.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.Current[_player] = 0;

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.NotEnoughRolls, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_withExactlyOneRoll_isAllowed()
        {
            // Arrange — edge case del GDD: con 1 roll se puede, el pool queda en 0.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.Current[_player] = 1;

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.None, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_outsideYourTurn_reportsNotYourTurn()
        {
            // Arrange — PRE-01.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            var turns = new TurnManager();
            turns.ConfigureForTests(_rolls, actions: null, ruleset: null);
            turns.SetActingGuidForTests(Guid.NewGuid());
            ServiceLocator.AddService<TurnManager>(turns);

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.NotYourTurn, _service.CanActivate());
        }

        [Test]
        public void test_canActivate_isPure_doesNotSpendRolls()
        {
            // Arrange — el HUD lo llama en cada refresh.
            _equipped.Equip(NewItem("item.a", DiceType.D6));

            // Act
            _service.CanActivate();
            _service.CanActivate();

            // Assert
            Assert.AreEqual(5, _rolls.Current[_player]);
            Assert.AreEqual(0, _rolls.SpendCalls);
        }

        // ------------------------------------------------------------------
        // Confirmacion: cobro, tirada y ventana de decision
        // ------------------------------------------------------------------

        [Test]
        public void test_confirm_spendsExactlyOneRoll()
        {
            // Arrange — "1 roll, fijo, igual para todos los ítems activos".
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = 5;

            // Act
            _service.Confirm(selection: null);

            // Assert
            Assert.AreEqual(4, _rolls.Current[_player]);
        }

        [Test]
        public void test_confirm_leavesTheRollPendingWithoutRunningEffects()
        {
            // Arrange — el activo se re-tira como ataque/defensa: entre la tirada y los
            // efectos hay una ventana de decision.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = 5;

            ActiveItemPendingRoll? pendingSeen = null;
            bool resolved = false;
            _service.OnRollPending += p => pendingSeen = p;
            _service.OnResolved += _ => resolved = true;

            // Act
            var pending = _service.Confirm(selection: null);

            // Assert
            Assert.IsNotNull(pending);
            Assert.AreEqual(5, pending.Value.RawRoll);
            Assert.AreEqual(0, pending.Value.RerollCount);
            Assert.IsTrue(_service.IsAwaitingDecision);
            Assert.IsNotNull(pendingSeen, "el HUD escucha OnRollPending para girar el dado");
            Assert.IsFalse(resolved, "OnResolved recien al aceptar");
            CollectionAssert.IsEmpty(Eff_Tag.Log, "los efectos corren recien al aceptar");
        }

        [TestCase(1, ActiveItemBand.Negative, "neg")]
        [TestCase(3, ActiveItemBand.Mixed, "mix")]
        [TestCase(6, ActiveItemBand.Positive, "pos")]
        public void test_accept_runsOnlyTheEffectsOfTheRolledBand(int roll, ActiveItemBand band, string tag)
        {
            // Arrange
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = roll;
            _service.Confirm(selection: null);

            // Act
            var result = _service.AcceptRoll();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(roll, result.Value.Roll);
            Assert.AreEqual(band, result.Value.Band);
            CollectionAssert.AreEqual(new[] { tag }, Eff_Tag.Log,
                "solo tiene que correr el grupo de la banda que salio");
        }

        [Test]
        public void test_confirm_whenBlocked_neitherSpendsNorRolls()
        {
            // Arrange
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.Current[_player] = 0;

            // Act
            var result = _service.Confirm(selection: null);

            // Assert
            Assert.IsNull(result);
            Assert.AreEqual(0, _roller.Calls, "no se tira el dado si la activacion esta bloqueada");
            CollectionAssert.IsEmpty(Eff_Tag.Log);
        }

        [Test]
        public void test_accept_raisesOnResolvedWithTheRollAndBand()
        {
            // Arrange — el HUD lo usa para mostrar la cara dentro del slot.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = 2;
            _service.Confirm(selection: null);

            ActiveItemActivationResult? seen = null;
            _service.OnResolved += r => seen = r;

            // Act
            _service.AcceptRoll();

            // Assert
            Assert.IsNotNull(seen);
            Assert.AreEqual(2, seen.Value.Roll);
            Assert.AreEqual(ActiveItemBand.Negative, seen.Value.Band);
            Assert.IsFalse(_service.IsAwaitingDecision, "la ventana se cierra al aceptar");
        }

        [Test]
        public void test_activation_canBeRepeatedWhileRollsLast()
        {
            // Arrange — el GDD no pone tope de usos por turno ni por combate.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = 6;
            _rolls.Current[_player] = 2;

            // Act
            Assert.IsNotNull(_service.Confirm(null));
            Assert.IsNotNull(_service.AcceptRoll());
            Assert.IsNotNull(_service.Confirm(null));
            Assert.IsNotNull(_service.AcceptRoll());

            // Assert — al tercer intento el pool esta en 0.
            Assert.AreEqual(0, _rolls.Current[_player]);
            Assert.IsNull(_service.Confirm(null));
        }

        [Test]
        public void test_accept_theRollIsSpentEvenIfTheBandEffectsFail()
        {
            // Arrange — no hay reembolso: el GDD dice que no existe ventana para uno.
            var item = NewItem("item.a", DiceType.D6);
            item.OnPositiveBand.Effects.Clear();
            item.OnPositiveBand.Effects.Add(new Eff_Fail());
            _roller.Next = 6;
            _service.Confirm(selection: null);

            // Act
            var result = _service.AcceptRoll();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Value.EffectsSucceeded);
            Assert.AreEqual(4, _rolls.Current[_player], "el roll ya se habia cobrado");
        }

        // ------------------------------------------------------------------
        // Reroll: mismo contrato que el de ataque/defensa, con un solo dado
        // ------------------------------------------------------------------

        [Test]
        public void test_reroll_spendsOneRollAndReplacesTheFace()
        {
            // Arrange — cada tirada (la primera o un reroll) cuesta exactamente 1 roll.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = 2;
            _service.Confirm(selection: null);
            _roller.Next = 5;

            // Act
            bool ok = _service.RequestReroll();

            // Assert
            Assert.IsTrue(ok);
            Assert.AreEqual(3, _rolls.Current[_player], "confirm + reroll = 2 rolls");
            Assert.AreEqual(5, _service.Pending.Value.RawRoll, "la cara nueva pisa a la vieja");
            Assert.AreEqual(1, _service.Pending.Value.RerollCount);
            Assert.IsTrue(_service.IsAwaitingDecision, "sigue pendiente: se puede volver a decidir");
            CollectionAssert.IsEmpty(Eff_Tag.Log);
        }

        [Test]
        public void test_reroll_withEmptyPool_neitherSpendsNorRolls()
        {
            // Arrange — pool 0 no cancela lo tirado: la cara vigente queda y la unica
            // salida es aceptar. Nunca hay reembolso.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.Current[_player] = 1;
            _roller.Next = 2;
            _service.Confirm(selection: null);

            // Act
            bool ok = _service.RequestReroll();

            // Assert
            Assert.IsFalse(ok);
            Assert.AreEqual(0, _rolls.Current[_player]);
            Assert.AreEqual(1, _roller.Calls, "el dado no se tira si no se pudo cobrar");
            Assert.AreEqual(2, _service.Pending.Value.RawRoll, "la cara vigente no cambia");
            Assert.IsTrue(_service.IsAwaitingDecision);
        }

        [Test]
        public void test_reroll_canRepeatWhileRollsLast()
        {
            // Arrange — como en combate: se re-tira mientras el pool aguante.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.Current[_player] = 3;
            _roller.Next = 2;
            _service.Confirm(selection: null);

            // Act + Assert
            Assert.IsTrue(_service.RequestReroll());
            Assert.IsTrue(_service.RequestReroll());
            Assert.AreEqual(0, _rolls.Current[_player]);
            Assert.IsFalse(_service.RequestReroll(), "el tercero no tiene con que pagarse");
            Assert.AreEqual(2, _service.Pending.Value.RerollCount);
        }

        [Test]
        public void test_reroll_withoutAPendingRoll_isRejected()
        {
            // Arrange
            _equipped.Equip(NewItem("item.a", DiceType.D6));

            // Act + Assert — no explota ni cobra.
            Assert.IsFalse(_service.RequestReroll());
            Assert.AreEqual(5, _rolls.Current[_player]);
        }

        [Test]
        public void test_canRequestReroll_followsPoolAndPendingState()
        {
            // Arrange — es el gate del click de la ficha, read-only.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _rolls.Current[_player] = 2;

            // Act + Assert
            Assert.IsFalse(_service.CanRequestReroll, "sin tirada pendiente no hay que re-tirar");
            _service.Confirm(selection: null);
            Assert.IsTrue(_service.CanRequestReroll, "pendiente y con pool");
            _service.RequestReroll();
            Assert.IsFalse(_service.CanRequestReroll, "el pool quedo en 0");
        }

        [Test]
        public void test_accept_afterRerolls_usesTheLastFace()
        {
            // Arrange — "debe aceptar el segundo resultado": la cara que resuelve es la
            // ultima que salio, no la mejor.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = 6;
            _service.Confirm(selection: null);
            _roller.Next = 1;
            _service.RequestReroll();

            // Act
            var result = _service.AcceptRoll();

            // Assert
            Assert.AreEqual(1, result.Value.Roll);
            Assert.AreEqual(ActiveItemBand.Negative, result.Value.Band);
            CollectionAssert.AreEqual(new[] { "neg" }, Eff_Tag.Log);
        }

        [Test]
        public void test_accept_withoutAPendingRoll_returnsNullAndRunsNothing()
        {
            // Arrange
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _service.Confirm(selection: null);
            _service.AcceptRoll();
            Eff_Tag.Log.Clear();

            // Act — un segundo accept no re-ejecuta nada.
            var second = _service.AcceptRoll();

            // Assert
            Assert.IsNull(second);
            CollectionAssert.IsEmpty(Eff_Tag.Log);
        }

        [Test]
        public void test_beginActivation_whileAwaitingDecision_isBlocked()
        {
            // Arrange — la ventana abierta bloquea abrir otra activacion.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _service.Confirm(selection: null);

            // Act + Assert
            Assert.AreEqual(ActiveItemBlock.AwaitingDecision, _service.CanActivate());
            Assert.IsFalse(_service.BeginActivation());
            Assert.AreEqual(4, _rolls.Current[_player], "no se cobro un segundo roll");
        }

        [Test]
        public void test_combatEnd_discardsThePendingRollWithoutRunningEffects()
        {
            // Arrange — OnCombatEnd llega despues del teardown del combate: ejecutar la
            // banda ahi pegaria sobre una sala desarmada. El roll pagado no se devuelve.
            _equipped.Equip(NewItem("item.a", DiceType.D6));
            _roller.Next = 6;
            _service.Confirm(selection: null);
            bool resolved = false;
            _service.OnResolved += _ => resolved = true;

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());

            // Assert
            Assert.IsFalse(_service.IsAwaitingDecision);
            Assert.IsFalse(resolved);
            CollectionAssert.IsEmpty(Eff_Tag.Log);
            Assert.AreEqual(4, _rolls.Current[_player], "el roll pagado no se reembolsa");
        }

        // ------------------------------------------------------------------
        // Feature#0085: trigger context, SourceItemId, PreConditionContext.Effect
        // ------------------------------------------------------------------

        [Test]
        public void test_accept_effectReceivesActiveItemRollTriggerContext_withFaceBandAndMagnitude()
        {
            // Arrange — Gradient para que Magnitude == Face (Bands/Binary la dejan en 0).
            var item = NewGradientItem("item.gradient", DiceType.D6);
            _roller.Next = 4;
            _service.Confirm(selection: null);

            // Act
            _service.AcceptRoll();

            // Assert
            Assert.IsNotNull(Eff_CaptureRollContext.Last);
            Assert.AreEqual(4, Eff_CaptureRollContext.Last.Face);
            Assert.AreEqual(4, Eff_CaptureRollContext.Last.RawFace);
            Assert.AreEqual(6, Eff_CaptureRollContext.Last.Faces);
            Assert.AreEqual(4, Eff_CaptureRollContext.Last.Magnitude);
            Assert.AreEqual(item, Eff_CaptureRollContext.LastItem);
        }

        [Test]
        public void test_accept_setsSourceItemIdOnTheEffectContext()
        {
            // Arrange — roll=1 en D6 Bands cae en negativa: la captura vive ahi.
            var item = NewCaptureItem("item.source", DiceType.D6);
            _roller.Next = 1;
            _service.Confirm(selection: null);

            // Act
            _service.AcceptRoll();

            // Assert
            Assert.AreEqual("item.source", Eff_CaptureRollContext.LastSourceItemId);
        }

        [Test]
        public void test_accept_populatesPreConditionContextEffect()
        {
            // Arrange — la precondicion cuelga del grupo que va a correr (positiva, roll=6).
            var item = NewItem("item.precheck", DiceType.D6);
            item.OnPositiveBand.PreConditions.Add(new Pc_CapturePreCtx());
            _roller.Next = 6;
            _service.Confirm(selection: null);

            // Act
            _service.AcceptRoll();

            // Assert
            Assert.IsNotNull(Pc_CapturePreCtx.LastEffect, "PreConditionContext.Effect tiene que viajar");
            Assert.IsInstanceOf<ActiveItemRollTriggerContext>(Pc_CapturePreCtx.LastEffect.TriggerContext);
        }

        [Test]
        public void test_pcActiveItemFaceCompare_gatesOnTheResolvedFace()
        {
            // Arrange — Greater/6 solo pasa con la cara maxima.
            var item = NewItem("item.gated", DiceType.D6);
            item.OnPositiveBand.PreConditions.Add(new Rollgeon.PreConditions.Concretes.PcActiveItemFaceCompare
            {
                Comparison = Rollgeon.PreConditions.Concretes.IntComparison.GreaterOrEqual,
                Value = 6,
            });
            _roller.Next = 6;
            _service.Confirm(selection: null);

            // Act
            var result = _service.AcceptRoll();

            // Assert
            Assert.IsTrue(result.Value.EffectsSucceeded);
            CollectionAssert.AreEqual(new[] { "pos" }, Eff_Tag.Log);
        }

        [Test]
        public void test_pcActiveItemFaceCompare_blocksWhenFaceDoesNotMatch()
        {
            // Arrange
            var item = NewItem("item.gated2", DiceType.D6);
            item.OnMixedBand.PreConditions.Add(new Rollgeon.PreConditions.Concretes.PcActiveItemFaceCompare
            {
                Comparison = Rollgeon.PreConditions.Concretes.IntComparison.Equal,
                Value = 99,
            });
            _roller.Next = 3; // banda mixta
            _service.Confirm(selection: null);

            // Act
            var result = _service.AcceptRoll();

            // Assert — la precondicion no matchea: el grupo no corre, EffectsSucceeded false.
            Assert.IsFalse(result.Value.EffectsSucceeded);
            CollectionAssert.IsEmpty(Eff_Tag.Log);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private ItemSO NewGradientItem(string id, DiceType die)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Active;
            item.ActiveDie = die;
            item.ActiveResolution = ActiveItemResolution.Gradient;
            item.OnNegativeBand = new EffectData();
            item.OnMixedBand = new EffectData();
            item.OnPositiveBand = new EffectData();
            item.OnPositiveBand.Effects.Add(new Eff_CaptureRollContext());
            _spawned.Add(item);
            _equipped.Equip(item);
            return item;
        }

        /// <summary>Item Bands (legacy) con la captura en las 3 bandas — corre cual sea la que salga.</summary>
        private ItemSO NewCaptureItem(string id, DiceType die)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Active;
            item.ActiveDie = die;
            item.OnNegativeBand = new EffectData();
            item.OnNegativeBand.Effects.Add(new Eff_CaptureRollContext());
            item.OnMixedBand = new EffectData();
            item.OnMixedBand.Effects.Add(new Eff_CaptureRollContext());
            item.OnPositiveBand = new EffectData();
            item.OnPositiveBand.Effects.Add(new Eff_CaptureRollContext());
            _spawned.Add(item);
            _equipped.Equip(item);
            return item;
        }

        /// <summary>Anota el ActiveItemRollTriggerContext y el SourceItemId que le llegaron.</summary>
        [Serializable]
        private sealed class Eff_CaptureRollContext : BaseEffect
        {
            public static ActiveItemRollTriggerContext Last;
            public static ItemSO LastItem;
            public static string LastSourceItemId;

            public override string GetEffectName() => "CaptureRollContext";

            public override bool ApplyEffect(EffectContext context)
            {
                LastSourceItemId = context?.SourceItemId;
                ActiveItemRollTriggerContext.TryGet(context, out Last);
                LastItem = Last?.Item;
                return true;
            }
        }

        /// <summary>Anota el PreConditionContext.Effect que le llego a Evaluate.</summary>
        [Serializable]
        private sealed class Pc_CapturePreCtx : Rollgeon.PreConditions.BasePreCondition
        {
            public static Rollgeon.Effects.EffectContext LastEffect;

            public override string ConditionName => "CapturePreCtx";

            public override bool Evaluate(Rollgeon.PreConditions.PreConditionContext context)
            {
                LastEffect = context?.Effect;
                return true;
            }
        }

        private ItemSO NewItem(string id, DiceType die)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Active;
            item.ActiveDie = die;
            item.ActiveFamily = ActiveItemFamily.Potencia;
            item.OnNegativeBand = new EffectData();
            item.OnNegativeBand.Effects.Add(new Eff_Tag { Tag = "neg" });
            item.OnMixedBand = new EffectData();
            item.OnMixedBand.Effects.Add(new Eff_Tag { Tag = "mix" });
            item.OnPositiveBand = new EffectData();
            item.OnPositiveBand.Effects.Add(new Eff_Tag { Tag = "pos" });
            _spawned.Add(item);
            _equipped.Equip(item);
            return item;
        }

        /// <summary>Registra que corrio, para saber que banda se ejecuto.</summary>
        [Serializable]
        private sealed class Eff_Tag : BaseEffect
        {
            public static readonly List<string> Log = new List<string>();
            public string Tag;

            public override string GetEffectName() => "Tag";
            public override bool ApplyEffect(EffectContext context)
            {
                Log.Add(Tag);
                return true;
            }
        }

        [Serializable]
        private sealed class Eff_Fail : BaseEffect
        {
            public override string GetEffectName() => "Fail";
            public override bool ApplyEffect(EffectContext context) => false;
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
            public int SpendCalls { get; private set; }

            public bool IsCombatActive => InCombat;

            public void InitializeForEntity(Guid entityId) => Current[entityId] = 5;

            public bool TrySpendRolls(Guid entityId, int count)
            {
                SpendCalls++;
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
