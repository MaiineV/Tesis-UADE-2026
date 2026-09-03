using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Covers which passive item owns which event subscription. The service had no tests, and the
    /// bug below survived precisely because one authored item can't expose it.
    /// </summary>
    public sealed class PassiveHookBindingTests
    {
        const EventName TriggerEvent = EventName.OnTurnStarted;

        readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        InventoryService _service;
        Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _playerGuid = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_playerGuid));

            Eff_Record.Log.Clear();
            _service = new InventoryService(null, 4);
        }

        [TearDown]
        public void TearDown()
        {
            // The handlers live on the static EventManager; a leaked subscription would fire in the
            // next test.
            _service?.Dispose();
            _service = null;

            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            ServiceLocator.Clear();
            Eff_Record.Log.Clear();
        }

        ItemSO NewPassive(string id, string tag)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Passive;

            var hook = new PassiveItemHook { TriggerEvent = TriggerEvent };
            hook.Effect.Effects.Add(new Eff_Record { Tag = tag });
            item.PassiveHooks.Add(hook);

            _spawned.Add(item);
            return item;
        }

        void FireTrigger() => EventManager.Trigger(TriggerEvent, _playerGuid);

        // ---- the bug --------------------------------------------------------

        /// <summary>
        /// The regression this suite exists for. UnbindPassiveHooks used to match handlers by
        /// TriggerEvent and remove the last one it found, so with two passives on the same event,
        /// removing A unsubscribed B and left A firing — exactly backwards.
        /// </summary>
        [Test]
        public void RemoveItem_TwoPassivesOnTheSameEvent_UnbindsOnlyTheOneRemoved()
        {
            _service.AddItem(NewPassive("item.a", "A"));
            _service.AddItem(NewPassive("item.b", "B"));

            _service.RemoveItem("item.a");
            FireTrigger();

            CollectionAssert.AreEqual(new[] { "B" }, Eff_Record.Log,
                "removing A must silence A and leave B firing");
        }

        [Test]
        public void RemoveItem_TheSecondOfTwo_LeavesTheFirstFiring()
        {
            _service.AddItem(NewPassive("item.a", "A"));
            _service.AddItem(NewPassive("item.b", "B"));

            _service.RemoveItem("item.b");
            FireTrigger();

            CollectionAssert.AreEqual(new[] { "A" }, Eff_Record.Log);
        }

        [Test]
        public void RemoveItem_BothPassives_SilencesEverything()
        {
            _service.AddItem(NewPassive("item.a", "A"));
            _service.AddItem(NewPassive("item.b", "B"));

            _service.RemoveItem("item.a");
            _service.RemoveItem("item.b");
            FireTrigger();

            CollectionAssert.IsEmpty(Eff_Record.Log);
        }

        // ---- surrounding behaviour ------------------------------------------

        [Test]
        public void AddItem_Passive_FiresOnItsTriggerEvent()
        {
            _service.AddItem(NewPassive("item.a", "A"));

            FireTrigger();

            CollectionAssert.AreEqual(new[] { "A" }, Eff_Record.Log);
        }

        [Test]
        public void AddItem_ThreePassivesOnTheSameEvent_AllFire()
        {
            _service.AddItem(NewPassive("item.a", "A"));
            _service.AddItem(NewPassive("item.b", "B"));
            _service.AddItem(NewPassive("item.c", "C"));

            FireTrigger();

            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, Eff_Record.Log);
        }

        /// <summary>
        /// A hook fires for the player, not for whoever else the event mentions — args[0] is the
        /// entity the event is about (§18 convention).
        /// </summary>
        [Test]
        public void PassiveHook_EventAboutAnotherEntity_DoesNotFire()
        {
            _service.AddItem(NewPassive("item.a", "A"));

            EventManager.Trigger(TriggerEvent, Guid.NewGuid());

            CollectionAssert.IsEmpty(Eff_Record.Log);
        }

        [Test]
        public void RemoveItem_UnknownId_ChangesNothing()
        {
            _service.AddItem(NewPassive("item.a", "A"));

            Assert.IsFalse(_service.RemoveItem("item.nope"));
            FireTrigger();

            CollectionAssert.AreEqual(new[] { "A" }, Eff_Record.Log);
        }

        [Test]
        public void Dispose_SilencesEveryHook()
        {
            _service.AddItem(NewPassive("item.a", "A"));
            _service.AddItem(NewPassive("item.b", "B"));

            _service.Dispose();
            _service = null;
            FireTrigger();

            CollectionAssert.IsEmpty(Eff_Record.Log);
        }

        /// <summary>
        /// Unbind keys off what was bound, not off the SO's current hooks. Editing the asset between
        /// add and remove used to strand a live subscription on a removed item.
        /// </summary>
        [Test]
        public void RemoveItem_AfterItsHooksWereEditedAway_StillUnbinds()
        {
            var item = NewPassive("item.a", "A");
            _service.AddItem(item);

            item.PassiveHooks.Clear();   // as if the asset were re-authored mid-run

            _service.RemoveItem("item.a");
            FireTrigger();

            CollectionAssert.IsEmpty(Eff_Record.Log,
                "the handler was bound, so it must be unbound regardless of the SO's current state");
        }

        // ---- dados en hooks de bus ----------------------------------------------

        /// <summary>
        /// OnRollResolved lleva la tirada como arg: un hook de bus (Bolsa del Impar) tiene
        /// que verla en <c>EffectContext.DiceResult</c>; sin dados en el evento queda null.
        /// </summary>
        [Test]
        public void EventBusHook_RollEventWithDice_ExposesDiceResultToTheEffect()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.dice";
            item.DisplayName = "item.dice";
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook { TriggerEvent = EventName.OnRollResolved };
            hook.Effect.Effects.Add(new Eff_RecordDice());
            item.PassiveHooks.Add(hook);
            _spawned.Add(item);
            _service.AddItem(item);

            var faces = new[] { 1, 4, 7, 8 };
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid, (IReadOnlyList<int>)faces,
                Rollgeon.Combat.Rolls.RollActionKind.Attack, null);
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            Assert.AreEqual(2, Eff_RecordDice.Seen.Count);
            CollectionAssert.AreEqual(faces, Eff_RecordDice.Seen[0]);
            Assert.IsNull(Eff_RecordDice.Seen[1], "un evento sin dados no inventa una tirada");
            Eff_RecordDice.Seen.Clear();
        }

        // ---- helpers ---------------------------------------------------------

        /// <summary>Records that it ran, so a test can tell which item's hook fired.</summary>
        [Serializable]
        sealed class Eff_Record : BaseEffect
        {
            public static readonly List<string> Log = new List<string>();
            public string Tag;

            public override string GetEffectName() => "Record";
            public override bool ApplyEffect(EffectContext context)
            {
                Log.Add(Tag);
                return true;
            }
        }

        /// <summary>Captures the dice the context carried (null when the event had none).</summary>
        [Serializable]
        sealed class Eff_RecordDice : BaseEffect
        {
            public static readonly List<IReadOnlyList<int>> Seen = new List<IReadOnlyList<int>>();

            public override string GetEffectName() => "RecordDice";
            public override bool ApplyEffect(EffectContext context)
            {
                Seen.Add(context.DiceResult);
                return true;
            }
        }

        /// <summary>Only <see cref="PlayerGuid"/> matters here — that's all the hooks filter on.</summary>
        sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid guid) { PlayerGuid = guid; }

            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }

#pragma warning disable CS0067 // never raised: nothing under test listens to these
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
