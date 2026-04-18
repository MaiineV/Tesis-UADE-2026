using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Screens;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Smoke test del <see cref="CombatHUDView"/>. Verifica que <c>BindAll</c>
    /// propaga a cada sub-view, que <c>SetEnemyTarget</c> llega al <see cref="EnemyPanelView"/>,
    /// y que los delegates de acciones no crashean cuando no estan cableados.
    /// Plan §3.10.
    /// </summary>
    [TestFixture]
    public class CombatHUDViewTests
    {
        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
        }

        private GameObject _hudGO;
        private CombatHUDView _hud;
        private TurnQueueView _turnQueue;
        private ComboIndicatorView _comboIndicator;
        private EnemyPanelView _enemyPanel;
        private ActionButtonsView _actionButtons;
        private DiceZoneView _diceZone;
        private RerollCountView _rerollCount;
        private FloatingDamageSpawner _floatingDamage;
        private Guid _playerGuid;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService { PlayerGuid = _playerGuid });

            _hudGO = new GameObject("CombatHUDView");
            _hudGO.SetActive(false);
            _hud = _hudGO.AddComponent<CombatHUDView>();

            _turnQueue = AttachChild<TurnQueueView>("TurnQueue", _hudGO);
            _comboIndicator = AttachChild<ComboIndicatorView>("ComboIndicator", _hudGO);
            _enemyPanel = AttachChild<EnemyPanelView>("EnemyPanel", _hudGO);
            _actionButtons = AttachChild<ActionButtonsView>("ActionButtons", _hudGO);
            _diceZone = AttachChild<DiceZoneView>("DiceZone", _hudGO);
            _rerollCount = AttachChild<RerollCountView>("RerollCount", _hudGO);
            _floatingDamage = AttachChild<FloatingDamageSpawner>("FloatingDamage", _hudGO);

            AssignPrivate(_hud, "_turnQueue", _turnQueue);
            AssignPrivate(_hud, "_comboIndicator", _comboIndicator);
            AssignPrivate(_hud, "_enemyPanel", _enemyPanel);
            AssignPrivate(_hud, "_actionButtons", _actionButtons);
            AssignPrivate(_hud, "_diceZone", _diceZone);
            AssignPrivate(_hud, "_rerollCount", _rerollCount);
            AssignPrivate(_hud, "_floatingDamage", _floatingDamage);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<HealthChangedPayload>.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.RemoveService<IPlayerService>();
            if (_hudGO != null) UnityEngine.Object.DestroyImmediate(_hudGO);
        }

        [Test]
        public void BindAll_PropagatesToSubViews_SubViewsBoundFlagSet()
        {
            _hud.BindAll(_playerGuid);

            bool bound = (bool)GetPrivateValue(_hud, "_subViewsBound");
            Assert.IsTrue(bound, "_subViewsBound debe quedar true tras BindAll.");
        }

        [Test]
        public void UnbindAll_ClearsSubViewsBoundFlag()
        {
            _hud.BindAll(_playerGuid);
            _hud.UnbindAll();

            bool bound = (bool)GetPrivateValue(_hud, "_subViewsBound");
            Assert.IsFalse(bound, "_subViewsBound debe ser false tras UnbindAll.");
        }

        [Test]
        public void SetEnemyTarget_PassesGuidToEnemyPanel()
        {
            _hud.BindAll(_playerGuid);
            var enemyGuid = Guid.NewGuid();

            _hud.SetEnemyTarget(enemyGuid);

            Assert.AreEqual(enemyGuid, _enemyPanel.CurrentTarget,
                "EnemyPanelView.CurrentTarget debe reflejar el target seteado por el HUD.");
        }

        [Test]
        public void BindAll_IsIdempotent_UnbindAllCleansOnce()
        {
            _hud.BindAll(_playerGuid);
            _hud.BindAll(_playerGuid); // re-bind — sub-views internamente hacen Unbind+resubscribe

            _hud.UnbindAll();
            bool bound = (bool)GetPrivateValue(_hud, "_subViewsBound");
            Assert.IsFalse(bound);
        }

        [Test]
        public void InvokeDelegate_WithoutWiring_DoesNotThrow()
        {
            _hud.BindAll(_playerGuid);
            // OnAttackRequested queda null — simular click via reflection del metodo privado.
            var method = typeof(CombatHUDView).GetMethod("InvokeAttackRequested",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            Assert.DoesNotThrow(() => method.Invoke(_hud, null),
                "Click sin delegate cableado no debe crashear (log warning + early return).");
        }

        // ---------------- helpers ----------------

        private static T AttachChild<T>(string name, GameObject parent) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static object GetPrivateValue(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            return field.GetValue(target);
        }
    }
}
