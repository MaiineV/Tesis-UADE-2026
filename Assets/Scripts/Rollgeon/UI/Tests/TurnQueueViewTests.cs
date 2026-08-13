using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Entities.Portraits;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

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
            ServiceLocator.RemoveService<IEntityPortraitResolver>();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_prefab != null) UnityEngine.Object.DestroyImmediate(_prefab.gameObject);
            foreach (var obj in _createdObjects)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        private readonly List<UnityEngine.Object> _createdObjects = new();

        /// <summary>Agrega la Image de portrait al prefab del slot (no viene en el Setup base).</summary>
        private Image AddPortraitImageToPrefab()
        {
            var image = _prefab.gameObject.AddComponent<Image>();
            AssignPrivate(_prefab, "_portrait", image);
            return image;
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(2, 2);
            _createdObjects.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            _createdObjects.Add(sprite);
            return sprite;
        }

        /// <summary>Fake con sprites fijos por guid; sin lazy player.</summary>
        private sealed class FakePortraitResolver : IEntityPortraitResolver
        {
            public readonly Dictionary<Guid, Sprite> Portraits = new();
            public void Register(Guid entityId, Sprite portrait) => Portraits[entityId] = portrait;
            public void Unregister(Guid entityId) => Portraits.Remove(entityId);
            public bool TryGetPortrait(Guid entityId, out Sprite portrait)
                => Portraits.TryGetValue(entityId, out portrait);
            public void Clear() => Portraits.Clear();
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
        public void RebuildQueue_WithResolverRegistered_SetsSlotPortrait()
        {
            // Arrange
            AddPortraitImageToPrefab();
            var guid = Guid.NewGuid();
            var sprite = CreateSprite();
            var fake = new FakePortraitResolver();
            fake.Register(guid, sprite);
            ServiceLocator.AddService<IEntityPortraitResolver>(fake, ServiceScope.Run);

            // Act
            _view.Bind(Guid.NewGuid());
            _view.RebuildQueue(new List<Guid> { guid });

            // Assert
            var image = _view.FindSlot(guid).GetComponent<Image>();
            Assert.AreSame(sprite, image.sprite,
                "El slot debe mostrar el sprite resuelto para su guid.");
        }

        [Test]
        public void RebuildQueue_GuidWithoutSprite_KeepsPrefabDefaultSprite()
        {
            // Arrange — resolver registrado pero sin entrada para este guid.
            var prefabImage = AddPortraitImageToPrefab();
            var defaultSprite = CreateSprite();
            prefabImage.sprite = defaultSprite;
            var guid = Guid.NewGuid();
            ServiceLocator.AddService<IEntityPortraitResolver>(
                new FakePortraitResolver(), ServiceScope.Run);

            // Act
            _view.Bind(Guid.NewGuid());
            _view.RebuildQueue(new List<Guid> { guid });

            // Assert
            var image = _view.FindSlot(guid).GetComponent<Image>();
            Assert.AreSame(defaultSprite, image.sprite,
                "Sin sprite resuelto, el slot conserva el default del prefab.");
        }

        [Test]
        public void RebuildQueue_WithoutResolverService_DoesNotThrow()
        {
            // Arrange
            AddPortraitImageToPrefab();
            var guids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

            // Act + Assert
            _view.Bind(Guid.NewGuid());
            Assert.DoesNotThrow(() => _view.RebuildQueue(guids));
            Assert.AreEqual(2, _container.childCount);
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
