using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes.Modifiers;

namespace Rollgeon.Attributes.Tests
{
    [TestFixture]
    public class ModifierTests
    {
        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            OperationResolver.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.ResetEventDictionary();
        }

        // --- Int ops -------------------------------------------------------

        [Test]
        public void IntAdd_AppliesAmount()
        {
            var mod = new Modifier<int>(
                amount: 5, op: ModifierOperation.Add, duration: 0,
                carrierId: Guid.NewGuid(), sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            Assert.AreEqual(15, mod.ApplyModifier(10));
        }

        [Test]
        public void IntSubtract_AppliesAmount()
        {
            var mod = new Modifier<int>(
                amount: 3, op: ModifierOperation.Subtract, duration: 0,
                carrierId: Guid.NewGuid(), sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            Assert.AreEqual(7, mod.ApplyModifier(10));
        }

        [Test]
        public void IntMultiply_AppliesAmount()
        {
            var mod = new Modifier<int>(
                amount: 3, op: ModifierOperation.Multiply, duration: 0,
                carrierId: Guid.NewGuid(), sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            Assert.AreEqual(12, mod.ApplyModifier(4));
        }

        [Test]
        public void IntOverride_ReplacesValue()
        {
            var mod = new Modifier<int>(
                amount: 99, op: ModifierOperation.Override, duration: 0,
                carrierId: Guid.NewGuid(), sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            Assert.AreEqual(99, mod.ApplyModifier(10));
        }

        // --- Float ops -----------------------------------------------------

        [Test]
        public void FloatPercent_IncreasesByFraction()
        {
            var mod = new Modifier<float>(
                amount: 0.5f, op: ModifierOperation.Percent, duration: 0,
                carrierId: Guid.NewGuid(), sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            Assert.AreEqual(15f, mod.ApplyModifier(10f), 0.0001f);
        }

        // --- Bool ops ------------------------------------------------------

        [Test]
        public void BoolSet_ForcesAmount()
        {
            var mod = new Modifier<bool>(
                amount: true, op: ModifierOperation.Set, duration: 0,
                carrierId: Guid.NewGuid(), sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            Assert.IsTrue(mod.ApplyModifier(false));
        }

        [Test]
        public void BoolAnd_Conjunction()
        {
            var mod = new Modifier<bool>(
                amount: true, op: ModifierOperation.And, duration: 0,
                carrierId: Guid.NewGuid(), sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            Assert.IsTrue(mod.ApplyModifier(true));
            // Recreate to avoid stale _resolvedOp state.
            Assert.IsFalse(mod.ApplyModifier(false));
        }

        [Test]
        public void BoolOr_Disjunction()
        {
            var mod = new Modifier<bool>(
                amount: true, op: ModifierOperation.Or, duration: 0,
                carrierId: Guid.NewGuid(), sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            Assert.IsTrue(mod.ApplyModifier(false));
        }

        [Test]
        public void OperationResolver_ThrowsOnIncompatibleTypeOp()
        {
            Assert.Throws<NotSupportedException>(() =>
                OperationResolver.Resolve<int>(ModifierOperation.And));
        }

        // --- Lifecycle -----------------------------------------------------

        [Test]
        public void Lifecycle_Turns_ExpiresAfterNTicks()
        {
            var carrier = Guid.NewGuid();
            bool removedFired = false;
            Guid removedId = Guid.Empty;

            EventManager.Subscribe(EventName.OnModifierRemoved, args =>
            {
                removedFired = true;
                removedId = (Guid)args[1];
            });

            var mod = new Modifier<int>(
                amount: 2, op: ModifierOperation.Add, duration: 3,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Turns,
                tickEvent: EventName.OnTurnFinished);

            Guid expectedId = mod.ModifierId;

            // 3 ticks para expirar.
            EventManager.Trigger(EventName.OnTurnFinished, carrier);
            Assert.IsFalse(removedFired, "should not remove on first tick");

            EventManager.Trigger(EventName.OnTurnFinished, carrier);
            Assert.IsFalse(removedFired, "should not remove on second tick");

            EventManager.Trigger(EventName.OnTurnFinished, carrier);
            Assert.IsTrue(removedFired, "should remove on third tick");
            Assert.AreEqual(expectedId, removedId);
        }

        [Test]
        public void Lifecycle_Turns_DoesNotTickOnWrongCarrier()
        {
            var carrier = Guid.NewGuid();
            var other = Guid.NewGuid();
            bool removedFired = false;
            EventManager.Subscribe(EventName.OnModifierRemoved, _ => removedFired = true);

            var mod = new Modifier<int>(
                amount: 1, op: ModifierOperation.Add, duration: 1,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Turns,
                tickEvent: EventName.OnTurnFinished);

            // Otro Guid — no debe decrementar ni remover.
            EventManager.Trigger(EventName.OnTurnFinished, other);
            Assert.IsFalse(removedFired);
            Assert.AreEqual(1, mod.Duration);

            // El del carrier SI decrementa y remueve.
            EventManager.Trigger(EventName.OnTurnFinished, carrier);
            Assert.IsTrue(removedFired);
        }

        [Test]
        public void Lifecycle_Encounter_RemovesOnCombatEnd()
        {
            var carrier = Guid.NewGuid();
            bool removedFired = false;
            EventManager.Subscribe(EventName.OnModifierRemoved, _ => removedFired = true);

            var mod = new Modifier<int>(
                amount: 1, op: ModifierOperation.Add, duration: 0,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Encounter,
                tickEvent: EventName.OnTurnFinished);

            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());
            Assert.IsTrue(removedFired);
        }

        [Test]
        public void Lifecycle_Run_RemovesOnRunEnd()
        {
            var carrier = Guid.NewGuid();
            bool removedFired = false;
            EventManager.Subscribe(EventName.OnModifierRemoved, _ => removedFired = true);

            var mod = new Modifier<int>(
                amount: 1, op: ModifierOperation.Add, duration: 0,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Run,
                tickEvent: EventName.OnTurnFinished);

            EventManager.Trigger(EventName.OnRunEnd, Guid.NewGuid());
            Assert.IsTrue(removedFired);
        }

        [Test]
        public void Lifecycle_Permanent_DoesNotReactToScopeEvents()
        {
            var carrier = Guid.NewGuid();
            bool removedFired = false;
            EventManager.Subscribe(EventName.OnModifierRemoved, _ => removedFired = true);

            var mod = new Modifier<int>(
                amount: 1, op: ModifierOperation.Add, duration: 0,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            EventManager.Trigger(EventName.OnTurnFinished, carrier);
            EventManager.Trigger(EventName.OnCombatEnd);
            EventManager.Trigger(EventName.OnRunEnd);

            Assert.IsFalse(removedFired);
        }

        // --- OnLoad idempotente -------------------------------------------

        [Test]
        public void OnLoad_Idempotent_DoesNotDoubleSubscribe()
        {
            var carrier = Guid.NewGuid();
            int removedCount = 0;
            EventManager.Subscribe(EventName.OnModifierRemoved, _ => removedCount++);

            var mod = new Modifier<int>(
                amount: 1, op: ModifierOperation.Add, duration: 1,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Turns,
                tickEvent: EventName.OnTurnFinished);

            // Simula la rehidratacion desde save: varias llamadas a OnLoad.
            mod.OnLoad();
            mod.OnLoad();
            mod.OnLoad();

            // Un solo tick debe dispar 1 sola remocion, aunque haya hubieron
            // tres OnLoad (no deben acumularse subscripciones).
            EventManager.Trigger(EventName.OnTurnFinished, carrier);
            Assert.AreEqual(1, removedCount);
        }
    }
}
