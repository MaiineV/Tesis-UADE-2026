using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Screens;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Smoke test del <see cref="CombatHUDView"/>. Verifica que <c>BindAll</c>
    /// propaga a cada sub-view y que los delegates de acciones no crashean
    /// cuando no estan cableados. Plan §3.10.
    /// </summary>
    [TestFixture]
    public class CombatHUDViewTests
    {
#pragma warning disable 67
        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId { get; set; }
            public ClassHeroSO CurrentHero { get; set; }
            public Rollgeon.Dice.DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }
#pragma warning restore 67

        private GameObject _hudGO;
        private CombatHUDView _hud;
        private TurnQueueView _turnQueue;
        private DiceZoneView _diceZone;
        private RerollCountView _rerollCount;
        private FloatingDamageSpawner _floatingDamage;
        private PlayerActionButtonsView _playerActionButtons;
        private EndTurnButtonView _endTurnButtonView;
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
            _diceZone = AttachChild<DiceZoneView>("DiceZone", _hudGO);
            _rerollCount = AttachChild<RerollCountView>("RerollCount", _hudGO);
            _floatingDamage = AttachChild<FloatingDamageSpawner>("FloatingDamage", _hudGO);
            _playerActionButtons = AttachChild<PlayerActionButtonsView>("PlayerActionButtons", _hudGO);
            _endTurnButtonView = AttachChild<EndTurnButtonView>("EndTurnButton", _hudGO);

            AssignPrivate(_hud, "_turnQueue", _turnQueue);
            AssignPrivate(_hud, "_diceZone", _diceZone);
            AssignPrivate(_hud, "_rerollCount", _rerollCount);
            AssignPrivate(_hud, "_floatingDamage", _floatingDamage);
            AssignPrivate(_hud, "_playerActionButtons", _playerActionButtons);
            AssignPrivate(_hud, "_endTurnButtonView", _endTurnButtonView);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<ComboMatchedPayload>.Clear();
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
            // OnEndTurnRequested queda null — simular click via reflection del metodo privado.
            var method = typeof(CombatHUDView).GetMethod("InvokeEndTurnRequested",
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
