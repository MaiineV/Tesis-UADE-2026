using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes.Modifiers;

namespace Rollgeon.Attributes.Tests
{
    [TestFixture]
    public class ModifiableAttributesTests
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

        [Test]
        public void Add_Has_Get_Remove_WorkAsExpected()
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();

            Assert.IsFalse(attrs.HasAttribute<TestIntAttribute>());

            attrs.SetAttribute<TestIntAttribute>(new TestIntAttribute(42));

            Assert.IsTrue(attrs.HasAttribute<TestIntAttribute>());
            Assert.AreEqual(42, attrs.GetAttributeValue<TestIntAttribute, int>());

            attrs.RemoveAttribute<TestIntAttribute>();
            Assert.IsFalse(attrs.HasAttribute<TestIntAttribute>());
        }

        [Test]
        public void GetAttribute_MissingKey_Throws()
        {
            var attrs = new ModifiableAttributes();
            Assert.Throws<KeyNotFoundException>(() => attrs.GetAttribute<TestIntAttribute>());
        }

        [Test]
        public void GetValue_WrongType_ThrowsInvalidCast()
        {
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<TestIntAttribute>(new TestIntAttribute(10));
            Assert.Throws<InvalidCastException>(() =>
                attrs.GetAttributeValue<TestIntAttribute, float>());
        }

        [Test]
        public void SetValue_WrongType_ThrowsInvalidCast()
        {
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<TestIntAttribute>(new TestIntAttribute(10));
            Assert.Throws<InvalidCastException>(() =>
                attrs.SetAttributeValue<TestIntAttribute, float>(3.14f));
        }

        [Test]
        public void SetValue_CorrectType_Updates()
        {
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<TestIntAttribute>(new TestIntAttribute(10));
            attrs.SetAttributeValue<TestIntAttribute, int>(77);
            Assert.AreEqual(77, attrs.GetAttributeValue<TestIntAttribute, int>());
        }

        [Test]
        public void DuplicateAttributes_ClonesValue_NotModifiers()
        {
            var attrs = new ModifiableAttributes();
            var original = new TestIntAttribute(5);

            // Agrega un modifier: NO debe copiarse en el duplicado.
            var carrier = Guid.NewGuid();
            var mod = new Modifier<int>(
                amount: 10, op: ModifierOperation.Add, duration: 0,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);
            original.AddModifier<int>(mod);

            attrs.SetAttribute<TestIntAttribute>(original);

            Assert.AreEqual(15, attrs.GetAttribute<TestIntAttribute>().ModifiedValue);

            var duplicate = attrs.DuplicateAttributes();
            var clonedAttr = duplicate.GetAttribute<TestIntAttribute>();

            // Valor crudo se duplico:
            Assert.AreEqual(5, clonedAttr.Value);
            // Modificadores NO se clonan:
            Assert.AreEqual(5, clonedAttr.ModifiedValue);
        }

        [Test]
        public void GetAllAttributes_EnumeratesEverything()
        {
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<TestIntAttribute>(new TestIntAttribute(1));
            attrs.SetAttribute<TestFloatAttribute>(new TestFloatAttribute(2f));

            var all = attrs.GetAllAttributes();
            Assert.AreEqual(2, all.Count);
        }

        [Test]
        public void ModifiedValue_AppliesOnlyIntrinsic()
        {
            var attr = new TestIntAttribute(10);
            var carrier = Guid.NewGuid();

            var intrinsic = new Modifier<int>(
                amount: 5, op: ModifierOperation.Add, duration: 0,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);
            var outgoing = new Modifier<int>(
                amount: 100, op: ModifierOperation.Add, duration: 0,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Outgoing,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            attr.AddModifier<int>(intrinsic);
            attr.AddModifier<int>(outgoing);

            // Intrinsic suma +5, Outgoing NO se aplica en el accessor.
            Assert.AreEqual(15, attr.ModifiedValue);
        }

        [Test]
        public void AddModifier_WrongType_ReturnsFalse()
        {
            var attr = new TestIntAttribute(10);
            var carrier = Guid.NewGuid();

            var floatMod = new Modifier<float>(
                amount: 0.5f, op: ModifierOperation.Percent, duration: 0,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            Assert.IsFalse(attr.AddModifier<float>(floatMod));
        }

        [Test]
        public void LinkAttribute_CallbackFiresOnAddAndRemove()
        {
            var attr = new TestIntAttribute(10);
            var carrier = Guid.NewGuid();
            int calls = 0;
            Guid lastId = Guid.Empty;

            attr.LinkAttribute(id =>
            {
                calls++;
                lastId = id;
            });

            var mod = new Modifier<int>(
                amount: 1, op: ModifierOperation.Add, duration: 0,
                carrierId: carrier, sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);

            attr.AddModifier<int>(mod);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(mod.ModifierId, lastId);

            attr.RemoveModifier(mod.ModifierId);
            Assert.AreEqual(2, calls);
        }
    }
}
