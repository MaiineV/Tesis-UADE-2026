using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes.Modifiers;

namespace Rollgeon.Attributes.Tests
{
    [TestFixture]
    public class AttributesManagerTests
    {
        private AttributesManager _mgr;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            OperationResolver.ClearCache();
            // Default: warning — queremos el flujo defensivo en la mayoria
            // de los tests.
            AttributesManager.LogMissingEntityAsWarning = true;
            _mgr = new AttributesManager();
        }

        [TearDown]
        public void TearDown()
        {
            _mgr?.Dispose();
            EventManager.ResetEventDictionary();
            AttributesManager.LogMissingEntityAsWarning = true;
        }

        private ModifiableAttributes BuildAttrs(int hp = 10, float resist = 0f)
        {
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<TestIntAttribute>(new TestIntAttribute(hp));
            attrs.SetAttribute<TestFloatAttribute>(new TestFloatAttribute(resist));
            return attrs;
        }

        // --- Registro -----------------------------------------------------

        [Test]
        public void RegisterAndLookup()
        {
            var id = Guid.NewGuid();
            _mgr.Register(id, BuildAttrs(hp: 50));

            Assert.IsTrue(_mgr.IsRegistered(id));
            Assert.AreEqual(50, _mgr.GetAttributeValue<TestIntAttribute, int>(id));
        }

