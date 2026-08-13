using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Effects;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Gate de energía de <see cref="HeroActionBehavior.HasUsableEffectGroup"/> y su
    /// bypass para la UI.
    /// </summary>
    /// <remarks>
    /// Regresión: la HUD de combate consultaba HasUsableEffectGroup ANTES de su propio
    /// chequeo de energía, y como el gate vive adentro, todo chip impagable salía Locked
    /// — el estado Unaffordable (outline rojo + shake de rechazo) era inalcanzable.
    /// </remarks>
    [TestFixture]
    public class HeroActionBehaviorEnergyGateTests
    {
        private FakeEnergyService _energy;
        private Guid _owner;

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            _owner = Guid.NewGuid();
            _energy = new FakeEnergyService();
            ServiceLocator.AddService<IEnergyService>(_energy);
        }

        [TearDown]
        public void Teardown() => ServiceLocator.Clear();

        // El gate sale temprano con Effects vacío, así que un behavior realista necesita
        // al menos un grupo para que la energía llegue a evaluarse.
        private static HeroActionBehavior BehaviorCosting(int cost) => new HeroActionBehavior
        {
            ActionName = "test",
            EnergyCost = cost,
            Effects = new List<EffectData> { new EffectData() },
        };

        [Test]
        public void should_block_when_energy_is_short_and_gate_is_included()
        {
            // Arrange
            _energy.Current[_owner] = 1;
            var behavior = BehaviorCosting(2);

            // Act
            bool usable = behavior.HasUsableEffectGroup(_owner, Guid.Empty, out var reason);

            // Assert
            Assert.IsFalse(usable, "Con el gate puesto, la falta de energía bloquea.");
            StringAssert.Contains("energy", reason.ToLowerInvariant());
        }

        [Test]
        public void should_pass_when_energy_is_short_but_gate_is_skipped()
        {
            // Arrange
            _energy.Current[_owner] = 1;
            var behavior = BehaviorCosting(2);

            // Act
            bool usable = behavior.HasUsableEffectGroup(_owner, Guid.Empty, out var reason,
                                                        includeEnergyGate: false);

            // Assert — sin esto la HUD no puede distinguir Locked de Unaffordable.
            Assert.IsTrue(usable, "Sin el gate, la falta de energía la decide el llamador.");
            Assert.IsNull(reason);
        }

        [Test]
        public void should_pass_when_energy_is_enough_regardless_of_the_gate()
        {
            // Arrange
            _energy.Current[_owner] = 5;
            var behavior = BehaviorCosting(2);

            // Act
            bool withGate = behavior.HasUsableEffectGroup(_owner, Guid.Empty, out _);
            bool without = behavior.HasUsableEffectGroup(_owner, Guid.Empty, out _,
                                                        includeEnergyGate: false);

            // Assert
            Assert.IsTrue(withGate);
            Assert.IsTrue(without);
        }

        [Test]
        public void should_keep_blocking_execution_paths_by_default()
        {
            // Arrange — el default es el que usan TurnManager y CombatHandoffService.
            _energy.Current[_owner] = 0;
            var behavior = BehaviorCosting(1);

            // Act
            bool usable = behavior.HasUsableEffectGroup(_owner, Guid.Empty, out _);

            // Assert
            Assert.IsFalse(usable, "El backstop de ejecución no debe aflojarse.");
        }

        private sealed class FakeEnergyService : IEnergyService
        {
            public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();

            public void InitializeForEntity(Guid entityId) => Current[entityId] = 4;
            public bool SpendEnergy(Guid entityId, int cost) => false;
            public void RegenerateAtTurnEnd(Guid entityId) { }
            public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;
            public int GetMax(Guid entityId) => 4;
        }
    }
}
