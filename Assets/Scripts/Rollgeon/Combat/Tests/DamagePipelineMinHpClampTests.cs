using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests del seam <see cref="IMinHpClampProvider"/> en <see cref="DamagePipeline"/>
    /// (Feature#0046 — clamp del Mimic). Sin provider registrado el pipeline debe
    /// comportarse exactamente igual que antes.
    /// </summary>
    [TestFixture]
    public class DamagePipelineMinHpClampTests
    {
        private AttributesManager _attrs;
        private DamagePipeline _pipeline;
        private Guid _target;
        private Guid _playerSource;
        private Guid _enemySource;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs);

            _target = Guid.NewGuid();
            _playerSource = Guid.NewGuid();
            _enemySource = Guid.NewGuid();

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(10));
            _attrs.Register(_target, attrs);

            _pipeline = new DamagePipeline(_attrs);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private DamageContext Hit(Guid source, int damage) =>
            _pipeline.Resolve(new DamageContext
            {
                SourceId = source,
                TargetId = _target,
                BaseDamage = damage
            });

        private void RegisterClamp(Guid exemptSource, int minHp = 1)
        {
            ServiceLocator.AddService<IMinHpClampProvider>(
                new StubClampProvider(_target, exemptSource, minHp));
        }

        [Test]
        public void Resolve_ShouldClampHpToFloor_WhenNonExemptSourceWouldKill()
        {
            // Arrange — clamp activo para todo source salvo el player.
            RegisterClamp(exemptSource: _playerSource);

            // Act — daño enemigo que mataría (25 > 10 HP).
            var ctx = Hit(_enemySource, 25);

            // Assert — HP queda exactamente en 1, el daño real se recalcula y no es letal.
            Assert.AreEqual(1, _attrs.GetAttribute<Health>(_target).Value);
            Assert.AreEqual(9, ctx.FinalDamage);
            Assert.IsFalse(ctx.WasLethal);
        }

        [Test]
        public void Resolve_ShouldIgnoreClamp_WhenSourceIsExempt()
        {
            // Arrange
            RegisterClamp(exemptSource: _playerSource);

            // Act — el player pega el mismo golpe letal.
            var ctx = Hit(_playerSource, 25);

            // Assert — sin clamp: muerte normal.
            Assert.AreEqual(0, _attrs.GetAttribute<Health>(_target).Value);
            Assert.AreEqual(25, ctx.FinalDamage);
            Assert.IsTrue(ctx.WasLethal);
        }

        [Test]
        public void Resolve_ShouldNotHealTarget_WhenAlreadyBelowFloor()
        {
            // Arrange — piso 5, target ya con 3 HP.
            _attrs.SetAttributeValue<Health, int>(_target, 3);
            RegisterClamp(exemptSource: _playerSource, minHp: 5);

            // Act
            var ctx = Hit(_enemySource, 25);

            // Assert — el clamp nunca sube la vida: queda en 3 y el golpe hace 0.
            Assert.AreEqual(3, _attrs.GetAttribute<Health>(_target).Value);
            Assert.AreEqual(0, ctx.FinalDamage);
            Assert.IsFalse(ctx.WasLethal);
        }

        [Test]
        public void Resolve_ShouldNotClamp_WhenDamageLeavesHpAboveFloor()
        {
            // Arrange
            RegisterClamp(exemptSource: _playerSource);

            // Act — daño no letal normal.
            var ctx = Hit(_enemySource, 4);

            // Assert — el clamp no interviene.
            Assert.AreEqual(6, _attrs.GetAttribute<Health>(_target).Value);
            Assert.AreEqual(4, ctx.FinalDamage);
        }

        [Test]
        public void Resolve_ShouldBehaveExactlyAsBefore_WhenNoProviderRegistered()
        {
            // Arrange — sin provider en el locator.
            // Act
            var ctx = Hit(_enemySource, 25);

            // Assert — comportamiento legacy intacto.
            Assert.AreEqual(0, _attrs.GetAttribute<Health>(_target).Value);
            Assert.AreEqual(25, ctx.FinalDamage);
            Assert.IsTrue(ctx.WasLethal);
        }

        private sealed class StubClampProvider : IMinHpClampProvider
        {
            private readonly Guid _protectedTarget;
            private readonly Guid _exemptSource;
            private readonly int _minHp;

            public StubClampProvider(Guid protectedTarget, Guid exemptSource, int minHp)
            {
                _protectedTarget = protectedTarget;
                _exemptSource = exemptSource;
                _minHp = minHp;
            }

            public bool TryGetMinHp(Guid targetId, Guid sourceId, out int minHp)
            {
                minHp = _minHp;
                return targetId == _protectedTarget && sourceId != _exemptSource;
            }
        }
    }
}
