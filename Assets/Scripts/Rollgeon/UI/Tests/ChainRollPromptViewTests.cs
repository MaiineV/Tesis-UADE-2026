using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    [TestFixture]
    public class ChainRollPromptViewTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private ChainRollPromptView MakePrompt(out TextMeshProUGUI label, bool withLabel = true)
        {
            var go = new GameObject("ChainRollPrompt");
            _created.Add(go);
            var view = go.AddComponent<ChainRollPromptView>();

            label = null;
            if (withLabel)
            {
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(go.transform);
                label = labelGo.AddComponent<TextMeshProUGUI>();
                SetPrivateField(view, "_label", label);
            }
            return view;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        [Test]
        public void Show_FormatsPhaseLabelAndActivates()
        {
            // Arrange
            var view = MakePrompt(out var label);
            view.gameObject.SetActive(false);

            // Act
            view.Show("Shield");

            // Assert
            Assert.AreEqual("Shield Roll (1E)", label.text);
            Assert.IsTrue(view.gameObject.activeSelf);
        }

        [Test]
        public void Show_UsesFallback_WhenPhaseLabelIsEmpty()
        {
            // Arrange
            var view = MakePrompt(out var label);

            // Act
            view.Show(null);

            // Assert — el fallback default es "Phase".
            Assert.AreEqual("Phase Roll (1E)", label.text);
            Assert.IsTrue(view.gameObject.activeSelf);
        }

        [Test]
        public void Show_WithoutLabelRef_DoesNotThrowAndActivates()
        {
            // Arrange
            var view = MakePrompt(out _, withLabel: false);
            view.gameObject.SetActive(false);

            // Act / Assert
            Assert.DoesNotThrow(() => view.Show("Shield"));
            Assert.IsTrue(view.gameObject.activeSelf);
        }

        [Test]
        public void Hide_DeactivatesGameObject()
        {
            // Arrange
            var view = MakePrompt(out _);
            view.Show("Shield");

            // Act
            view.Hide();

            // Assert
            Assert.IsFalse(view.gameObject.activeSelf);
        }
    }
}
