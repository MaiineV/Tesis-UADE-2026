using System.Collections.Generic;
using NUnit.Framework;

namespace Rollgeon.Upgrades.Dice.Tests
{
    [TestFixture]
    public class ComboFilterTests
    {
        [Test]
        public void None_MatchesAnyNonEmptyCombo()
        {
            var filter = new ComboFilter { Mode = ComboFilterMode.None };

            Assert.IsTrue(filter.Matches("combo.ladder"));
        }

        // --- Número Alto no es combo (Fix#0053) ------------------------------------------
        // combo.higher_number matchea cualquier selección no vacía y contribuye un solo dado:
        // como condición "cuando hay combo" no dice nada. Solo entra explícito.

        [TestCase(ComboFilterMode.None)]
        [TestCase(ComboFilterMode.AnyCombo)]
        public void AnyCombo_DoesNotMatchHigherNumber(ComboFilterMode mode)
        {
            var filter = new ComboFilter { Mode = mode };

            Assert.IsFalse(filter.Matches(Rollgeon.Combos.ComboId.HigherNumber));
            Assert.IsTrue(filter.Matches("combo.trio"));
        }

        [Test]
        public void AnyIncludingHigherNumber_MatchesHigherNumberAndRealCombos()
        {
            var filter = new ComboFilter { Mode = ComboFilterMode.AnyIncludingHigherNumber };

            Assert.IsTrue(filter.Matches(Rollgeon.Combos.ComboId.HigherNumber));
            Assert.IsTrue(filter.Matches("combo.trio"));
            Assert.IsFalse(filter.Matches(""));
        }

        [Test]
        public void ComboIds_ListingHigherNumberExplicitly_StillMatchesIt()
        {
            // Arcas / Coronas "del Número Alto": el texto lo nombra, el filtro lo lista.
            var filter = new ComboFilter
            {
                Mode = ComboFilterMode.ComboIds,
                ComboIds = new List<string> { Rollgeon.Combos.ComboId.HigherNumber },
            };

            Assert.IsTrue(filter.Matches(Rollgeon.Combos.ComboId.HigherNumber));
        }

        [Test]
        public void ExcludeComboIds_AlsoDropsHigherNumber_EvenIfNotListed()
        {
            var filter = new ComboFilter
            {
                Mode = ComboFilterMode.ExcludeComboIds,
                ComboIds = new List<string> { "combo.poker" },
            };

            Assert.IsFalse(filter.Matches(Rollgeon.Combos.ComboId.HigherNumber));
            Assert.IsTrue(filter.Matches("combo.trio"));
        }

        [Test]
        public void AnyIncludingHigherNumber_IsAppendOnly()
        {
            // Los assets serializan el int del enum.
            Assert.AreEqual(3, (int)ComboFilterMode.ExcludeComboIds);
            Assert.AreEqual(4, (int)ComboFilterMode.AnyIncludingHigherNumber);
        }

        [Test]
        public void None_DoesNotMatchEmptyCombo()
        {
            // None equivale a AnyCombo cuando el trigger ya está atado al hook de combo,
            // pero un comboId vacío significa "no hubo combo" y nunca debe disparar.
            var filter = new ComboFilter { Mode = ComboFilterMode.None };

            Assert.IsFalse(filter.Matches(""));
            Assert.IsFalse(filter.Matches(null));
        }

        [Test]
        public void AnyCombo_MatchesAnyNonEmptyCombo()
        {
            var filter = new ComboFilter { Mode = ComboFilterMode.AnyCombo };

            Assert.IsTrue(filter.Matches("combo.trio"));
        }

        [Test]
        public void AnyCombo_DoesNotMatchEmptyCombo()
        {
            var filter = new ComboFilter { Mode = ComboFilterMode.AnyCombo };

            Assert.IsFalse(filter.Matches(null));
        }

        [Test]
        public void ComboIds_MatchesListedId()
        {
            var filter = new ComboFilter
            {
                Mode = ComboFilterMode.ComboIds,
                ComboIds = new List<string> { "combo.trio", "combo.poker" },
            };

            Assert.IsTrue(filter.Matches("combo.poker"));
        }

        [Test]
        public void ComboIds_DoesNotMatchUnlistedId()
        {
            var filter = new ComboFilter
            {
                Mode = ComboFilterMode.ComboIds,
                ComboIds = new List<string> { "combo.trio" },
            };

            Assert.IsFalse(filter.Matches("combo.ladder"));
        }

        [Test]
        public void ComboIds_DoesNotMatchEmptyCombo()
        {
            var filter = new ComboFilter
            {
                Mode = ComboFilterMode.ComboIds,
                ComboIds = new List<string> { "combo.trio" },
            };

            Assert.IsFalse(filter.Matches(""));
        }

        [Test]
        public void ComboIds_EmptyList_NeverMatches()
        {
            var filter = new ComboFilter
            {
                Mode = ComboFilterMode.ComboIds,
                ComboIds = new List<string>(),
            };

            Assert.IsFalse(filter.Matches("combo.trio"));
        }

        // --- ExcludeComboIds (Fuente Mágica: todo combo salvo Número Mayor) --------------

        [Test]
        public void test_combo_filter_exclude_mode_matches_unlisted_combo()
        {
            // Arrange
            var filter = new ComboFilter
            {
                Mode = ComboFilterMode.ExcludeComboIds,
                ComboIds = new List<string> { "combo.higher_number" },
            };

            // Act
            bool matches = filter.Matches("combo.trio");

            // Assert
            Assert.IsTrue(matches);
        }

        [Test]
        public void test_combo_filter_exclude_mode_rejects_listed_combo()
        {
            // Arrange
            var filter = new ComboFilter
            {
                Mode = ComboFilterMode.ExcludeComboIds,
                ComboIds = new List<string> { "combo.higher_number" },
            };

            // Act
            bool matches = filter.Matches("combo.higher_number");

            // Assert
            Assert.IsFalse(matches);
        }

        [Test]
        public void test_combo_filter_exclude_mode_rejects_empty_combo()
        {
            // Arrange — sin combo no hay nada que excluir ni que disparar.
            var filter = new ComboFilter
            {
                Mode = ComboFilterMode.ExcludeComboIds,
                ComboIds = new List<string> { "combo.higher_number" },
            };

            // Act + Assert
            Assert.IsFalse(filter.Matches(""));
            Assert.IsFalse(filter.Matches(null));
        }

        [Test]
        public void test_combo_filter_exclude_mode_with_empty_list_matches_any_combo()
        {
            // Arrange — lista vacía = no se excluye nada, equivale a AnyCombo.
            var filter = new ComboFilter
            {
                Mode = ComboFilterMode.ExcludeComboIds,
                ComboIds = new List<string>(),
            };

            // Act + Assert
            Assert.IsTrue(filter.Matches("combo.trio"));
            Assert.IsTrue(filter.UsesComboIds);
        }
    }
}
