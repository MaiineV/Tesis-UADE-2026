using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Dice.Tests
{
    /// <summary>
    /// Tests de <see cref="RerollSelectionPrefs"/>: default, persistencia y el
    /// mapeo selección→keep en ambos modos (invertido default y clásico).
    /// </summary>
    [TestFixture]
    public class RerollSelectionPrefsTests
    {
        private const string Key = "Rollgeon.RerollKeepSelected";

        private bool _saved;

        [SetUp]
        public void SetUp()
        {
            // El setter escribe PlayerPrefs reales incluso en EditMode: backup y
            // restore para no pisar la preferencia del dev que corre los tests.
            _saved = RerollSelectionPrefs.KeepSelected;
        }

        [TearDown]
        public void TearDown()
        {
            RerollSelectionPrefs.KeepSelected = _saved;
        }

        private static void ResetCache()
        {
            typeof(RerollSelectionPrefs)
                .GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
        }

        [Test]
        public void KeepSelected_WithoutPersistedKey_DefaultsToInvertedMode()
        {
            PlayerPrefs.DeleteKey(Key);
            ResetCache();

            Assert.IsFalse(RerollSelectionPrefs.KeepSelected,
                "Sin key persistida el modo debe ser el invertido (Balatro) actual.");
        }

        [Test]
        public void KeepSelected_Set_PersistsToPlayerPrefsAndSurvivesCacheReset()
        {
            RerollSelectionPrefs.KeepSelected = true;
            ResetCache();

            Assert.AreEqual(1, PlayerPrefs.GetInt(Key, -1));
            Assert.IsTrue(RerollSelectionPrefs.KeepSelected);
        }

        [Test]
        public void SelectionToKeep_InvertedMode_ReturnsComplementOfSelection()
        {
            RerollSelectionPrefs.KeepSelected = false;
            var selected = new[] { true, false, true, false };

            var keep = RerollSelectionPrefs.SelectionToKeep(selected, 4);

            CollectionAssert.AreEqual(new[] { false, true, false, true }, keep);
        }

        [Test]
        public void SelectionToKeep_InvertedMode_NullSelection_KeepsAllDice()
        {
            RerollSelectionPrefs.KeepSelected = false;

            var keep = RerollSelectionPrefs.SelectionToKeep(null, 3);

            CollectionAssert.AreEqual(new[] { true, true, true }, keep);
        }

        [Test]
        public void SelectionToKeep_InvertedMode_ShortMask_KeepsDiceWithoutSelectionState()
        {
            RerollSelectionPrefs.KeepSelected = false;
            var selected = new[] { true };

            var keep = RerollSelectionPrefs.SelectionToKeep(selected, 3);

            CollectionAssert.AreEqual(new[] { false, true, true }, keep);
        }

        [Test]
        public void SelectionToKeep_ClassicMode_ReturnsSelectionAsKeep()
        {
            RerollSelectionPrefs.KeepSelected = true;
            var selected = new[] { true, false, true, false };

            var keep = RerollSelectionPrefs.SelectionToKeep(selected, 4);

            CollectionAssert.AreEqual(new[] { true, false, true, false }, keep);
        }

        [Test]
        public void SelectionToKeep_ClassicMode_NullSelection_RerollsAllDice()
        {
            RerollSelectionPrefs.KeepSelected = true;

            var keep = RerollSelectionPrefs.SelectionToKeep(null, 3);

            CollectionAssert.AreEqual(new[] { false, false, false }, keep);
        }

        [Test]
        public void SelectionToKeep_ClassicMode_ShortMask_RerollsDiceWithoutSelectionState()
        {
            RerollSelectionPrefs.KeepSelected = true;
            var selected = new[] { true };

            var keep = RerollSelectionPrefs.SelectionToKeep(selected, 3);

            CollectionAssert.AreEqual(new[] { true, false, false }, keep);
        }
    }
}
