using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Vampiro (Fix#0053): el costo de vida se cobra al jugador aunque el contexto traiga
    /// al enemigo como TargetGuid (hook ComboPlayed). Sin <c>TargetSelf</c>, el effect
    /// resolvía el target y pegaba al rival.
    /// </summary>
    [TestFixture]
    public class EffModifyIntAttributeTargetSelfTests
    {
        private AttributesManager _attrs;
        private Guid _player;
        private Guid _enemy;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _attrs = new AttributesManager();
            _player = Guid.NewGuid();
            _enemy = Guid.NewGuid();
            Register(_player, 20);
            Register(_enemy, 30);
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            _attrs?.Dispose();
        }

        private void Register(Guid id, int hp)
        {
            var a = new ModifiableAttributes();
            a.SetAttribute<Health>(new Health(hp));
            _attrs.Register(id, a);
        }

        private static EffModifyIntAttribute HealthCost(int amount, bool targetSelf)
        {
            var eff = new EffModifyIntAttribute
            {
                TargetStat = StatType.Health,
                Operation = IntOperation.Subtract,
                TargetSelf = targetSelf,
            };
            eff.EditorSetConstantAmount(amount);
            return eff;
        }

        [Test]
        public void TargetSelf_ChargesTheSource_NotTheContextTarget()
        {
            var ctx = new EffectContext { SourceGuid = _player, TargetGuid = _enemy };

            Assert.IsTrue(HealthCost(5, targetSelf: true).ApplyEffect(ctx));

            Assert.AreEqual(15, _attrs.GetAttributeValue<Health, int>(_player));
            Assert.AreEqual(30, _attrs.GetAttributeValue<Health, int>(_enemy));
        }

        [Test]
        public void WithoutTargetSelf_HitsTheContextTarget()
        {
            // El comportamiento previo (y el bug del Vampiro) — queda documentado por contraste.
            var ctx = new EffectContext { SourceGuid = _player, TargetGuid = _enemy };

            HealthCost(5, targetSelf: false).ApplyEffect(ctx);

            Assert.AreEqual(20, _attrs.GetAttributeValue<Health, int>(_player));
            Assert.AreEqual(25, _attrs.GetAttributeValue<Health, int>(_enemy));
        }
    }
}
