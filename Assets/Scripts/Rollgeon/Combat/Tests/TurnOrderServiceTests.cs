using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Balance;
using Rollgeon.Combat.Initiative;
using UnityEngine;

namespace Rollgeon.Combat.Tests
{
    [TestFixture]
    public class TurnOrderServiceTests
    {
        private InMemoryEntityRegistry _registry;
        private RulesetSO _ruleset;
        private TurnOrderService _service;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _registry = new InMemoryEntityRegistry();

            _ruleset = ScriptableObject.CreateInstance<RulesetSO>();
            _ruleset.TurnOrder = new TurnOrderConfig
            {
                SpeedDieMin = 1,
                SpeedDieMax = 6,
                FallbackInitiativeForMissingSpeed = 0,
            };
            ServiceLocator.AddService<RulesetSO>(_ruleset);

            _service = new TurnOrderService();
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            if (_ruleset != null)
            {
                ScriptableObject.DestroyImmediate(_ruleset);
            }
        }

        // --- Helpers ------------------------------------------------------

        private Guid RegisterEntityWithSpeed(int speed)
        {
            var id = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<Speed>(new Speed(speed));
            _registry.Register(id, attrs);
            return id;
        }

        private void InstallProvider(params int[] dieRolls)
        {
            var rng = new FixedInitiativeRng(fallback: 1, values: dieRolls);
            var provider = new DefaultInitiativeProvider(_registry, rng, _ruleset);
            ServiceLocator.AddService<IInitiativeProvider>(provider);
        }

        // --- Orden por initiative -----------------------------------------

        [Test]
        public void BuildForCombat_ProducesDescendingOrderBySpeed()
        {
            var slow = RegisterEntityWithSpeed(1);
            var mid = RegisterEntityWithSpeed(5);
            var fast = RegisterEntityWithSpeed(9);

            // Force die = 1 para todos, así la diferencia viene del Speed base.
            InstallProvider(1, 1, 1);

            _service.BuildForCombat(new[] { slow, mid, fast });

            Assert.AreEqual(fast, _service.OrderForRound[0]);
            Assert.AreEqual(mid, _service.OrderForRound[1]);
            Assert.AreEqual(slow, _service.OrderForRound[2]);
            Assert.AreEqual(fast, _service.Current);
        }

        // --- Evento -------------------------------------------------------

        [Test]
        public void BuildForCombat_FiresOnTurnQueueBuilt()
        {
            var a = RegisterEntityWithSpeed(5);
            var b = RegisterEntityWithSpeed(3);
            InstallProvider(1, 1);

            bool fired = false;
            IReadOnlyList<Guid> capturedOrder = null;
            int capturedRound = -999;
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, args =>
            {
                fired = true;
                capturedOrder = (IReadOnlyList<Guid>)args[0];
                capturedRound = (int)args[1];
            });

            _service.BuildForCombat(new[] { a, b });

