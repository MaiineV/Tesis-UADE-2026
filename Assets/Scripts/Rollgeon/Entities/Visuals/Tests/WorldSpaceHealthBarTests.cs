using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Entities.Visuals.Tests
{
    [TestFixture]
    public class WorldSpaceHealthBarTests
    {
        private GameObject _go;
        private WorldSpaceHealthBar _bar;
        private Image _fillImage;
        private GameObject _barRoot;
        private AttributesManager _attributes;
        private Guid _entityGuid;

        [SetUp]
        public void SetUp()
        {
            _entityGuid = Guid.NewGuid();

            _attributes = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attributes);

            _go = new GameObject("HealthBar");
            _bar = _go.AddComponent<WorldSpaceHealthBar>();

            _barRoot = new GameObject("BarRoot");
            _barRoot.transform.SetParent(_go.transform, false);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(_barRoot.transform, false);
            _fillImage = fillGO.AddComponent<Image>();
            _fillImage.type = Image.Type.Filled;

            AssignPrivate(_bar, "_fillImage", _fillImage);
            AssignPrivate(_bar, "_barRoot", _barRoot);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<HealResolvedPayload>.Clear();
            ServiceLocator.RemoveService<AttributesManager>();
            _attributes?.Dispose();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void Initialize_SetsCorrectFillAndText()
        {
            _bar.Initialize(_entityGuid, 50, 100);

            Assert.AreEqual(0.5f, _fillImage.fillAmount, 0.001f);
        }

        [Test]
        public void DamageResolved_ForEntity_UpdatesFill()
        {
            RegisterHealth(_entityGuid, 100);
            _bar.Initialize(_entityGuid, 100, 100);

            _attributes.SetAttributeValue<Health, int>(_entityGuid, 60);

            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _entityGuid,
                FinalDamage = 40,
                WeaknessHit = false
            });

            Assert.AreEqual(0.6f, _fillImage.fillAmount, 0.001f);
        }

        [Test]
        public void DamageResolved_OtherEntity_IsIgnored()
        {
            RegisterHealth(_entityGuid, 100);
            _bar.Initialize(_entityGuid, 100, 100);

            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = Guid.NewGuid(),
                FinalDamage = 40,
                WeaknessHit = false
            });

            Assert.AreEqual(1f, _fillImage.fillAmount, 0.001f);
        }

        [Test]
        public void HealResolved_ForEntity_UpdatesFill()
        {
            RegisterHealth(_entityGuid, 50);
            _bar.Initialize(_entityGuid, 50, 100);

            _attributes.SetAttributeValue<Health, int>(_entityGuid, 80);

            TypedEvent<HealResolvedPayload>.Raise(new HealResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _entityGuid,
                FinalHeal = 30,
                WasPercentBased = false
            });

            Assert.AreEqual(0.8f, _fillImage.fillAmount, 0.001f);
        }

        [Test]
        public void OnEntityDestroyed_HidesBar()
        {
            _bar.Initialize(_entityGuid, 100, 100);
            Assert.IsTrue(_barRoot.activeSelf);

            EventManager.Trigger(EventName.OnEntityDestroyed, _entityGuid, Guid.Empty);

            Assert.IsFalse(_barRoot.activeSelf);
        }

        [Test]
        public void Teardown_StopsProcessingEvents()
        {
            RegisterHealth(_entityGuid, 100);
            _bar.Initialize(_entityGuid, 100, 100);

            _bar.Teardown();

            _attributes.SetAttributeValue<Health, int>(_entityGuid, 30);
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = _entityGuid,
                FinalDamage = 70,
                WeaknessHit = false
            });

            Assert.AreEqual(1f, _fillImage.fillAmount, 0.001f,
                "Tras Teardown, el fill no debe actualizarse.");
        }

        // ------------------------------------------------------------------
        // BUG-050 — escala world-space contra el zoom (ComputeZoomScale es
        // estatico y puro: sin camara ni instancia del componente).
        // ------------------------------------------------------------------

        [Test]
        public void test_computeZoomScale_atReferenceZoom_returnsOne()
        {
            // Act
            float scale = WorldSpaceHealthBar.ComputeZoomScale(orthographicSize: 9f, referenceZoom: 9f);

            // Assert
            Assert.AreEqual(1f, scale, 0.001f);
        }

        [Test]
        public void test_computeZoomScale_zoomedOutPastMax_clampsAtMaxScale()
        {
            // Arrange — CameraConfigSO.ZoomMax = 22, DefaultZoom = 9 → 22/9 ≈ 2.44,
            // por encima del clamp de 2.2 (BUG-050: legibilidad con zoom out extremo).

            // Act
            float scale = WorldSpaceHealthBar.ComputeZoomScale(orthographicSize: 22f, referenceZoom: 9f);

            // Assert
            Assert.AreEqual(2.2f, scale, 0.001f);
        }

        [Test]
        public void test_computeZoomScale_zoomedIn_neverGoesBelowMinScale()
        {
            // Arrange — CameraConfigSO.ZoomMin = 6, DefaultZoom = 9 → 6/9 ≈ 0.67; la barra
            // no debe encogerse por debajo de su tamaño autorado.

            // Act
            float scale = WorldSpaceHealthBar.ComputeZoomScale(orthographicSize: 6f, referenceZoom: 9f);

            // Assert
            Assert.AreEqual(1f, scale, 0.001f);
        }

        [Test]
        public void test_computeZoomScale_zeroReferenceZoom_returnsMinScaleWithoutDividingByZero()
        {
            // Act
            float scale = WorldSpaceHealthBar.ComputeZoomScale(orthographicSize: 15f, referenceZoom: 0f);

            // Assert
            Assert.AreEqual(1f, scale, 0.001f);
        }

        private void RegisterHealth(Guid entityId, int hp)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute(new Health(hp));
            _attributes.Register(entityId, attrs);
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
