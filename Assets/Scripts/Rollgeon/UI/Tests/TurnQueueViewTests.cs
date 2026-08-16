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
    /// Verifica que <see cref="TurnQueueView"/> arma la ventana del carrusel
    /// (5 slots por posición de display: activo + 4 próximos) a partir de
    /// <c>OnTurnQueueBuilt</c>, rota con <c>OnTurnStarted</c> y refleja
    /// <c>OnEntityDestroyed</c>. En EditMode las poses aplican por snap
    /// (sin tweens) — asserts deterministas.
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

            var containerGO = new GameObject("Container", typeof(RectTransform));
            containerGO.transform.SetParent(_go.transform, false);
            _container = containerGO.transform;

            // Prefab "a mano" — un GO con TurnSlotView en una subescena, no instanciable
            // como asset pero instanciable en runtime con Instantiate(). RectTransform
            // requerido: el carrusel posiciona/escala los slots a mano.
            var prefabGO = new GameObject("TurnSlotPrefab", typeof(RectTransform));
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

        private static List<Guid> MakeGuids(int count)
        {
            var guids = new List<Guid>(count);
            for (int i = 0; i < count; i++) guids.Add(Guid.NewGuid());
            return guids;
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
        public void OnTurnQueueBuilt_NonEmptyOrder_CreatesWindowOfFiveSlots()
        {
            // Arrange
            _view.Bind(Guid.NewGuid());
            var guids = MakeGuids(3);

            // Act
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)guids, 0);

            // Assert — la ventana es fija (2 pasados + activo + 2 próximos),
            // independiente de la cantidad de participantes.
            Assert.AreEqual(TurnQueueCarouselLayout.WindowSize, _container.childCount,
                "La ventana del carrusel siempre instancia 5 slots.");
        }

        [Test]
        public void OnTurnQueueBuilt_Rebuild_ReplacesPreviousWindow()
        {
            // Arrange
            _view.Bind(Guid.NewGuid());
            var first = MakeGuids(3);
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)first, 0);
            Assert.AreEqual(TurnQueueCarouselLayout.WindowSize, _container.childCount);

            // Act — set distinto: rebuild completo de la ventana.
            var second = MakeGuids(2);
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)second, 1);

            // Assert — sigue habiendo exactamente 5 hijos (sin leak) y los guids
            // viejos ya no están en pantalla.
            Assert.AreEqual(TurnQueueCarouselLayout.WindowSize, _container.childCount,
                "Un rebuild no debe acumular slots viejos.");
            Assert.IsNull(_view.FindSlot(first[0]),
                "Los guids del round anterior no deben seguir visibles.");
            Assert.IsNotNull(_view.FindSlot(second[0]));
        }

        [Test]
        public void OnTurnQueueBuilt_SameSequence_KeepsSlotInstances()
        {
            // Arrange — el wrap de round re-dispara el evento con la misma secuencia;
            // la vista no debe destruir/recrear slots (el loop quedaría con un pop).
            _view.Bind(Guid.NewGuid());
            var guids = MakeGuids(4);
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)guids, 0);
            var firstChild = _container.GetChild(0).gameObject;

            // Act — mismo contenido, round siguiente.
            EventManager.Trigger(EventName.OnTurnQueueBuilt,
                (IReadOnlyList<Guid>)new List<Guid>(guids), 1);

            // Assert
            Assert.AreEqual(TurnQueueCarouselLayout.WindowSize, _container.childCount);
            Assert.AreSame(firstChild, _container.GetChild(0).gameObject,
                "Con la misma secuencia las instancias de slot deben sobrevivir.");
        }

        [Test]
        public void OnTurnStarted_MovesActorToLeftmostSlot()
        {
            // Arrange — N=5: cada guid aparece una sola vez en la ventana.
            _view.Bind(Guid.NewGuid());
            var guids = MakeGuids(5);
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)guids, 0);

            // Act
            EventManager.Trigger(EventName.OnTurnStarted, guids[1]);

            // Assert — en EditMode el shift es snap: el actor del turno queda a la
            // izquierda de todos los demás slots.
            var slot = _view.FindSlot(guids[1]);
            Assert.IsNotNull(slot);
            Assert.AreEqual(guids[1], slot.SlotGuid);
            float activeX = slot.Rect.anchoredPosition.x;
            for (int i = 0; i < _container.childCount; i++)
            {
                var child = (RectTransform)_container.GetChild(i);
                if (child == slot.Rect) continue;
                Assert.Less(activeX, child.anchoredPosition.x,
                    "El actor con el turno debe ser el slot de más a la izquierda.");
            }
        }

        [Test]
        public void OnTurnStarted_ThreeParticipants_AdvancesWithoutExceptions()
        {
            // Arrange — con N=3 la ventana repite actores; el avance no debe romper.
            _view.Bind(Guid.NewGuid());
            var guids = MakeGuids(3);
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)guids, 0);

            // Act + Assert — un round completo, incluido el wrap.
            Assert.DoesNotThrow(() =>
            {
                EventManager.Trigger(EventName.OnTurnStarted, guids[0]);
                EventManager.Trigger(EventName.OnTurnStarted, guids[1]);
                EventManager.Trigger(EventName.OnTurnStarted, guids[2]);
                EventManager.Trigger(EventName.OnTurnStarted, guids[0]);
            });
            Assert.AreEqual(TurnQueueCarouselLayout.WindowSize, _container.childCount);
        }

        [Test]
        public void OnEntityDestroyed_MarksVisibleSlotDestroyed()
        {
            // Arrange
            _view.Bind(Guid.NewGuid());
            var guids = MakeGuids(2);
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)guids, 0);

            // Act
            EventManager.Trigger(EventName.OnEntityDestroyed, guids[0], Guid.Empty);

            // Assert — el guid destruido sigue visible (marcado) hasta el próximo build.
            var slot = _view.FindSlot(guids[0]);
            Assert.IsNotNull(slot, "El slot debe seguir en la ventana post-destroyed.");
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
            var guids = MakeGuids(2);

            // Act + Assert
            _view.Bind(Guid.NewGuid());
            Assert.DoesNotThrow(() => _view.RebuildQueue(guids));
            Assert.AreEqual(TurnQueueCarouselLayout.WindowSize, _container.childCount);
        }

        [Test]
        public void Unbind_StopsReactingToBuildEvents()
        {
            // Arrange
            _view.Bind(Guid.NewGuid());
            _view.Unbind();

            // Act
            var guids = MakeGuids(1);
            EventManager.Trigger(EventName.OnTurnQueueBuilt, (IReadOnlyList<Guid>)guids, 0);

            // Assert
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