            Assert.IsTrue(fired);
            Assert.IsNotNull(capturedOrder);
            Assert.AreEqual(2, capturedOrder.Count);
            Assert.AreEqual(a, capturedOrder[0]);
            Assert.AreEqual(0, capturedRound);
        }

        [Test]
        public void OnTurnQueueBuilt_PayloadIsSnapshot_NotLiveReference()
        {
            var a = RegisterEntityWithSpeed(5);
            var b = RegisterEntityWithSpeed(3);
            InstallProvider(1, 1);

            IReadOnlyList<Guid> captured = null;
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, args =>
            {
                captured = (IReadOnlyList<Guid>)args[0];
            });

            _service.BuildForCombat(new[] { a, b });

            // La copia debe ser distinta de la instancia viva del servicio.
            Assert.AreNotSame(_service.OrderForRound, captured,
                "El payload debe ser copia, no referencia mutable al estado interno.");
        }

        // --- Tie-break ----------------------------------------------------

        [Test]
        public void BuildForCombat_TieByInitiative_OrdersByGuidAscending()
        {
            // Mismos Speed + mismo die → desempate por Guid ASC.
            var gLow = new Guid("00000000-0000-0000-0000-000000000001");
            var gHigh = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff");
            var attrsLow = new ModifiableAttributes();
            attrsLow.SetAttribute<Speed>(new Speed(5));
            var attrsHigh = new ModifiableAttributes();
            attrsHigh.SetAttribute<Speed>(new Speed(5));
            _registry.Register(gLow, attrsLow);
            _registry.Register(gHigh, attrsHigh);

            InstallProvider(3, 3);

            _service.BuildForCombat(new[] { gHigh, gLow });

            Assert.AreEqual(gLow, _service.OrderForRound[0],
                "En tie de initiative, Guid menor va primero.");
            Assert.AreEqual(gHigh, _service.OrderForRound[1]);
        }

        [Test]
        public void BuildForCombat_StableTieBreakingByGuid_IsIndependentOfInputOrder()
        {
            var gLow = new Guid("00000000-0000-0000-0000-000000000001");
            var gHigh = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff");
            var attrsLow = new ModifiableAttributes();
            attrsLow.SetAttribute<Speed>(new Speed(5));
            var attrsHigh = new ModifiableAttributes();
            attrsHigh.SetAttribute<Speed>(new Speed(5));
            _registry.Register(gLow, attrsLow);
            _registry.Register(gHigh, attrsHigh);

            InstallProvider(2, 2);

            // Orden de input al revés del test anterior — el resultado debe ser
            // idéntico (tie-break es determinista por Guid, no por orden de llegada).
            _service.BuildForCombat(new[] { gLow, gHigh });

            Assert.AreEqual(gLow, _service.OrderForRound[0]);
            Assert.AreEqual(gHigh, _service.OrderForRound[1]);
        }

        // --- Advance / wrap-around ----------------------------------------

        [Test]
        public void Advance_MovesCursorAndReturnsNextCurrent()
        {
            var a = RegisterEntityWithSpeed(10);
            var b = RegisterEntityWithSpeed(5);
            InstallProvider(1, 1);

            _service.BuildForCombat(new[] { a, b });
            Assert.AreEqual(a, _service.Current);

            var next = _service.Advance();
            Assert.AreEqual(b, next);
            Assert.AreEqual(b, _service.Current);
        }

        [Test]
        public void Advance_WrapsAfterLastParticipant_IncrementsRoundIndex()
        {
            var a = RegisterEntityWithSpeed(10);
            var b = RegisterEntityWithSpeed(5);
            InstallProvider(1, 1);

            _service.BuildForCombat(new[] { a, b });
            Assert.AreEqual(0, _service.RoundIndex);

            _service.Advance(); // → b (cursor 1)
            Assert.AreEqual(0, _service.RoundIndex);

            var wrapped = _service.Advance(); // wrap → a (cursor 0, roundIndex++)
            Assert.AreEqual(a, wrapped);
            Assert.AreEqual(1, _service.RoundIndex);
        }

        [Test]
        public void Advance_WrapAround_RefiresOnTurnQueueBuiltWithSameOrderAndNewRoundIndex()
        {
            var a = RegisterEntityWithSpeed(10);
            var b = RegisterEntityWithSpeed(5);
            InstallProvider(1, 1);

            _service.BuildForCombat(new[] { a, b });

            var rounds = new List<int>();
            var orders = new List<IReadOnlyList<Guid>>();
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, args =>
            {
                orders.Add((IReadOnlyList<Guid>)args[0]);
                rounds.Add((int)args[1]);
            });

            _service.Advance(); // no wrap
            Assert.AreEqual(0, rounds.Count, "Advance sin wrap no debe re-disparar.");

            _service.Advance(); // wrap
            Assert.AreEqual(1, rounds.Count);
            Assert.AreEqual(1, rounds[0]);
            Assert.AreEqual(2, orders[0].Count);
            Assert.AreEqual(a, orders[0][0]);
            Assert.AreEqual(b, orders[0][1]);
        }

        // --- Edge cases ---------------------------------------------------

        [Test]
        public void BuildForCombat_EmptyList_Throws()
        {
            InstallProvider();
            Assert.Throws<InvalidOperationException>(
                () => _service.BuildForCombat(Array.Empty<Guid>()));
        }

        [Test]
        public void BuildForCombat_Null_Throws()
        {
            InstallProvider();
            Assert.Throws<ArgumentNullException>(
                () => _service.BuildForCombat(null));
        }

        [Test]
        public void SingleParticipant_FiresEvent()
        {
            var solo = RegisterEntityWithSpeed(3);
            InstallProvider(1);

            int fires = 0;
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, _ => fires++);

            _service.BuildForCombat(new[] { solo });

            Assert.AreEqual(1, fires);
            Assert.AreEqual(solo, _service.Current);
            Assert.AreEqual(1, _service.ParticipantCount);
        }

        [Test]
        public void SingleParticipant_Advance_WrapsAndIncrementsRound()
        {
            var solo = RegisterEntityWithSpeed(3);
            InstallProvider(1);

            _service.BuildForCombat(new[] { solo });
            Assert.AreEqual(0, _service.RoundIndex);

            var next = _service.Advance();
            Assert.AreEqual(solo, next);
            Assert.AreEqual(1, _service.RoundIndex);
        }

        // --- Modifiers ----------------------------------------------------

        [Test]
        public void BuildForCombat_UsesSpeedModifiedValue_NotRaw()
        {
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<Speed>(new Speed(5));

            // Add an intrinsic +10 modifier to Speed → ModifiedValue should be 15.
            var speed = attrs.GetAttribute<Speed>();
            var mod = new Rollgeon.Attributes.Modifiers.Modifier<int>(
                amount: 10,
                op: Rollgeon.Attributes.Modifiers.ModifierOperation.Add,
                duration: 0,
                carrierId: Guid.NewGuid(),
                sourceId: Guid.Empty,
                dir: Rollgeon.Attributes.Modifiers.ModifierDirection.Intrinsic,
                lifetime: Rollgeon.Attributes.Modifiers.ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);
            speed.AddModifier<int>(mod);

            var idBuffed = Guid.NewGuid();
            _registry.Register(idBuffed, attrs);

            var slowId = RegisterEntityWithSpeed(9); // base 9, sin buff.

            // Same die for both (1) → buffed (5+10+1=16) > slow (9+1=10).
            InstallProvider(1, 1);

            _service.BuildForCombat(new[] { slowId, idBuffed });

            Assert.AreEqual(idBuffed, _service.OrderForRound[0],
                "DefaultInitiativeProvider debe leer Speed.ModifiedValue, no Value raw.");
        }

        // --- Missing Speed fallback ---------------------------------------

        [Test]
        public void EntityWithoutSpeedStat_UsesFallbackInitiative()
        {
            var fast = RegisterEntityWithSpeed(10);

            var noSpeed = Guid.NewGuid();
            _registry.Register(noSpeed, new ModifiableAttributes());

            // fallback=0, die=1 → noSpeed initiative = 1.
            // fast = 10 + 1 = 11.
            InstallProvider(1, 1);

            _service.BuildForCombat(new[] { noSpeed, fast });

            Assert.AreEqual(fast, _service.OrderForRound[0]);
            Assert.AreEqual(noSpeed, _service.OrderForRound[1]);
        }

        // --- Remove -------------------------------------------------------

        [Test]
        public void Remove_BeforeCursor_DecrementsCursor()
        {
            var a = RegisterEntityWithSpeed(10);
            var b = RegisterEntityWithSpeed(5);
            var c = RegisterEntityWithSpeed(1);
            InstallProvider(1, 1, 1);

            _service.BuildForCombat(new[] { a, b, c });
            _service.Advance(); // cursor → b (index 1)

            _service.Remove(a); // remove index 0, before cursor

            Assert.AreEqual(2, _service.ParticipantCount);
            Assert.AreEqual(b, _service.Current, "Cursor should still point to b after removing a.");
        }

        [Test]
        public void Remove_AtCursor_CursorPointsToNextEntity()
        {
            var a = RegisterEntityWithSpeed(10);
            var b = RegisterEntityWithSpeed(5);
            var c = RegisterEntityWithSpeed(1);
            InstallProvider(1, 1, 1);

            _service.BuildForCombat(new[] { a, b, c });
            _service.Advance(); // cursor → b (index 1)

            _service.Remove(b); // remove at cursor

            Assert.AreEqual(2, _service.ParticipantCount);
            Assert.AreEqual(c, _service.Current, "After removing b at cursor, cursor should land on c.");
        }

        [Test]
        public void Remove_AfterCursor_CursorUnchanged()
        {
            var a = RegisterEntityWithSpeed(10);
            var b = RegisterEntityWithSpeed(5);
            var c = RegisterEntityWithSpeed(1);
            InstallProvider(1, 1, 1);

            _service.BuildForCombat(new[] { a, b, c });
            // cursor at a (index 0)

            _service.Remove(c); // remove index 2, after cursor

            Assert.AreEqual(2, _service.ParticipantCount);
            Assert.AreEqual(a, _service.Current, "Cursor should still point to a.");
        }

        [Test]
        public void Remove_LastRemainingEntity_EmptiesQueue()
        {
            var a = RegisterEntityWithSpeed(5);
            InstallProvider(1);

            _service.BuildForCombat(new[] { a });
            _service.Remove(a);

            Assert.AreEqual(0, _service.ParticipantCount);
        }

        [Test]
        public void Remove_UnknownGuid_ReturnsFalse()
        {
            var a = RegisterEntityWithSpeed(5);
            InstallProvider(1);

            _service.BuildForCombat(new[] { a });

            var result = _service.Remove(Guid.NewGuid());
            Assert.IsFalse(result);
            Assert.AreEqual(1, _service.ParticipantCount);
        }

        [Test]
        public void Remove_AtCursor_LastIndex_WrapsToZero()
        {
            var a = RegisterEntityWithSpeed(10);
            var b = RegisterEntityWithSpeed(5);
            InstallProvider(1, 1);

            _service.BuildForCombat(new[] { a, b });
            _service.Advance(); // cursor → b (index 1)

            _service.Remove(b); // remove at cursor (last index)

            Assert.AreEqual(1, _service.ParticipantCount);
            Assert.AreEqual(a, _service.Current, "Cursor should wrap to index 0.");
        }

        // --- Reset --------------------------------------------------------

        [Test]
        public void Reset_ClearsOrderAndResetsCounters()
        {
            var a = RegisterEntityWithSpeed(5);
            InstallProvider(1);
            _service.BuildForCombat(new[] { a });
            _service.Advance(); // roundIndex = 1

            _service.Reset();

            Assert.AreEqual(0, _service.ParticipantCount);
            Assert.AreEqual(0, _service.RoundIndex);
            Assert.Throws<InvalidOperationException>(() => { var _ = _service.Current; });
        }
    }
}
