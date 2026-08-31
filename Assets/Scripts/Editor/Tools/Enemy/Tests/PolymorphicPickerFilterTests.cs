using NUnit.Framework;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// El picker del árbol enemigo (<see cref="PolymorphicBlockDrawer.Options.Enemy"/>) esconde
    /// los tipos que en ese contexto no hacen nada (leen el roll/combo del jugador). El Item /
    /// Enchantment Editor (<c>Options.Default</c>) sigue listando todo.
    /// </summary>
    [TestFixture]
    public class PolymorphicPickerFilterTests
    {
        [Test]
        public void EnemyFilter_HidesPlayerRollEffects_KeepsTheRest()
        {
            // Arrange
            var filter = PolymorphicBlockDrawer.Options.Enemy.TypeFilter;
            Assert.IsNotNull(filter);

            // Act
            var effects = PolymorphicPicker.ConcreteSubtypesOf(typeof(IEffect), filter);

            // Assert — los 5 Scratch + EffClassSkillPush afuera; lo genérico sigue.
            CollectionAssert.DoesNotContain(effects, typeof(EffAddComboBonus));
            CollectionAssert.DoesNotContain(effects, typeof(EffMultiplyComboDamage));
            CollectionAssert.DoesNotContain(effects, typeof(EffBlockComboDamage));
            CollectionAssert.DoesNotContain(effects, typeof(EffClassSkillPush));
            CollectionAssert.Contains(effects, typeof(EffDealDamage));
            CollectionAssert.Contains(effects, typeof(EffGridPush));
        }

        [Test]
        public void EnemyFilter_HidesPlayerRollPcs_KeepsGoldAndRange()
        {
            // Arrange
            var filter = PolymorphicBlockDrawer.Options.Enemy.TypeFilter;

            // Act
            var pcs = PolymorphicPicker.ConcreteSubtypesOf(typeof(BasePreCondition), filter);

            // Assert — PcGoldCompare se exime (el oro sale de IEconomyService, no del roll).
            CollectionAssert.DoesNotContain(pcs, typeof(PcNoComboThisRoll));
            CollectionAssert.Contains(pcs, typeof(PcGoldCompare));
            CollectionAssert.Contains(pcs, typeof(PcTargetInRange));
            CollectionAssert.Contains(pcs, typeof(PcOwnerStatCompare));
        }

        [Test]
        public void DefaultOptions_HaveNoFilter_ItemEditorListsEverything()
        {
            // Arrange + Act + Assert — Options.Default no filtra: el Item Editor sí tiene roll.
            Assert.IsNull(PolymorphicBlockDrawer.Options.Default.TypeFilter);
            CollectionAssert.Contains(
                PolymorphicPicker.ConcreteSubtypesOf(typeof(IEffect)), typeof(EffAddComboBonus));
        }
    }
}