        [Test]
        public void Register_GuidEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(() => _mgr.Register(Guid.Empty, BuildAttrs()));
        }

        [Test]
        public void Unregister_DropsEntity()
        {
            var id = Guid.NewGuid();
            _mgr.Register(id, BuildAttrs());
            _mgr.Unregister(id);
            Assert.IsFalse(_mgr.IsRegistered(id));
        }

        // --- Modify / Set -------------------------------------------------

        [Test]
        public void SetAttributeValue_TriggersOnAttributeChanged()
        {
            var id = Guid.NewGuid();
            _mgr.Register(id, BuildAttrs(hp: 10));

            Guid capturedId = Guid.Empty;
            Type capturedType = null;
            EventManager.Subscribe(EventName.OnAttributeChanged, args =>
            {
                capturedId = (Guid)args[0];
                capturedType = (Type)args[1];
            });

            _mgr.SetAttributeValue<TestIntAttribute, int>(id, 77);

            Assert.AreEqual(77, _mgr.GetAttributeValue<TestIntAttribute, int>(id));
            Assert.AreEqual(id, capturedId);
            Assert.AreEqual(typeof(TestIntAttribute), capturedType);
        }

        [Test]
        public void Modify_AppliesClosure()
        {
            var id = Guid.NewGuid();
            _mgr.Register(id, BuildAttrs(hp: 10));

            _mgr.Modify<TestIntAttribute, int>(id, v => v - 3);
            Assert.AreEqual(7, _mgr.GetAttributeValue<TestIntAttribute, int>(id));
        }

        // --- Modifier lifecycle -------------------------------------------

        [Test]
        public void AddModifier_TriggersEventsAndUpdatesModifiedValue()
        {
            var id = Guid.NewGuid();
            _mgr.Register(id, BuildAttrs(hp: 10));

            bool addedFired = false;
            Guid addedMod = Guid.Empty;
            EventManager.Subscribe(EventName.OnModifierAdded, args =>
            {
                addedFired = true;
                addedMod = (Guid)args[2];
            });

            var mod = new Modifier<int>(
                amount: 5, op: ModifierOperation.Add, duration: 0,
                carrierId: id, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            Assert.IsTrue(_mgr.AddModifier<TestIntAttribute, int>(id, mod));
            Assert.IsTrue(addedFired);
            Assert.AreEqual(mod.ModifierId, addedMod);

            Assert.AreEqual(15, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(id));
        }

        [Test]
        public void RemoveModifier_ClearsFromStack()
        {
            var id = Guid.NewGuid();
            _mgr.Register(id, BuildAttrs(hp: 10));

            var mod = new Modifier<int>(
                amount: 5, op: ModifierOperation.Add, duration: 0,
                carrierId: id, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);
            _mgr.AddModifier<TestIntAttribute, int>(id, mod);
            Assert.AreEqual(15, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(id));

            Assert.IsTrue(_mgr.RemoveModifier<TestIntAttribute, int>(id, mod.ModifierId));
            Assert.AreEqual(10, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(id));
        }

        [Test]
        public void Lifecycle_Turns_ExpiryCleansFromStack()
        {
            var id = Guid.NewGuid();
            _mgr.Register(id, BuildAttrs(hp: 10));

            var mod = new Modifier<int>(
                amount: 5, op: ModifierOperation.Add, duration: 2,
                carrierId: id, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Turns,
                tickEvent: EventName.OnTurnFinished);
            _mgr.AddModifier<TestIntAttribute, int>(id, mod);

            Assert.AreEqual(15, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(id));

            EventManager.Trigger(EventName.OnTurnFinished, id);
            Assert.AreEqual(15, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(id));

            EventManager.Trigger(EventName.OnTurnFinished, id);
            // Segundo tick expira → el handler del manager lo limpia.
            Assert.AreEqual(10, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(id));
        }

        [Test]
        public void Lifecycle_Encounter_CombatEndCleansFromStack()
        {
            var id = Guid.NewGuid();
            _mgr.Register(id, BuildAttrs(hp: 10));

            var mod = new Modifier<int>(
                amount: 5, op: ModifierOperation.Add, duration: 0,
                carrierId: id, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Encounter,
                tickEvent: EventName.OnTurnFinished);
            _mgr.AddModifier<TestIntAttribute, int>(id, mod);
            Assert.AreEqual(15, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(id));

            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());
            Assert.AreEqual(10, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(id));
        }

        // --- RemoveBySource ------------------------------------------------

        [Test]
        public void RemoveModifierBySource_PerEntityPerAttribute()
        {
            var id = Guid.NewGuid();
            var boss = Guid.NewGuid();
            _mgr.Register(id, BuildAttrs(hp: 10));

            var mBoss = new Modifier<int>(
                amount: 2, op: ModifierOperation.Add, duration: 0,
                carrierId: id, sourceId: boss,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);
            var mOther = new Modifier<int>(
                amount: 3, op: ModifierOperation.Add, duration: 0,
                carrierId: id, sourceId: Guid.NewGuid(),
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            _mgr.AddModifier<TestIntAttribute, int>(id, mBoss);
            _mgr.AddModifier<TestIntAttribute, int>(id, mOther);

            Assert.AreEqual(15, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(id));

            int cleaned = _mgr.RemoveModifierBySource<TestIntAttribute, int>(id, boss);
            Assert.AreEqual(1, cleaned);

            // Debe quedar solo mOther = 10 + 3 = 13.
            Assert.AreEqual(13, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(id));
        }

        [Test]
        public void RemoveAllModifiersBySource_CleansAcrossEntitiesAndAttributes()
        {
            var boss = Guid.NewGuid();
            var other = Guid.NewGuid();

            var e1 = Guid.NewGuid();
            var e2 = Guid.NewGuid();
            _mgr.Register(e1, BuildAttrs(hp: 10, resist: 0f));
            _mgr.Register(e2, BuildAttrs(hp: 20, resist: 1f));

            var bossModE1Int = new Modifier<int>(
                amount: 5, op: ModifierOperation.Add, duration: 0,
                carrierId: e1, sourceId: boss,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);
            var bossModE2Float = new Modifier<float>(
                amount: 1f, op: ModifierOperation.Add, duration: 0,
                carrierId: e2, sourceId: boss,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);
            var otherModE1Int = new Modifier<int>(
                amount: 7, op: ModifierOperation.Add, duration: 0,
                carrierId: e1, sourceId: other,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            _mgr.AddModifier<TestIntAttribute, int>(e1, bossModE1Int);
            _mgr.AddModifier<TestFloatAttribute, float>(e2, bossModE2Float);
            _mgr.AddModifier<TestIntAttribute, int>(e1, otherModE1Int);

            // Pre-checks:
            Assert.AreEqual(22, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(e1)); // 10+5+7
            Assert.AreEqual(2f, _mgr.GetAttributeModifiedValue<TestFloatAttribute, float>(e2), 0.0001f); // 1+1

            int cleaned = _mgr.RemoveAllModifiersBySource(boss);
            Assert.AreEqual(2, cleaned, "debe limpiar mods del boss en ambas entidades y atributos");

            // Post-checks: el mod de 'other' en e1 sigue; e2 vuelve a su valor base.
            Assert.AreEqual(17, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(e1)); // 10+7
            Assert.AreEqual(1f, _mgr.GetAttributeModifiedValue<TestFloatAttribute, float>(e2), 0.0001f);
        }

        [Test]
        public void RemoveAllModifiersBySource_GuidEmpty_IsNoOp()
        {
            var e1 = Guid.NewGuid();
            _mgr.Register(e1, BuildAttrs(hp: 10));

            // Tres mods con SourceId == Guid.Empty (auto-infligidos / stat boost anonimo).
            for (int i = 0; i < 3; i++)
            {
                var m = new Modifier<int>(
                    amount: 1, op: ModifierOperation.Add, duration: 0,
                    carrierId: e1, sourceId: Guid.Empty,
                    dir: ModifierDirection.Intrinsic,
                    lifetime: ModifierLifetime.Permanent,
                    tickEvent: EventName.OnTurnFinished);
                _mgr.AddModifier<TestIntAttribute, int>(e1, m);
            }

            Assert.AreEqual(13, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(e1));

            // Safety rail: NO debe limpiar mods anonimos.
            int cleaned = _mgr.RemoveAllModifiersBySource(Guid.Empty);
            Assert.AreEqual(0, cleaned);
            Assert.AreEqual(13, _mgr.GetAttributeModifiedValue<TestIntAttribute, int>(e1),
                "RemoveAllModifiersBySource(Guid.Empty) debe ser no-op");
        }

        [Test]
        public void MissingEntity_WarnMode_ReturnsDefaultWithoutThrow()
        {
            AttributesManager.LogMissingEntityAsWarning = true;
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*not registered.*"));

            int v = _mgr.GetAttributeValue<TestIntAttribute, int>(Guid.NewGuid());
            Assert.AreEqual(0, v);
        }
    }
}
