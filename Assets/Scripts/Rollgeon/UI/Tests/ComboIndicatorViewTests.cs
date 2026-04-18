using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica <see cref="ComboIndicatorView"/>: el TypedEvent de combo filtra por
    /// playerGuid, y los eventos <c>OnComboBlocked/Unblocked</c> togglean el
    /// overlay del row. Plan §3.10.
    /// </summary>
    [TestFixture]
    public class ComboIndicatorViewTests
    {
        private GameObject _go;
        private ComboIndicatorView _view;
        private Guid _playerGuid;
        private GameObject _parOverlay;
        private GameObject _generalaOverlay;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("ComboIndicator");
            _view = _go.AddComponent<ComboIndicatorView>();

            _parOverlay = new GameObject("ParBlockedOverlay");
            _parOverlay.transform.SetParent(_go.transform, false);
            _parOverlay.SetActive(true);

            _generalaOverlay = new GameObject("GeneralaBlockedOverlay");
            _generalaOverlay.transform.SetParent(_go.transform, false);
            _generalaOverlay.SetActive(true);

            // Inyectar _rows via reflection — cada row es un struct ComboRow.
            var rows = new List<ComboIndicatorView.ComboRow>
            {
                new ComboIndicatorView.ComboRow { ComboId = "combo.par", BlockedOverlay = _parOverlay },
                new ComboIndicatorView.ComboRow { ComboId = "combo.generala", BlockedOverlay = _generalaOverlay },
            };
            AssignPrivate(_view, "_rows", rows);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<ComboMatchedPayload>.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Bind_DisablesAllBlockedOverlays()
        {
            _view.Bind(_playerGuid);

            Assert.IsFalse(_parOverlay.activeSelf);
            Assert.IsFalse(_generalaOverlay.activeSelf);
        }

        [Test]
        public void OnComboBlocked_ActivatesOverlayForMatchedComboId()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnComboBlocked, _playerGuid, "combo.par", 2);

            Assert.IsTrue(_parOverlay.activeSelf);
            Assert.IsFalse(_generalaOverlay.activeSelf);
        }

        [Test]
        public void OnComboBlocked_FiltersByPlayerGuid()
        {
            _view.Bind(_playerGuid);
            var otherGuid = Guid.NewGuid();
            EventManager.Trigger(EventName.OnComboBlocked, otherGuid, "combo.par", 2);

            Assert.IsFalse(_parOverlay.activeSelf,
                "Evento de otra entidad no debe mutar el overlay del player.");
        }

        [Test]
        public void OnComboUnblocked_DeactivatesOverlay()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnComboBlocked, _playerGuid, "combo.par", 2);
            Assert.IsTrue(_parOverlay.activeSelf);

            EventManager.Trigger(EventName.OnComboUnblocked, _playerGuid, "combo.par");

            Assert.IsFalse(_parOverlay.activeSelf);
        }

        [Test]
        public void ComboMatched_FromOtherEntity_IsIgnored()
        {
            _view.Bind(_playerGuid);

            var payload = new ComboMatchedPayload
            {
                SourceGuid = Guid.NewGuid(),
                ComboId = "combo.par",
                BaseDamage = 5,
            };
            Assert.DoesNotThrow(() => TypedEvent<ComboMatchedPayload>.Raise(payload),
                "El handler debe retornar sin crashear para sources distintos al player.");
        }

        [Test]
        public void SetBlocked_UnknownComboId_IsSilentNoOp()
        {
            _view.Bind(_playerGuid);
            Assert.DoesNotThrow(() => _view.SetBlocked("combo.unknown", true, 0),
                "Un comboId sin row configurado debe ignorarse silenciosamente.");
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado.");
            field.SetValue(target, value);
        }
    }
}
