using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Combat.FSM;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos.Tests
{
    /// <summary>
    /// Tests del dispatch genérico (hooks <c>IOn*PassiveTrigger</c>) del
    /// <see cref="ComboPassiveService"/>: dispatch a TODAS las pasivas sin importar
    /// <c>TargetComboId</c>, filtro de turnos del player, payloads por evento,
    /// guard de reentrada de oro y no-interferencia con <c>LastComboScratch</c>.
    /// </summary>
    [TestFixture]
    public class ComboPassiveGenericTriggerTests
    {
        private ComboPassiveService _svc;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();
            SaveSystem.ResetForTests();

            _svc = new ComboPassiveService();
            _svc.SubscribeEventsForTests();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.UnsubscribeEventsForTests();
            _svc = null;

            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static void TriggerRunStart()
        {
            // Espeja RunBootstrapper.StartRun — run nueva = cache de save limpio
            // (mismo racional que ComboCountersServiceTests).
            SaveSystem.Clear();
            EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "test-ruleset");
        }

        private ComboPassiveSO MakePassive(string id, string targetComboId, params IComboPassiveTrigger[] triggers)
        {
            var passive = ScriptableObject.CreateInstance<ComboPassiveSO>();
            passive.name = id;
            _created.Add(passive);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, id);
            typeof(ComboPassiveSO).GetField("_targetComboId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, targetComboId);
            typeof(ComboPassiveSO).GetField("_extraTriggers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(passive, new List<IComboPassiveTrigger>(triggers));
            return passive;
        }

        private static void AddToRunState(ComboPassiveSO passive)
        {
            // Add directo al state — svc.Apply también aplica StatGrants y dispara
            // OnItemObtained, ruido innecesario para estos tests.
            ServiceLocator.GetService<RunComboPassivesState>().Add(passive);
        }

        private static StubPlayerService RegisterPlayer()
        {
            var player = new StubPlayerService();
            ServiceLocator.AddService<IPlayerService>(player);
            return player;
        }

        // ---- Stubs ----------------------------------------------------

        private sealed class RecordingTrigger :
            IOnTurnStartedPassiveTrigger, IOnTurnFinishedPassiveTrigger,
            IOnRoomEnteredPassiveTrigger, IOnCombatStartPassiveTrigger,
            IOnCombatEndPassiveTrigger, IOnGoldChangedPassiveTrigger,
            IOnDiceRolledPassiveTrigger, IOnRollResolvedPassiveTrigger,
            IOnDamageResolvedPassiveTrigger
        {
            public readonly List<(string Hook, ComboPassiveContext Ctx)> Calls
                = new List<(string, ComboPassiveContext)>();

            public void OnTurnStarted(ComboPassiveContext ctx) => Calls.Add(("TurnStarted", ctx));
            public void OnTurnFinished(ComboPassiveContext ctx) => Calls.Add(("TurnFinished", ctx));
            public void OnRoomEntered(ComboPassiveContext ctx) => Calls.Add(("RoomEntered", ctx));
            public void OnCombatStart(ComboPassiveContext ctx) => Calls.Add(("CombatStart", ctx));
            public void OnCombatEnd(ComboPassiveContext ctx) => Calls.Add(("CombatEnd", ctx));
            public void OnGoldChanged(ComboPassiveContext ctx) => Calls.Add(("GoldChanged", ctx));
            public void OnDiceRolled(ComboPassiveContext ctx) => Calls.Add(("DiceRolled", ctx));
            public void OnRollResolved(ComboPassiveContext ctx) => Calls.Add(("RollResolved", ctx));
            public void OnDamageResolved(ComboPassiveContext ctx) => Calls.Add(("DamageResolved", ctx));
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; } = Guid.NewGuid();
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet { add { } remove { } }
            public event Action OnPlayerCleared { add { } remove { } }
        }

        /// <summary>Economy stub que emite OnGoldChanged como el real — para el test de reentrada.</summary>
        private sealed class EventFiringEconomyStub : IEconomyService
        {
            public int CurrentGold { get; private set; }

            public void Add(int amount)
            {
                if (amount <= 0) return;
                CurrentGold += amount;
                EventManager.Trigger(EventName.OnGoldChanged, CurrentGold, amount);
            }

            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                EventManager.Trigger(EventName.OnGoldChanged, CurrentGold, -amount);
                return true;
            }

            public bool CanAfford(int amount) => amount <= 0 || CurrentGold >= amount;

            public void ResetTo(int amount)
            {
                CurrentGold = amount;
                EventManager.Trigger(EventName.OnGoldChanged, CurrentGold, CurrentGold);
            }
        }

        private sealed class GoldOnGoldChangedTrigger : IOnGoldChangedPassiveTrigger
        {
            public int Dispatches;

            public void OnGoldChanged(ComboPassiveContext ctx)
            {
                Dispatches++;
                ctx.Scratch.BonusGold += 5;
            }
        }

        // ================================================================
        // Dispatch a todas las pasivas + filtro de player turn
        // ================================================================

        [Test]
        public void OnTurnStarted_PlayerGuid_DispatchesToAllOwnedPassivesRegardlessOfTargetCombo()
        {
            TriggerRunStart();
            var player = RegisterPlayer();
            var parTrigger = new RecordingTrigger();
            var trioTrigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", "combo.par", parTrigger));
            AddToRunState(MakePassive("p2", "combo.trio", trioTrigger));

            EventManager.Trigger(EventName.OnTurnStarted, player.PlayerGuid);

            Assert.AreEqual(1, parTrigger.Calls.Count, "El hook genérico ignora TargetComboId.");
            Assert.AreEqual(1, trioTrigger.Calls.Count, "Ambas pasivas deben recibir el dispatch.");
            Assert.AreEqual("TurnStarted", parTrigger.Calls[0].Hook);
            Assert.IsNull(parTrigger.Calls[0].Ctx.ComboId, "ComboId debe ser null en hooks genéricos.");
        }

        [Test]
        public void OnTurnStarted_NonPlayerGuid_DoesNotDispatch()
        {
            TriggerRunStart();
            RegisterPlayer();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", "combo.par", trigger));

            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid()); // guid de un enemigo

            Assert.AreEqual(0, trigger.Calls.Count, "Los turnos de enemigos no disparan pasivas.");
        }

        [Test]
        public void OnTurnFinished_PlayerGuid_DispatchesHook()
        {
            TriggerRunStart();
            var player = RegisterPlayer();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", null, trigger));

            EventManager.Trigger(EventName.OnTurnFinished, player.PlayerGuid);

            Assert.AreEqual(1, trigger.Calls.Count);
            Assert.AreEqual("TurnFinished", trigger.Calls[0].Hook);
        }

        // ================================================================
        // Payloads por evento
        // ================================================================

        [Test]
        public void OnRoomEntered_PopulatesRoomIdAndInstanceIdOnContext()
        {
            TriggerRunStart();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", null, trigger));
            var roomInstanceId = Guid.NewGuid();

            EventManager.Trigger(EventName.OnRoomEntered, roomInstanceId, "room.shop");

            Assert.AreEqual(1, trigger.Calls.Count);
            var ctx = trigger.Calls[0].Ctx;
            Assert.AreEqual(roomInstanceId, ctx.RoomInstanceId);
            Assert.AreEqual("room.shop", ctx.RoomId);
        }

        [Test]
        public void OnRoomEntered_WithoutArgs_DoesNotDispatch()
        {
            TriggerRunStart();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", null, trigger));

            // Varios tests del proyecto disparan OnRoomEntered sin args — el handler
            // debe ser defensivo y no dispatchar (no hay sala que reportar).
            EventManager.Trigger(EventName.OnRoomEntered);

            Assert.AreEqual(0, trigger.Calls.Count);
        }

        [Test]
        public void OnCombatEnd_WithOutcome_PopulatesOutcome()
        {
            TriggerRunStart();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", null, trigger));
            var roomInstanceId = Guid.NewGuid();

            EventManager.Trigger(EventName.OnCombatEnd, roomInstanceId, CombatOutcome.Victory);

            Assert.AreEqual(1, trigger.Calls.Count);
            Assert.AreEqual(CombatOutcome.Victory, trigger.Calls[0].Ctx.Outcome);
            Assert.AreEqual(roomInstanceId, trigger.Calls[0].Ctx.RoomInstanceId);
        }

        [Test]
        public void OnCombatEnd_MissingOutcomeArg_DispatchesWithSentinel()
        {
            TriggerRunStart();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", null, trigger));

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.AreEqual(1, trigger.Calls.Count, "Sin outcome se dispatcha igual (sentinel).");
            Assert.AreEqual(CombatOutcome.None, trigger.Calls[0].Ctx.Outcome);
        }

        [Test]
        public void OnGoldChanged_PopulatesTotalAndDelta()
        {
            TriggerRunStart();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", null, trigger));

            EventManager.Trigger(EventName.OnGoldChanged, 25, 10);

            Assert.AreEqual(1, trigger.Calls.Count);
            Assert.AreEqual(25, trigger.Calls[0].Ctx.GoldTotal);
            Assert.AreEqual(10, trigger.Calls[0].Ctx.GoldDelta);
        }

        [Test]
        public void OnRollResolved_PopulatesDiceResultOnEffectContext()
        {
            TriggerRunStart();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", null, trigger));
            var faces = new List<int> { 1, 3, 3, 5, 6 };

            EventManager.Trigger(EventName.OnRollResolved, Guid.NewGuid(), (IReadOnlyList<int>)faces);

            Assert.AreEqual(1, trigger.Calls.Count);
            Assert.AreEqual("RollResolved", trigger.Calls[0].Hook);
            CollectionAssert.AreEqual(faces, trigger.Calls[0].Ctx.Effect.DiceResult);
        }

        [Test]
        public void DamageResolvedTypedEvent_PopulatesDamagePayload()
        {
            TriggerRunStart();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", null, trigger));
            var source = Guid.NewGuid();
            var target = Guid.NewGuid();

            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = source,
                TargetGuid = target,
                FinalDamage = 7,
            });

            Assert.AreEqual(1, trigger.Calls.Count);
            var ctx = trigger.Calls[0].Ctx;
            Assert.IsTrue(ctx.Damage.HasValue);
            Assert.AreEqual(7, ctx.Damage.Value.FinalDamage);
            Assert.AreEqual(source, ctx.Effect.SourceGuid);
            Assert.AreEqual(target, ctx.Effect.TargetGuid);
        }

        // ================================================================
        // Guard de reentrada (oro) + LastComboScratch intacto
        // ================================================================

        [Test]
        public void OnGoldChanged_DuringScratchApply_DoesNotRedispatch()
        {
            TriggerRunStart();
            RegisterPlayer();
            var economy = new EventFiringEconomyStub();
            ServiceLocator.AddService<IEconomyService>(economy);
            var trigger = new GoldOnGoldChangedTrigger();
            AddToRunState(MakePassive("p1", null, trigger));

            // Cambio de oro "externo" → dispatch → el trigger da +5 oro → el applier
            // llama economy.Add(5) → OnGoldChanged de nuevo → el guard corta acá.
            EventManager.Trigger(EventName.OnGoldChanged, 10, 10);

            Assert.AreEqual(1, trigger.Dispatches,
                "El oro movido por la aplicación del scratch no debe re-disparar pasivas.");
            Assert.AreEqual(5, economy.CurrentGold, "El +5 del trigger sí debe aplicarse (una sola vez).");
        }

        [Test]
        public void GenericEvent_DoesNotOverwriteLastComboScratch()
        {
            TriggerRunStart();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", "combo.par", trigger));

            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = Guid.NewGuid(),
                ComboId = "combo.par",
                BaseDamage = 10,
            });
            var comboScratch = _svc.LastComboScratch;
            Assert.IsNotNull(comboScratch, "Precondición: el path de combo debe setear LastComboScratch.");

            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "room.any");

            Assert.AreSame(comboScratch, _svc.LastComboScratch,
                "Los hooks genéricos no deben tocar LastComboScratch (contrato del damage pipeline).");
        }

        // ================================================================
        // ExecuteEffectsOnEvent — integración event → service → effects
        // ================================================================

        private sealed class CountingEffect : Rollgeon.Effects.BaseEffect
        {
            public int Applies;
            protected override bool ShowSelection => false;
            public override bool ApplyEffect(Rollgeon.Effects.EffectContext context)
            {
                Applies++;
                return true;
            }
        }

        [Test]
        public void ExecuteEffectsOnEvent_ViaServiceDispatch_RunsEffectsOnConfiguredEvent()
        {
            TriggerRunStart();
            var effect = new CountingEffect();
            var bridge = new Triggers.Concretes.ExecuteEffectsOnEvent
            {
                Event = Triggers.Concretes.ComboPassiveHookEvent.RoomEntered,
                Effects = new List<Rollgeon.Effects.EffectData>
                {
                    new Rollgeon.Effects.EffectData
                    {
                        Effects = new List<Rollgeon.Effects.IEffect> { effect },
                    },
                },
            };
            AddToRunState(MakePassive("p1", null, bridge));

            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "room.shop");
            EventManager.Trigger(EventName.OnCombatEnd); // evento no configurado — no debe ejecutar

            Assert.AreEqual(1, effect.Applies,
                "El bridge debe ejecutar sus EffectData solo en el evento configurado.");
        }

        // ================================================================
        // Unsubscribe
        // ================================================================

        [Test]
        public void Unsubscribe_StopsGenericDispatch()
        {
            TriggerRunStart();
            var trigger = new RecordingTrigger();
            AddToRunState(MakePassive("p1", null, trigger));

            _svc.UnsubscribeEventsForTests();
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "room.shop");

            Assert.AreEqual(0, trigger.Calls.Count);
        }
    }
}
