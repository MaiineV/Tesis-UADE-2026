using NUnit.Framework;
using Patterns;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Cobertura de <see cref="EnchantmentWeightModifierService"/> — registro por
    /// fuente, composición multiplicativa y limpieza en OnRunStart.
    /// </summary>
    [TestFixture]
    public class EnchantmentWeightModifierServiceTests
    {
        private EnchantmentWeightModifierService _service;

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _service = new EnchantmentWeightModifierService();
            _service.Register();
        }

        [TearDown]
        public void Teardown()
        {
            _service.Dispose();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void ResolveCursedMultiplier_ReturnsOne_WhenNoSourcesRegistered()
        {
            // Act + Assert
            Assert.AreEqual(1f, _service.ResolveCursedMultiplier());
        }

        [Test]
        public void ResolveCursedMultiplier_MultipliesAcrossSources()
        {
            // Arrange
            _service.Register("item.a", 2f);
            _service.Register("item.b", 3f);

            // Act + Assert
            Assert.AreEqual(6f, _service.ResolveCursedMultiplier(), 0.0001f);
        }

        [Test]
        public void Register_ReplacesMultiplier_WhenSameSourceIdRegisteredTwice()
        {
            // Arrange
            _service.Register("item.a", 2f);
            _service.Register("item.a", 5f);

            // Act + Assert
            Assert.AreEqual(5f, _service.ResolveCursedMultiplier(), 0.0001f);
        }

        [Test]
        public void Unregister_RemovesOnlyThatSource()
        {
            // Arrange
            _service.Register("item.a", 2f);
            _service.Register("item.b", 3f);

            // Act
            _service.Unregister("item.a");

            // Assert
            Assert.AreEqual(3f, _service.ResolveCursedMultiplier(), 0.0001f);
        }

        [Test]
        public void OnRunStart_ClearsAllSources()
        {
            // Arrange
            _service.Register("item.a", 4f);

            // Act — nueva run: el registry arranca limpio (los items se re-registran
            // al rehidratar el inventario).
            EventManager.Trigger(EventName.OnRunStart);

            // Assert
            Assert.AreEqual(1f, _service.ResolveCursedMultiplier());
        }
    }
}
