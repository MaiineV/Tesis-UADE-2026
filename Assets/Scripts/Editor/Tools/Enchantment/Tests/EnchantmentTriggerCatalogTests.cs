using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Triggers;

namespace Rollgeon.Editor.Tools.Enchantment.Tests
{
    public class EnchantmentTriggerCatalogTests
    {
        [Test]
        public void All_IsNotEmpty()
        {
            Assert.IsNotEmpty(EnchantmentTriggerCatalog.All);
        }

        [Test]
        public void All_IdsAreUnique()
        {
            // Arrange
            var ids = EnchantmentTriggerCatalog.All.Select(o => o.Id).ToList();

            // Assert
            CollectionAssert.AllItemsAreUnique(ids);
        }

        [Test]
        public void All_DisplayNamesAreUnique()
        {
            // Arrange
            var names = EnchantmentTriggerCatalog.All.Select(o => o.DisplayName).ToList();

            // Assert
            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void All_HaveDisplayNameAndHelp()
        {
            foreach (var option in EnchantmentTriggerCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(option.DisplayName), $"'{option.Id}' sin DisplayName");
                Assert.IsFalse(string.IsNullOrWhiteSpace(option.Help), $"'{option.Id}' sin Help");
            }
        }

        [TestCase("combo.matched.all", EnchantmentHookEvent.ComboMatched)]
        [TestCase("combo.played.all", EnchantmentHookEvent.ComboPlayed)]
        public void IncludingHigherNumberOptions_RoundTrip_ThroughApplyAndMatch(string id, EnchantmentHookEvent evt)
        {
            // Fix#0053: las mutaciones de cara valen en toda jugada, Número Alto incluido.
            var option = EnchantmentTriggerCatalog.All.First(o => o.Id == id);
            var trigger = new ExecuteEffectsOnDiceEvent();

            EnchantmentTriggerCatalog.Apply(trigger, option);

            Assert.AreEqual(evt, trigger.Event);
            Assert.AreEqual(ComboFilterMode.AnyIncludingHigherNumber, trigger.Filter.Mode);
            Assert.AreEqual(id, EnchantmentTriggerCatalog.Match(trigger)?.Id);
        }

        [Test]
        public void AnyComboOption_DoesNotMatchATriggerThatIncludesHigherNumber()
        {
            var any = EnchantmentTriggerCatalog.All.First(o => o.Id == "combo.matched.any");
            var trigger = new ExecuteEffectsOnDiceEvent();
            EnchantmentTriggerCatalog.Apply(trigger, any);

            Assert.AreEqual(ComboFilterMode.AnyCombo, trigger.Filter.Mode);
            Assert.AreEqual("combo.matched.any", EnchantmentTriggerCatalog.Match(trigger)?.Id);
        }

        [Test]
        public void All_ComboMatchedOptions_AreScratchOnly()
        {
            // BUG-017: preview re-dispara por toggle de hold — el catálogo tiene que llevar
            // la trampa en el dato para que la UI/skill la muestren.
            foreach (var option in EnchantmentTriggerCatalog.All)
            {
                if (option.Event == EnchantmentHookEvent.ComboMatched)
                    Assert.IsTrue(option.ScratchOnly, $"'{option.Id}' es ComboMatched y no está marcada ScratchOnly");
                else
                    Assert.IsFalse(option.ScratchOnly, $"'{option.Id}' no es ComboMatched y está marcada ScratchOnly");
            }
        }

        [Test]
        public void Apply_ThenMatch_ReturnsTheSameOption()
        {
            foreach (var option in EnchantmentTriggerCatalog.All)
            {
                // Arrange
                var trigger = new ExecuteEffectsOnDiceEvent();

                // Act
                EnchantmentTriggerCatalog.Apply(trigger, option);
                var matched = EnchantmentTriggerCatalog.Match(trigger);

                // Assert
                Assert.IsNotNull(matched, $"'{option.Id}' no se re-matchea tras Apply");
                Assert.AreEqual(option.Id, matched.Value.Id);
            }
        }

        [Test]
        public void Match_NullTrigger_IsNull()
        {
            Assert.IsNull(EnchantmentTriggerCatalog.Match(null));
        }

        [Test]
        public void Match_ComboHookWithModeNone_FallsBackToTheAnyOption()
        {
            // Arrange — Mode None equivale a AnyCombo en runtime (ComboFilter.Matches);
            // el catálogo lo mapea a la opción "any" en vez de declararlo fuera de catálogo.
            var trigger = new ExecuteEffectsOnDiceEvent { Event = EnchantmentHookEvent.ComboPlayed };
            trigger.Filter.Mode = ComboFilterMode.None;

            // Act
            var matched = EnchantmentTriggerCatalog.Match(trigger);

            // Assert
            Assert.IsNotNull(matched);
            Assert.AreEqual("combo.played.any", matched.Value.Id);
        }

        [Test]
        public void Describe_ComboIdsOption_ListsTheChosenCombos()
        {
            // Arrange
            var trigger = new ExecuteEffectsOnDiceEvent();
            var option = EnchantmentTriggerCatalog.All.First(o => o.Id == "combo.played.ids");
            EnchantmentTriggerCatalog.Apply(trigger, option);
            trigger.Filter.ComboIds = new List<string> { "combo.trio", "combo.generala" };

            // Act
            var text = EnchantmentTriggerCatalog.Describe(trigger);

            // Assert
            StringAssert.Contains("combo.trio", text);
            StringAssert.Contains("combo.generala", text);
        }

        [Test]
        public void Describe_RequireCarrierParticipates_MentionsTheCarrierGate()
        {
            // Arrange
            var trigger = new ExecuteEffectsOnDiceEvent { RequireCarrierParticipates = true };
            EnchantmentTriggerCatalog.Apply(
                trigger, EnchantmentTriggerCatalog.All.First(o => o.Id == "combo.played.any"));

            // Act
            var text = EnchantmentTriggerCatalog.Describe(trigger);

            // Assert
            StringAssert.Contains("participa", text);
        }
    }
}
