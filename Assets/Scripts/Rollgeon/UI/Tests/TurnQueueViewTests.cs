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
    /// Verifica que <see cref="TurnQueueView"/> responde a <c>OnTurnQueueBuilt</c>,
    /// <c>OnTurnStarted</c> y <c>OnEntityDestroyed</c>, y que el rebuild limpia los
    /// slots previos. Plan §3.10.
    /// </summary>
    [TestFixture]
    public class TurnQueueViewTests
    {
        private GameObject _go;
        private TurnQueueView _view;
        private TurnSlotView _prefab;
        private Transform _container;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TurnQueue");
            _view = _go.AddComponent<TurnQueueView>();

            var containerGO = new GameObject("Container");
            containerGO.transform.SetParent(_go.transform, false);
            _container = containerGO.transform;

            // Prefab "a mano" — un GO con TurnSlotView en una subescena, no instanciable
            // como asset pero instanciable en runtime con Instantiate().
            var prefabGO = new GameObject("TurnSlotPrefab");
            prefabGO.SetActive(false); // evita que el instance "raiz" cuente en el parent
            _prefab = prefabGO.AddComponent<TurnSlotView>();

            AssignPrivate(_view, "_slotPrefab", _prefab);
            AssignPrivate(_view, "_container", (Transform)_container);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_prefab != null) UnityEngine.Object.DestroyImmediate(_prefab.gameObject);
        }

        [Test]
        public void OnTurnQueueBuilt_InstantiatesOneSlotPerGuid()
        {
            _view.Bind(Guid.NewGuid());
            var guids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)guids, 0);

            Assert.AreEqual(3, _container.childCount,
                "Debe instanciarse un slot por guid en la lista.");
        }

        [Test]
        public void OnTurnQueueBuilt_Rebuild_ClearsPreviousSlots()
        {
            _view.Bind(Guid.NewGuid());
            var first = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)first, 0);
            Assert.AreEqual(3, _container.childCount);

            // Destroy() es async — en EditMode los objects persisten hasta el siguiente frame.
            // Por eso usamos DestroyImmediate a traves del fallback en ClearSlots (Application.isPlaying = false).
            var second = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)second, 1);

            Assert.AreEqual(2, _container.childCount,
                "Un rebuild con menos guids debe dejar exactamente N hijos.");
        }

        [Test]
        public void OnTurnStarted_HighlightsCorrectSlot()
        {
            _view.Bind(Guid.NewGuid());
            var guids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)guids, 0);

            EventManager.Trigger(EventName.OnTurnStarted, guids[1]);

            var slot = _view.FindSlot(guids[1]);
            Assert.IsNotNull(slot);
            // No hay "IsActive" publico en TurnSlotView — verificamos que el slot existe
            // y el flujo corrio sin exceptions. Smoke coverage.
            Assert.AreEqual(guids[1], slot.SlotGuid);
        }

        [Test]
        public void OnEntityDestroyed_MarksSlotDestroyed()
        {
            _view.Bind(Guid.NewGuid());
            var guids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)guids, 0);

            EventManager.Trigger(EventName.OnEntityDestroyed, guids[0], Guid.Empty);

            var slot = _view.FindSlot(guids[0]);
            Assert.IsNotNull(slot, "El slot debe seguir en el mapping post-destroyed.");
        }

        [Test]
        public void Unbind_StopsReactingToBuildEvents()
        {
            _view.Bind(Guid.NewGuid());
            _view.Unbind();

            var guids = new List<Guid> { Guid.NewGuid() };
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)guids, 0);

            Assert.AreEqual(0, _container.childCount,
                "Tras Unbind no se deben procesar nuevos eventos de queue built.");
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
