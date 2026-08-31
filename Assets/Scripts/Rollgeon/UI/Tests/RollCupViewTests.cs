using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Vaso de generala del Pool de Rolls: fetch inicial vía IRollPoolService,
    /// updates por OnPlayerRollsChanged, filtro por guid, ocultamiento fuera de
    /// combate y pose boca abajo con 0 rolls. Rig inactivo y sin juice → path
    /// instantáneo (EditMode no corre Awake/OnEnable — todo engancha en Bind).
    /// </summary>
    [TestFixture]
    public class RollCupViewTests
    {
        private sealed class FakeRollPoolService : IRollPoolService
        {
            public int Current = 5;
            public int Max = 15;
            public bool InCombat = true;

            public bool IsCombatActive => InCombat;
            public void InitializeForEntity(Guid entityId) { }
            public bool TrySpendRolls(Guid entityId, int count) => false;
            public int Drain(Guid entityId, int amount) => 0;
            public void AddRolls(Guid entityId, int amount) { }
            public int GetCurrent(Guid entityId) => Current;
            public int GetMax(Guid entityId) => Max;
            public int GetRollsPerTurn(Guid entityId) => 5;
            public void AddRollPoolBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value) { }
        }

        private GameObject _go;
        private RollCupView _view;
        private RectTransform _cup;
        private TextMeshProUGUI _label;
        private FakeRollPoolService _rolls;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();
            _rolls = new FakeRollPoolService();
            ServiceLocator.AddService<IRollPoolService>(_rolls);

            _go = new GameObject("RollCup", typeof(RectTransform));
            _go.SetActive(false);
            _view = _go.AddComponent<RollCupView>();

            var cupGo = new GameObject("Cup", typeof(RectTransform));
            cupGo.transform.SetParent(_go.transform, false);
            _cup = (RectTransform)cupGo.transform;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_go.transform, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();

            AssignPrivate(_view, "_cup", _cup);
            AssignPrivate(_view, "_label", _label);
        }

        [TearDown]
        public void Teardown()
        {
            InvokeNonPublic(_view, "OnDisable");
            EventManager.ResetEventDictionary();
            ServiceLocator.RemoveService<IRollPoolService>();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Bind_FetchesInitialStateFromService()
        {
            // Arrange
            _rolls.Current = 5;
            _rolls.Max = 15;

            // Act
            _view.Bind(_playerGuid);

            // Assert
            Assert.AreEqual("5/15", _label.text);
            Assert.IsFalse(_view.IsCupFaceDown);
        }

        [Test]
        public void RollsEvent_UpdatesLabel()
        {
            // Arrange
            _view.Bind(_playerGuid);

            // Act
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 1, 15);

            // Assert
            Assert.AreEqual("1/15", _label.text);
            Assert.IsFalse(_view.IsCupFaceDown);
        }

        [Test]
        public void RollsEvent_OtherGuid_Ignored()
        {
            // Arrange
            _view.Bind(_playerGuid);

            // Act
            EventManager.Trigger(EventName.OnPlayerRollsChanged, Guid.NewGuid(), 0, 15);

            // Assert
            Assert.AreEqual("5/15", _label.text);
            Assert.IsFalse(_view.IsCupFaceDown);
        }

        [Test]
        public void RollsEvent_ToZero_SetsCupFaceDownInstant()
        {
            // Arrange
            _view.Bind(_playerGuid);

            // Act
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 0, 15);

            // Assert: sin juice el path es instantáneo — pose directa boca abajo.
            Assert.IsTrue(_view.IsCupFaceDown);
            Assert.AreEqual(RollCupMath.FaceDownZ, _cup.localEulerAngles.z, 0.01f);
            Assert.AreEqual("0/15", _label.text);
        }

        [Test]
        public void RollsEvent_RecoverFromZero_RestoresUprightPose()
        {
            // Arrange
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 0, 15);

            // Act
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 5, 15);

            // Assert
            Assert.IsFalse(_view.IsCupFaceDown);
            Assert.AreEqual(RollCupMath.UprightZ, _cup.localEulerAngles.z, 0.01f);
            Assert.AreEqual("5/15", _label.text);
        }

        [Test]
        public void OutsideCombat_HidesCupAndLabel()
        {
            // Arrange: el pool es combat-only — en exploración el vaso se oculta.
            _rolls.InCombat = false;

            // Act
            _view.Bind(_playerGuid);

            // Assert
            Assert.IsFalse(_cup.gameObject.activeSelf,
                "Fuera de combate el vaso debe ocultarse.");
            Assert.IsFalse(_label.gameObject.activeSelf,
                "Fuera de combate el número del pool debe ocultarse.");
        }

        [Test]
        public void ReenteringCombat_ShowsCupAgain()
        {
            // Arrange
            _rolls.InCombat = false;
            _view.Bind(_playerGuid);

            // Act
            _rolls.InCombat = true;
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 5, 15);

            // Assert
            Assert.IsTrue(_cup.gameObject.activeSelf);
            Assert.AreEqual("5/15", _label.text);
        }

        [Test]
        public void ReenteringCombat_AfterHide_AppliesPoseWithoutTransition()
        {
            // Arrange: al ocultarse se olvida lo mostrado (prev = -1) — el 0 del
            // combate anterior no debe producir un flip espurio al reentrar.
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 0, 15);
            _rolls.InCombat = false;
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 0, 15);

            // Act
            _rolls.InCombat = true;
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerGuid, 5, 15);

            // Assert: pose directa parado, sin herencia del 0 anterior.
            Assert.IsFalse(_view.IsCupFaceDown);
            Assert.AreEqual(RollCupMath.UprightZ, _cup.localEulerAngles.z, 0.01f);
        }

        // ---------------- helpers ----------------

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' no encontrado en {target.GetType().Name}.");
            method.Invoke(target, null);
        }
    }
}
