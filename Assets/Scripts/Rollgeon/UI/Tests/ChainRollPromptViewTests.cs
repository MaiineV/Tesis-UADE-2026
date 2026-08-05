using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    [TestFixture]
    public class ChainRollPromptViewTests
    {
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
            EventManager.ResetEventDictionary();
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

        // PUL-016: el prompt no puede sobrevivir al chain que lo mostró. Estos cubren los
        // caminos de salida que no pasan por un Hide explícito.

        [Test]
        public void Show_ThenOnChainCompleted_HidesPrompt()
        {
            // Arrange
            var view = MakePrompt(out _);
            view.Show("Shield");

            // Act
            EventManager.Trigger(EventName.OnChainCompleted, System.Guid.NewGuid(), 1, 2, false);

            // Assert
            Assert.IsFalse(view.gameObject.activeSelf);
        }

        [Test]
        public void Show_ThenOnCombatEnd_HidesPrompt()
        {
            // Arrange — el repro original: el combate cierra con la entrada paga pendiente.
            var view = MakePrompt(out _);
            view.Show("Shield");

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.IsFalse(view.gameObject.activeSelf);
        }

        [Test]
        public void ShowTwice_ThenOnCombatEnd_HidesPrompt()
        {
            // Arrange — dos Show seguidos no deben apilar suscripciones: si se apilaran, un
            // solo Hide dejaría una viva y el handler correría sobre un prompt ya apagado.
            var view = MakePrompt(out _);
            view.Show("Shield");
            view.Show("Shield");

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.IsFalse(view.gameObject.activeSelf);
        }

        [Test]
        public void Hide_ThenOnCombatEnd_StaysHiddenAndDoesNotThrow()
        {
            // Arrange
            var view = MakePrompt(out _);
            view.Show("Shield");
            view.Hide();

            // Act / Assert — ya desuscripto: el evento no debe tocar nada.
            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnCombatEnd));
            Assert.IsFalse(view.gameObject.activeSelf);
        }

        // BUG-034: el prompt es la affordance visible del pago — su click debe
        // rutearse al mismo entry point que el botón Roll, y solo mientras está arriba.

        private UnityEngine.UI.Button AttachButton(ChainRollPromptView view)
        {
            var button = view.gameObject.AddComponent<UnityEngine.UI.Button>();
            SetPrivateField(view, "_button", button);
            return button;
        }

        [Test]
        public void ButtonClick_WhileShown_InvokesOnPromptClickedOnce()
        {
            // Arrange
            var view = MakePrompt(out _);
            var button = AttachButton(view);
            int clicks = 0;
            view.OnPromptClicked.AddListener(() => clicks++);
            view.Show("Shield");

            // Act
            button.onClick.Invoke();

            // Assert
            Assert.AreEqual(1, clicks);
        }

        [Test]
        public void ButtonClick_AfterHide_DoesNotInvokeOnPromptClicked()
        {
            // Arrange
            var view = MakePrompt(out _);
            var button = AttachButton(view);
            int clicks = 0;
            view.OnPromptClicked.AddListener(() => clicks++);
            view.Show("Shield");
            view.Hide();

            // Act — el listener del botón se soltó junto con las suscripciones del bus.
            button.onClick.Invoke();

            // Assert
            Assert.AreEqual(0, clicks);
        }

        [Test]
        public void ShowTwice_ButtonClick_InvokesOnPromptClickedOnce()
        {
            // Arrange — dos Show seguidos no deben apilar listeners del botón.
            var view = MakePrompt(out _);
            var button = AttachButton(view);
            int clicks = 0;
            view.OnPromptClicked.AddListener(() => clicks++);
            view.Show("Shield");
            view.Show("Shield");

            // Act
            button.onClick.Invoke();

            // Assert
            Assert.AreEqual(1, clicks);
        }

        [Test]
        public void Show_WithoutButtonRef_DoesNotThrow()
        {
            // Arrange — prefab sin botón wireado (rollback / setup viejo).
            var view = MakePrompt(out _);

            // Act / Assert
            Assert.DoesNotThrow(() => view.Show("Shield"));
            Assert.DoesNotThrow(() => view.Hide());
        }
    }
}
