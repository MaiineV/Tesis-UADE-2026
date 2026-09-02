using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.PreConditions.Tests
{
    /// <summary>
    /// La precondición de Última Carta: compara los rolls disponibles del owner con Value.
    /// Sin servicio / sin owner / fuera de combate veta.
    /// </summary>
    [TestFixture]
    public class PcRollPoolCompareTests
    {
        private FakeRollPool _pool;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _player = Guid.NewGuid();
            _pool = new FakeRollPool { InCombat = true };
            ServiceLocator.AddService<IRollPoolService>(_pool, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private PreConditionContext Ctx() => new PreConditionContext { OwnerGuid = _player };

        private static PcRollPoolCompare UltimaCarta()
            => new PcRollPoolCompare { Comparison = IntComparison.Equal, Value = 0 };

        [Test]
        public void PoolEmpty_EqualZero_Passes()
        {
            _pool.Current = 0;
            Assert.IsTrue(UltimaCarta().Evaluate(Ctx()));
        }

        [Test]
        public void PoolHasRolls_EqualZero_Fails()
        {
            _pool.Current = 1;
            Assert.IsFalse(UltimaCarta().Evaluate(Ctx()));
        }

        [Test]
        public void GreaterOrEqual_ComparesCurrent()
        {
            _pool.Current = 3;
            var pc = new PcRollPoolCompare { Comparison = IntComparison.GreaterOrEqual, Value = 3 };
            Assert.IsTrue(pc.Evaluate(Ctx()));
            pc.Value = 4;
            Assert.IsFalse(pc.Evaluate(Ctx()));
        }

        [Test]
        public void OutOfCombat_Fails()
        {
            // El pool devuelve 0 fuera de combate: sin este gate Última Carta pagaría
            // +30 en cada Force Door / Heal de exploración.
            _pool.Current = 0;
            _pool.InCombat = false;
            Assert.IsFalse(UltimaCarta().Evaluate(Ctx()));
        }

        [Test]
        public void WithoutService_Fails()
        {
            ServiceLocator.Clear();
            Assert.IsFalse(UltimaCarta().Evaluate(Ctx()));
        }

        [Test]
        public void WithoutOwner_Fails()
        {
            _pool.Current = 0;
            Assert.IsFalse(UltimaCarta().Evaluate(new PreConditionContext()));
            Assert.IsFalse(UltimaCarta().Evaluate(null));
        }

        private sealed class FakeRollPool : IRollPoolService
        {
            public int Current;
            public bool InCombat;

            public void InitializeForEntity(Guid entityId) { }
            public bool TrySpendRolls(Guid entityId, int count) => false;
            public int Drain(Guid entityId, int amount) => 0;
            public void AddRolls(Guid entityId, int amount) { }
            public bool IsCombatActive => InCombat;
            public int GetCurrent(Guid entityId) => Current;
            public int GetMax(Guid entityId) => 15;
            public int GetRollsPerTurn(Guid entityId) => 5;
            public void AddRollPoolBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value) { }
        }
    }
}
