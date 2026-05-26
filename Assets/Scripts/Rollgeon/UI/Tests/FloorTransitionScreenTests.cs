using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="FloorTransitionScreen"/> (UI#0013b).
    /// <list type="bullet">
    /// <item><description><see cref="ScreenStringId_Returns_FloorTransitionScreen"/> — literal match.</description></item>
    /// <item><description><see cref="OnPushed_WithPayload_PopulatesFloorNumber"/> — label shows "Piso 3".</description></item>
    /// <item><description><see cref="OnPushed_WithPayload_PopulatesFloorTitle"/> — title label text matches.</description></item>
    /// <item><description><see cref="OnPushed_WithNullPayload_DoesNotCrash"/> — graceful degradation.</description></item>
    /// <item><description><see cref="OnPushed_WithEmptyTitle_HidesTitleLabel"/> — title label deactivated.</description></item>
    /// <item><description><see cref="OnPopped_CleansUpState"/> — _currentFloorNumber reset to 0.</description></item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class FloorTransitionScreenTests
    {
        private GameObject _screenGO;
        private FloorTransitionScreen _screen;
        private Button _continueButton;

        private GameObject _floorNumberGO;
        private GameObject _floorTitleGO;

        [SetUp]
        public void SetUp()
        {
            _screenGO = new GameObject("FloorTransitionScreen");
            _screenGO.SetActive(false);
            _screen = _screenGO.AddComponent<FloorTransitionScreen>();

            // Continue button
            _continueButton = AttachButton("ContinueButton");

            // Floor number label
            _floorNumberGO = new GameObject("FloorNumberLabel");
            _floorNumberGO.transform.SetParent(_screenGO.transform, false);
            var floorNumberLabel = _floorNumberGO.AddComponent<TextMeshProUGUI>();

            // Floor title label
            _floorTitleGO = new GameObject("FloorTitleLabel");
            _floorTitleGO.transform.SetParent(_screenGO.transform, false);
            var floorTitleLabel = _floorTitleGO.AddComponent<TextMeshProUGUI>();

            // Wire serialized fields via reflection
            AssignPrivate(_screen, "_floorNumberLabel", floorNumberLabel);
            AssignPrivate(_screen, "_floorTitleLabel", floorTitleLabel);
            AssignPrivate(_screen, "_continueButton", _continueButton);
        }

        [TearDown]
        public void TearDown()
        {
            if (_screenGO != null) Object.DestroyImmediate(_screenGO);
        }

        [Test]
        public void ScreenStringId_Returns_FloorTransitionScreen()
        {
            Assert.AreEqual("FloorTransitionScreen", _screen.ScreenStringId,
                "Must match the literal string-id used for PushByStringId navigation.");
        }

        [Test]
        public void OnPushed_WithPayload_PopulatesFloorNumber()
        {
            var payload = new FloorTransitionPayload
            {
                FloorNumber = 3,
                FloorTitle = "Catacumbas Profundas"
            };

            InvokePushed(payload);

            var label = GetPrivate<TextMeshProUGUI>(_screen, "_floorNumberLabel");
            Assert.AreEqual("Piso 3", label.text,
                "Floor number label must show 'Piso {N}' from payload.");
        }

        [Test]
        public void OnPushed_WithPayload_PopulatesFloorTitle()
        {
            var payload = new FloorTransitionPayload
            {
                FloorNumber = 2,
                FloorTitle = "Cavernas Oscuras"
            };

            InvokePushed(payload);

            var label = GetPrivate<TextMeshProUGUI>(_screen, "_floorTitleLabel");
            Assert.AreEqual("Cavernas Oscuras", label.text,
                "Floor title label must match payload FloorTitle.");
            Assert.IsTrue(label.gameObject.activeSelf,
                "Floor title label must be active when title is provided.");
        }

        [Test]
        public void OnPushed_WithNullPayload_DoesNotCrash()
        {
            Assert.DoesNotThrow(() => InvokePushed(null),
                "Null payload must not throw.");
        }

        [Test]
        public void OnPushed_WithEmptyTitle_HidesTitleLabel()
        {
            var payload = new FloorTransitionPayload
            {
                FloorNumber = 1,
                FloorTitle = ""
            };

            InvokePushed(payload);

            var label = GetPrivate<TextMeshProUGUI>(_screen, "_floorTitleLabel");
            Assert.IsFalse(label.gameObject.activeSelf,
                "Floor title label must be deactivated when title is empty.");
        }

        [Test]
        public void OnPopped_CleansUpState()
        {
            var payload = new FloorTransitionPayload
            {
                FloorNumber = 5,
                FloorTitle = "Piso Final"
            };

            InvokePushed(payload);
            InvokePopped();

            var floorNumber = GetPrivateValue(_screen, "_currentFloorNumber");
            Assert.AreEqual(0, floorNumber,
                "_currentFloorNumber must be 0 after OnPopped.");
        }

        // ---------------- helpers ----------------

        private Button AttachButton(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_screenGO.transform, false);
            return go.AddComponent<Button>();
        }

        private void InvokePushed(IScreenPayload payload)
        {
            ((IBaseScreen)_screen)._Internal_OnPushed(payload);
        }

        private void InvokePopped()
        {
            ((IBaseScreen)_screen)._Internal_OnPopped();
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = null;
            var type = target.GetType();
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = null;
            var type = target.GetType();
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }

        private static object GetPrivateValue(object target, string fieldName)
        {
            FieldInfo field = null;
            var type = target.GetType();
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in {target.GetType().Name}.");
            return field.GetValue(target);
        }
    }
}
