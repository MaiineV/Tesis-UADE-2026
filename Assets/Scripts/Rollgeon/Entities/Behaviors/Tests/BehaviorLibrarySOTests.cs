using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors.Tests
{
    /// <summary>
    /// Tests de <see cref="BehaviorLibrarySO"/>: lookup por key, round-trip de clone polimorfico,
    /// clones independientes.
    /// </summary>
    [TestFixture]
    public class BehaviorLibrarySOTests
    {
        private BehaviorLibrarySO _lib;

        [SetUp]
        public void SetUp()
        {
            _lib = ScriptableObject.CreateInstance<BehaviorLibrarySO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_lib);
        }

        [Test]
        public void GetClone_UnknownId_ReturnsNull()
        {
            Assert.IsNull(_lib.GetClone("does.not.exist"));
        }

        [Test]
        public void GetClone_EmptyId_ReturnsNull()
        {
            Assert.IsNull(_lib.GetClone(null));
            Assert.IsNull(_lib.GetClone(""));
        }

        [Test]
        public void GetClone_RegisteredTemplate_ReturnsClone()
        {
            var template = new SupportHealBehavior { BaseHealAmount = 7 };
            _lib.SetTemplate("support.heal", template);

            var clone = _lib.GetClone("support.heal") as SupportHealBehavior;
            Assert.IsNotNull(clone, "Clone debe ser de tipo concreto SupportHealBehavior.");
            Assert.AreEqual(7, clone.BaseHealAmount);
            Assert.AreNotSame(template, clone, "El clone debe ser una instancia distinta del template.");
        }

        [Test]
        public void GetClone_TwoCalls_ReturnIndependentInstances()
        {
            var template = new SupportHealBehavior { BaseHealAmount = 3 };
            _lib.SetTemplate("support.heal", template);

            var a = _lib.GetClone("support.heal") as SupportHealBehavior;
            var b = _lib.GetClone("support.heal") as SupportHealBehavior;

            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreNotSame(a, b);

            // Mutar uno no debe impactar al otro.
            a.BaseHealAmount = 99;
            Assert.AreEqual(3, b.BaseHealAmount);
        }

        [Test]
        public void Contains_True_WhenRegistered()
        {
            _lib.SetTemplate("k", new SupportHealBehavior());
            Assert.IsTrue(_lib.Contains("k"));
        }

        [Test]
        public void Contains_False_WhenAbsent()
        {
            Assert.IsFalse(_lib.Contains("missing"));
            Assert.IsFalse(_lib.Contains(null));
            Assert.IsFalse(_lib.Contains(""));
        }

        [Test]
        public void AllTemplateIds_ListsKeys()
        {
            _lib.SetTemplate("a", new SupportHealBehavior());
            _lib.SetTemplate("b", new SupportHealBehavior());

            var ids = new System.Collections.Generic.List<string>(_lib.AllTemplateIds);
            Assert.Contains("a", ids);
            Assert.Contains("b", ids);
            Assert.AreEqual(2, ids.Count);
        }
    }
}
