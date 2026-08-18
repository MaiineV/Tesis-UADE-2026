using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Combat.Weakness;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Combos.Tests;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// El badge de debilidad de <see cref="BossBarView"/>. El dato sale del
    /// <see cref="IWeaknessRegistry"/>, que la IA puede reescribir, y no del <c>EnemyDataSO</c>.
    /// </summary>
    [TestFixture]
    public class BossBarViewWeaknessTests
    {
        // Ids inexistentes a propósito: con los reales el label saldría traducido por la tabla
        // Content y el assert dependería del locale del editor. Así cae al DisplayName del fixture.
        private const string LadderId = "combo.test_ladder";
        private const string FullHouseId = "combo.test_full_house";

        private GameObject _go;
        private BossBarView _view;
        private GameObject _badgeRoot;
        private Image _icon;
        private TMPro.TextMeshProUGUI _label;

        private WeaknessRegistry _registry;
        private RulesetSO _ruleset;
        private ComboCatalogSO _catalog;
        private Combo_Escalera _ladder;
        private Combo_FullHouse _fullHouse;

        private readonly List<UnityEngine.Object> _created = new();

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();

            _go = new GameObject("Canvas_BossBar");
            _view = _go.AddComponent<BossBarView>();

            _ladder = ComboTestUtils.CreateCombo<Combo_Escalera>(LadderId, 35);
            _fullHouse = ComboTestUtils.CreateCombo<Combo_FullHouse>(FullHouseId, 40);
            ComboTestUtils.SetField(_ladder, "_displayName", "ESCALERA");
            ComboTestUtils.SetField(_fullHouse, "_displayName", "FULL");

            _catalog = ScriptableObject.CreateInstance<ComboCatalogSO>();
            ComboTestUtils.SetField(_catalog, "_entries",
                new List<BaseComboSO> { _ladder, _fullHouse });
            ServiceLocator.AddService<ComboCatalogSO>(_catalog, ServiceScope.Run);

            _registry = new WeaknessRegistry();
            ServiceLocator.AddService<IWeaknessRegistry>(_registry, ServiceScope.Run);

            _ruleset = ScriptableObject.CreateInstance<RulesetSO>();
            ServiceLocator.AddService<RulesetSO>(_ruleset, ServiceScope.Run);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.RemoveService<ComboCatalogSO>();
            ServiceLocator.RemoveService<IWeaknessRegistry>();
            ServiceLocator.RemoveService<RulesetSO>();

            // NUnit reusa la instancia del fixture: un test que no cablea el badge vería las refs
            // destruidas del anterior.
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            _badgeRoot = null;
            _icon = null;
            _label = null;

            foreach (var obj in new UnityEngine.Object[] { _ladder, _fullHouse, _catalog, _ruleset })
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // -------------------------------------------------------------------
        // Badge visible
        // -------------------------------------------------------------------

        [Test]
        public void Show_BossWithWeakness_ShowsIconAndMultiplier()
        {
            // Arrange
            WireBadge();
            var sprite = CreateSprite();
            ComboTestUtils.SetField(_ladder, "_icon", sprite);
            var boss = Guid.NewGuid();
            _registry.SetWeakness(boss, LadderId, 1.5f);

            // Act
            _view.Show(boss, "LA BANDIDA");

            // Assert
            Assert.IsTrue(_badgeRoot.activeSelf, "Con debilidad registrada el badge tiene que estar prendido.");
            Assert.AreSame(sprite, _icon.sprite, "El icono es el del combo al que el jefe es débil.");
            Assert.IsTrue(_icon.enabled);
            Assert.AreEqual(LadderId, _view.WeaknessComboId);
            Assert.AreEqual(1.5f, _view.WeaknessMultiplier, 0.0001f);
            Assert.AreEqual(string.Format(BossBarView.DefaultWeaknessFormat, 1.5f), _label.text,
                "Con icono autorado el label es sólo el multiplicador.");
        }

        [Test]
        public void Show_WeaknessWithoutOverride_FallsBackToRulesetDefault()
        {
            // Arrange — override 0 = "usá el default global" (contrato de IWeaknessRegistry).
            WireBadge();
            _ruleset.Weakness.DefaultMultiplier = 2f;
            var boss = Guid.NewGuid();
            _registry.SetWeakness(boss, LadderId, 0f);

            // Act
            _view.Show(boss, "JEFE");

            // Assert
            Assert.AreEqual(2f, _view.WeaknessMultiplier, 0.0001f,
                "Sin override per-enemy el badge muestra el default del RulesetSO.");
        }

        [Test]
        public void Show_ComboWithoutIcon_PutsComboNameInLabel()
        {
            // Arrange
            WireBadge();
            var boss = Guid.NewGuid();
            _registry.SetWeakness(boss, LadderId, 1.5f);

            // Act
            _view.Show(boss, "JEFE");

            // Assert
            Assert.IsTrue(_badgeRoot.activeSelf, "Sin sprite el badge sigue visible: la regla existe igual.");
            Assert.IsFalse(_icon.enabled, "Sin sprite la Image se esconde, no muestra el cuadro blanco.");
            StringAssert.Contains("ESCALERA", _label.text,
                "Sin icono, el número solo no dice a qué combo le pega.");
        }

        // -------------------------------------------------------------------
        // Badge apagado
        // -------------------------------------------------------------------

        [Test]
        public void Show_BossWithoutWeakness_HidesBadge()
        {
            // Arrange — jefe sin WeaknessComboId: el spawn no lo registra.
            WireBadge();

            // Act
            _view.Show(Guid.NewGuid(), "JEFE SIN DEBILIDAD");

            // Assert
            Assert.IsFalse(_badgeRoot.activeSelf);
            Assert.IsFalse(_icon.enabled);
            Assert.IsEmpty(_label.text);
            Assert.IsNull(_view.WeaknessComboId);
            Assert.AreEqual(1f, _view.WeaknessMultiplier, 0.0001f);
        }

        [Test]
        public void Show_WeaknessRegisteredWithEmptyCombo_HidesBadge()
        {
            // Arrange — entry presente pero sin combo.
            WireBadge();
            var boss = Guid.NewGuid();
            _registry.SetWeakness(boss, string.Empty, 1.5f);

            // Act
            _view.Show(boss, "JEFE");

            // Assert
            Assert.IsFalse(_badgeRoot.activeSelf);
            Assert.IsNull(_view.WeaknessComboId);
        }

        [Test]
        public void Hide_ClearsBadge()
        {
            // Arrange
            WireBadge();
            var boss = Guid.NewGuid();
            _registry.SetWeakness(boss, LadderId, 1.5f);
            _view.Show(boss, "LA BANDIDA");

            // Act
            _view.Hide();

            // Assert — la barra es persistente entre salas: un badge pegado mentiría en la próxima.
            Assert.IsFalse(_badgeRoot.activeSelf);
            Assert.IsNull(_view.WeaknessComboId);
            Assert.IsEmpty(_label.text);
        }

        // -------------------------------------------------------------------
        // Debilidad viva
        // -------------------------------------------------------------------

        [Test]
        public void TurnStarted_AfterWeaknessReassigned_RepaintsBadge()
        {
            // Arrange — AINode_AdoptWeakness reescribe el registry a mitad de combate.
            WireBadge();
            var boss = Guid.NewGuid();
            _registry.SetWeakness(boss, LadderId, 1.5f);
            _view.Show(boss, "LA GENERALA");
            _registry.SetWeakness(boss, FullHouseId, 2f);

            // Act
            EventManager.Trigger(EventName.OnTurnStarted, boss);

            // Assert
            Assert.AreEqual(FullHouseId, _view.WeaknessComboId,
                "El badge sigue al registry, que es lo que la IA reescribe — no al EnemyDataSO.");
            Assert.AreEqual(2f, _view.WeaknessMultiplier, 0.0001f);
            StringAssert.Contains("FULL", _label.text);
        }

        // -------------------------------------------------------------------
        // Degradación
        // -------------------------------------------------------------------

        [Test]
        public void Show_WithoutBadgeRefs_DoesNotThrow()
        {
            // El caso de los prefabs viejos: la barra existe pero nadie cableó el badge.
            var boss = Guid.NewGuid();
            _registry.SetWeakness(boss, LadderId, 1.5f);

            Assert.DoesNotThrow(() => _view.Show(boss, "SIN BADGE"));
            Assert.AreEqual(LadderId, _view.WeaknessComboId,
                "El estado se resuelve igual aunque no haya nada que pintar.");
        }

        [Test]
        public void Show_WithoutRegistryService_HidesBadgeAndDoesNotThrow()
        {
            WireBadge();
            ServiceLocator.RemoveService<IWeaknessRegistry>();

            Assert.DoesNotThrow(() => _view.Show(Guid.NewGuid(), "SIN SERVICIO"));
            Assert.IsFalse(_badgeRoot.activeSelf);
        }

        [Test]
        public void Show_ComboMissingFromCatalog_StillShowsMultiplier()
        {
            // Arrange — catálogo incompleto: el id crudo en el label delata el combo sin autorar.
            WireBadge();
            ServiceLocator.RemoveService<ComboCatalogSO>();
            var boss = Guid.NewGuid();
            _registry.SetWeakness(boss, LadderId, 1.5f);

            // Act
            _view.Show(boss, "JEFE");

            // Assert
            Assert.IsTrue(_badgeRoot.activeSelf);
            StringAssert.Contains(LadderId, _label.text);
        }

        [Test]
        public void Show_WithoutRuleset_FallsBackToBalanceDefaultNotToOne()
        {
            // Arrange
            WireBadge();
            ServiceLocator.RemoveService<RulesetSO>();
            var boss = Guid.NewGuid();
            _registry.SetWeakness(boss, LadderId, 0f);

            // Act
            _view.Show(boss, "JEFE");

            // Assert
            Assert.AreEqual(new WeaknessConfig().DefaultMultiplier, _view.WeaknessMultiplier, 0.0001f);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private void WireBadge()
        {
            _badgeRoot = new GameObject("WeaknessBadge");
            _badgeRoot.transform.SetParent(_go.transform, false);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(_badgeRoot.transform, false);
            _icon = iconGo.AddComponent<Image>();

            var labelGo = new GameObject("Multiplier");
            labelGo.transform.SetParent(_badgeRoot.transform, false);
            _label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();

            AssignPrivate(_view, "_weaknessRoot", _badgeRoot);
            AssignPrivate(_view, "_weaknessIcon", _icon);
            AssignPrivate(_view, "_weaknessText", _label);
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(2, 2);
            _created.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            _created.Add(sprite);
            return sprite;
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
