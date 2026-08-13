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
    /// Cubre el retrato de <see cref="BossBarView"/>: se resuelve por guid vía
    /// <see cref="IEntityPortraitResolver"/> y, cuando no hay sprite (o no hay servicio, o la Image
    /// no está cableada), la barra sigue funcionando y la Image queda escondida en vez de mostrar el
    /// cuadro blanco del default de uGUI.
    /// </summary>
    [TestFixture]
    public class BossBarViewPortraitTests
    {
        private GameObject _go;
        private BossBarView _view;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("Canvas_BossBar");
            _view = _go.AddComponent<BossBarView>();
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.RemoveService<IEntityPortraitResolver>();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            foreach (var obj in _createdObjects)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        [Test]
        public void Show_WithResolvedSprite_AssignsAndEnablesPortrait()
        {
            var portrait = AddPortraitImage();
            portrait.enabled = false;
            var guid = Guid.NewGuid();
            var sprite = CreateSprite();
            RegisterResolver((guid, sprite));

            _view.Show(guid, "EL CROUPIER");

            Assert.AreSame(sprite, portrait.sprite, "La Image debe mostrar el sprite del guid del jefe.");
            Assert.IsTrue(portrait.enabled, "Con sprite resuelto la Image tiene que estar visible.");
        }

        [Test]
        public void Show_GuidWithoutSprite_HidesPortrait()
        {
            var portrait = AddPortraitImage();
            portrait.sprite = CreateSprite();
            RegisterResolver();

            _view.Show(Guid.NewGuid(), "JEFE SIN RETRATO");

            Assert.IsNull(portrait.sprite, "Sin sprite resuelto no debe quedar el sprite de otro jefe.");
            Assert.IsFalse(portrait.enabled, "Sin sprite la Image se esconde, no muestra el cuadro blanco.");
        }

        [Test]
        public void Show_WithoutResolverService_HidesPortraitAndDoesNotThrow()
        {
            var portrait = AddPortraitImage();

            Assert.DoesNotThrow(() => _view.Show(Guid.NewGuid(), "SIN SERVICIO"));
            Assert.IsFalse(portrait.enabled);
        }

        [Test]
        public void Show_WithoutPortraitImage_DoesNotThrow()
        {
            // El caso de los prefabs viejos: la barra existe pero nadie le cableó el retrato.
            var guid = Guid.NewGuid();
            RegisterResolver((guid, CreateSprite()));

            Assert.DoesNotThrow(() => _view.Show(guid, "SIN IMAGE"));
        }

        [Test]
        public void Show_EmptyGuid_HidesPortrait()
        {
            var portrait = AddPortraitImage();
            RegisterResolver((Guid.Empty, CreateSprite()));

            _view.Show(Guid.Empty, "GUID VACIO");

            Assert.IsFalse(portrait.enabled, "Guid.Empty no identifica a nadie — sin retrato.");
        }

        private Image AddPortraitImage()
        {
            var imageGo = new GameObject("Portrait");
            imageGo.transform.SetParent(_go.transform, false);
            var image = imageGo.AddComponent<Image>();
            AssignPrivate(_view, "_portrait", image);
            return image;
        }

        private void RegisterResolver(params (Guid Guid, Sprite Sprite)[] entries)
        {
            var fake = new FakePortraitResolver();
            foreach (var entry in entries) fake.Register(entry.Guid, entry.Sprite);
            ServiceLocator.AddService<IEntityPortraitResolver>(fake, ServiceScope.Run);
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(2, 2);
            _createdObjects.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            _createdObjects.Add(sprite);
            return sprite;
        }

        /// <summary>Registro plano guid → sprite, sin el fallback lazy del player.</summary>
        private sealed class FakePortraitResolver : IEntityPortraitResolver
        {
            private readonly Dictionary<Guid, Sprite> _portraits = new();
            public void Register(Guid entityId, Sprite portrait) => _portraits[entityId] = portrait;
            public void Unregister(Guid entityId) => _portraits.Remove(entityId);
            public bool TryGetPortrait(Guid entityId, out Sprite portrait)
                => _portraits.TryGetValue(entityId, out portrait);
            public void Clear() => _portraits.Clear();
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
