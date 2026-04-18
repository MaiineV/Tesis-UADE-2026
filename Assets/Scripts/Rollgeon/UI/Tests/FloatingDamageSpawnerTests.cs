using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Entities;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica <see cref="FloatingDamageSpawner"/>: se suscribe al TypedEvent de
    /// dano y al legacy <c>OnFloatingNumberRequested</c>, y sin
    /// <see cref="IEntityPositionResolver"/> cae a fallback sin crashear. Plan §3.10.
    /// </summary>
    [TestFixture]
    public class FloatingDamageSpawnerTests
    {
        private sealed class FakePositionResolver : IEntityPositionResolver
        {
            public Vector3? ReturnPos;
            public Vector3? TryGetWorldPosition(Guid entityId) => ReturnPos;
        }

        private GameObject _go;
        private FloatingDamageSpawner _spawner;
        private GameObject _prefabGO;
        private FloatingDamageInstance _prefab;
        private RectTransform _container;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("FloatingDamageSpawner");
            _spawner = _go.AddComponent<FloatingDamageSpawner>();

            // GameObject construido con typeof(RectTransform) reemplaza el Transform por
            // un RectTransform (truco standard Unity).
            var containerGO = new GameObject("OverlayContainer", typeof(RectTransform));
            containerGO.transform.SetParent(_go.transform, false);
            _container = (RectTransform)containerGO.transform;

            // "Prefab" = objeto inactivo en la escena — Instantiate() lo copia igual.
            _prefabGO = new GameObject("FloatingDamagePrefab", typeof(RectTransform));
            _prefabGO.SetActive(false);
            _prefabGO.AddComponent<CanvasGroup>();
            _prefab = _prefabGO.AddComponent<FloatingDamageInstance>();

            AssignPrivate(_spawner, "_instancePrefab", _prefab);
            AssignPrivate(_spawner, "_overlayContainer", _container);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.RemoveService<IEntityPositionResolver>();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_prefabGO != null) UnityEngine.Object.DestroyImmediate(_prefabGO);
        }

        [Test]
        public void DamageResolved_SpawnsOneInstance()
        {
            _spawner.Bind(_playerGuid);
            int before = _container.childCount;

            var payload = new DamageResolvedPayload
            {
                SourceGuid = _playerGuid,
                TargetGuid = Guid.NewGuid(),
                FinalDamage = 7,
                WeaknessHit = false,
            };
            TypedEvent<DamageResolvedPayload>.Raise(payload);

            Assert.AreEqual(before + 1, _container.childCount,
                "Debe instanciarse exactamente 1 floating number.");
        }

        [Test]
        public void DamageResolved_WithoutPositionResolver_FallsBackGracefully()
        {
            _spawner.Bind(_playerGuid);
            // No registramos IEntityPositionResolver — fallback debe ser centro de pantalla.

            var payload = new DamageResolvedPayload
            {
                TargetGuid = Guid.NewGuid(),
                FinalDamage = 5,
            };

            Assert.DoesNotThrow(() => TypedEvent<DamageResolvedPayload>.Raise(payload));
        }

        [Test]
        public void DamageResolved_WithPositionResolver_UsesWorldPos()
        {
            var resolver = new FakePositionResolver { ReturnPos = new Vector3(1f, 2f, 3f) };
            ServiceLocator.AddService<IEntityPositionResolver>(resolver);

            _spawner.Bind(_playerGuid);

            var payload = new DamageResolvedPayload
            {
                TargetGuid = Guid.NewGuid(),
                FinalDamage = 10,
            };
            Assert.DoesNotThrow(() => TypedEvent<DamageResolvedPayload>.Raise(payload));
            Assert.AreEqual(1, _container.childCount);
        }

        [Test]
        public void Unbind_StopsSpawning()
        {
            _spawner.Bind(_playerGuid);
            _spawner.Unbind();
            int before = _container.childCount;

            var payload = new DamageResolvedPayload { TargetGuid = Guid.NewGuid(), FinalDamage = 1 };
            TypedEvent<DamageResolvedPayload>.Raise(payload);

            Assert.AreEqual(before, _container.childCount,
                "Tras Unbind no se deben instanciar nuevas instancias.");
        }

        [Test]
        public void SpawnAt_Manually_InstantiatesInstance()
        {
            _spawner.Bind(_playerGuid);
            int before = _container.childCount;

            var instance = _spawner.SpawnAt("42", Color.red, Vector3.zero);

            Assert.IsNotNull(instance);
            Assert.AreEqual(before + 1, _container.childCount);
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
