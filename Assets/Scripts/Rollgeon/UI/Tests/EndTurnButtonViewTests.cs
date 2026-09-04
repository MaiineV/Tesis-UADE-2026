using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    [TestFixture]
    public class EndTurnButtonViewTests
    {
        private readonly System.Collections.Generic.List<UnityEngine.Object> _spriteCleanup =
            new System.Collections.Generic.List<UnityEngine.Object>();

        private GameObject _go;
        private EndTurnButtonView _view;
        private Button _button;
        private Guid _playerGuid;
        private Image _buttonImage;
        private Sprite _endTurnSprite;
        private Sprite _confirmSprite;
        private Sprite _passSprite;

        [SetUp]
        public void Setup()
        {
            _playerGuid = Guid.NewGuid();

            _go = new GameObject("EndTurnButton");
            _view = _go.AddComponent<EndTurnButtonView>();

            _button = CreateButton("EndTurnBtn", _go);
            AssignPrivate(_view, "_endTurnButton", _button);

            var awake = typeof(EndTurnButtonView).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(_view, null);
        }

        [TearDown]
        public void Teardown()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            // El view se suscribe al toggle de holds (re-gateo del Confirm) — sin el
            // Clear, un test que no desbindea dejaría el handler colgado para otros
            // fixtures que disparen el payload.
            TypedEvent<ComboMatchedPayload>.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            foreach (var o in _spriteCleanup)
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spriteCleanup.Clear();
        }

        [Test]
        public void Bind_DisablesButton_Initially()
        {
            _view.Bind(_playerGuid);
            Assert.IsFalse(_button.interactable, "EndTurn inicia disabled.");
        }

        [Test]
        public void OnTurnStarted_Player_EnablesButton()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_button.interactable, "EndTurn enabled en turno del player.");
        }

        [Test]
        public void OnTurnStarted_OtherEntity_KeepsDisabled()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());
            Assert.IsFalse(_button.interactable, "EndTurn ignora otros guids.");
        }

        [Test]
        public void OnTurnFinished_Player_DisablesButton()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_button.interactable);

            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);
            Assert.IsFalse(_button.interactable, "EndTurn disabled al terminar turno.");
        }

        [Test]
        public void OnDiceRolled_Player_DisablesButton()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_button.interactable);

            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsFalse(_button.interactable, "EndTurn disabled durante behavior.");
        }

        [Test]
        public void OnRollResolved_Player_EnablesButton()
        {
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.IsFalse(_button.interactable);

            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);
            Assert.IsTrue(_button.interactable, "EndTurn re-enabled post confirm.");
        }

        [Test]
        public void Unbind_RemovesSubscriptions()
        {
            _view.Bind(_playerGuid);
            _view.Unbind();

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsFalse(_button.interactable, "Tras Unbind, eventos no tienen efecto.");
        }

        [Test]
        public void DoubleBindIsIdempotent()
        {
            _view.Bind(_playerGuid);
            _view.Bind(_playerGuid);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsTrue(_button.interactable, "Tras doble Bind, un solo handler activo.");

            _view.Unbind();
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsFalse(_button.interactable,
                "Tras Unbind del doble Bind, no quedan handlers colgados.");
        }

        [Test]
        public void OnDisable_Unbinds()
        {
            _view.Bind(_playerGuid);
            var onDisable = typeof(EndTurnButtonView).GetMethod("OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onDisable, "OnDisable method not found.");
            onDisable.Invoke(_view, null);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            Assert.IsFalse(_button.interactable, "OnDisable desuscribe.");
        }

        [Test]
        public void EndTurnButton_Click_InvokesEvent()
        {
            bool fired = false;
            _view.OnEndTurnPressed.AddListener(() => fired = true);
            _button.onClick.Invoke();
            Assert.IsTrue(fired, "OnEndTurnPressed debe dispararse al clickear EndTurn.");
        }

        // ==================================================================
        // Botón contextual — modos EndTurn / Confirm / Pass
        // (hereda la cobertura de gating que tenía el Confirm de
        // PlayerActionButtonsView)
        // ==================================================================

        [Test]
        public void ChainStarted_EntersConfirmMode_DisabledWithoutRoll()
        {
            // Arrange
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode,
                "Con un chain en curso el botón está en modo Confirm.");
            Assert.IsFalse(_button.interactable,
                "Sin tirada revelada no hay nada que confirmar.");
        }

        [Test]
        public void DiceRolledWithHeldDie_EnablesConfirm()
        {
            // Arrange
            WireDiceZoneWithHolds(new[] { true });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode);
            Assert.IsTrue(_button.interactable,
                "Con dados rolleados y al menos un hold, Confirm se habilita.");
        }

        [Test]
        public void RolledWithoutHolds_KeepsConfirmDisabled()
        {
            // Arrange — sin holds no hay combo posible; el Confirm engañaría al jugador.
            WireDiceZoneWithHolds(new[] { false, false });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode);
            Assert.IsFalse(_button.interactable,
                "Confirm disabled tras OnDiceRolled si no hay dados holdeados.");
        }

        [Test]
        public void ClickInConfirmMode_FiresConfirm_NotEndTurn_AndLocksButton()
        {
            // Arrange
            WireDiceZoneWithHolds(new[] { true });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            bool confirmFired = false, endTurnFired = false;
            _view.OnConfirmPressed.AddListener(() => confirmFired = true);
            _view.OnEndTurnPressed.AddListener(() => endTurnFired = true);

            // Act
            _button.onClick.Invoke();

            // Assert
            Assert.IsTrue(confirmFired, "En modo Confirm el click dispara OnConfirmPressed.");
            Assert.IsFalse(endTurnFired, "En modo Confirm el click NO debe pasar turno.");
            Assert.IsFalse(_button.interactable,
                "BUG-018: en chain el click apaga el botón hasta el próximo refresh con estado fresco.");
        }

        [Test]
        public void RollResolvedDuringChain_KeepsConfirmMode()
        {
            // Regresión heredada del chain: OnRollResolved entre fases NO cierra la
            // acción — el modo Confirm se mantiene hasta OnChainCompleted.
            // Arrange
            WireDiceZoneWithHolds(new[] { true });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode,
                "Durante un chain, OnRollResolved entre fases no vuelve a End Turn.");
            Assert.IsTrue(_button.interactable,
                "El confirm sigue habilitado con el latch del chain activo.");
        }

        [Test]
        public void RollResolvedOutsideChain_ReturnsToEndTurnMode()
        {
            // Arrange
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode, "Precondición: modo Confirm.");

            // Act
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.EndTurn, _view.CurrentMode,
                "Fuera de chain, resolver la tirada devuelve el botón a End Turn.");
            Assert.IsTrue(_button.interactable, "En turno propio End Turn queda habilitado.");
        }

        [Test]
        public void ChainCompleted_ReturnsToEndTurnMode()
        {
            // Arrange
            WireDiceZoneWithHolds(new[] { true });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnChainCompleted, _playerGuid, 2, 2, false);

            // Assert
            Assert.AreEqual(TurnButtonMode.EndTurn, _view.CurrentMode,
                "Al completarse el chain el botón vuelve a End Turn.");
            Assert.IsTrue(_button.interactable);
        }

        [Test]
        public void PaidRollPending_EntersPassMode_AndClickFiresPass()
        {
            // Arrange — fase de chain con entrada paga (prompt 'X Roll -1⚡' visible).
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            bool passFired = false, endTurnFired = false, confirmFired = false;
            _view.OnPassPressed.AddListener(() => passFired = true);
            _view.OnEndTurnPressed.AddListener(() => endTurnFired = true);
            _view.OnConfirmPressed.AddListener(() => confirmFired = true);

            // Act
            _view.SetChainPaidRollPending(true);

            // Assert
            Assert.AreEqual(TurnButtonMode.Pass, _view.CurrentMode,
                "Con un roll pago pendiente y sin tirada, el botón ofrece Pass.");
            Assert.IsTrue(_button.interactable, "Pass es la salida sin costo — habilitado.");

            _button.onClick.Invoke();
            Assert.IsTrue(passFired, "En modo Pass el click dispara OnPassPressed.");
            Assert.IsFalse(endTurnFired, "Pass NO debe cerrar el turno.");
            Assert.IsFalse(confirmFired, "Pass NO debe confirmar.");
        }

        [Test]
        public void PaidRollCleared_ReturnsToConfirmMode()
        {
            // Arrange — el jugador pagó y tiró: el prompt se esconde y llega el reveal.
            WireDiceZoneWithHolds(new[] { true });
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);
            _view.SetChainPaidRollPending(true);
            Assert.AreEqual(TurnButtonMode.Pass, _view.CurrentMode, "Precondición: modo Pass.");

            // Act
            _view.SetChainPaidRollPending(false);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode,
                "Pagado el roll, el botón vuelve a Confirm para la tirada nueva.");
            Assert.IsTrue(_button.interactable);
        }

        // ==================================================================
        // Ventana de decision del item activo (aceptar / re-tirar)
        // ==================================================================

        [Test]
        public void ActiveItemPending_EntersConfirmMode_AndClickAccepts()
        {
            // Arrange — con la tirada del activo esperando decision, el boton pasa a
            // Confirm y el click acepta la cara vigente (nunca dispara el flow de combate).
            var fake = new FakeActiveItemActivation();
            ServiceLocator.AddService<Rollgeon.Items.Active.IActiveItemActivationService>(fake);
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            bool confirmFired = false, endTurnFired = false;
            _view.OnConfirmPressed.AddListener(() => confirmFired = true);
            _view.OnEndTurnPressed.AddListener(() => endTurnFired = true);

            // Act
            fake.RaisePending();

            // Assert
            Assert.AreEqual(TurnButtonMode.Confirm, _view.CurrentMode,
                "Con la tirada del activo pendiente el boton ofrece Confirm.");
            Assert.IsTrue(_button.interactable, "Aceptar no cuesta nada — habilitado.");

            _button.onClick.Invoke();
            Assert.AreEqual(1, fake.AcceptCalls, "el click acepta la cara vigente");
            Assert.IsFalse(confirmFired, "no debe disparar el confirm del flow de combate");
            Assert.IsFalse(endTurnFired, "no debe cerrar el turno");
            Assert.AreEqual(TurnButtonMode.EndTurn, _view.CurrentMode,
                "resuelta la tirada, el boton vuelve a End Turn");
        }

        private sealed class FakeActiveItemActivation
            : Rollgeon.Items.Active.IActiveItemActivationService
        {
            public int AcceptCalls { get; private set; }
            public bool IsAwaitingDecision { get; private set; }

            public Rollgeon.Items.Active.ActiveItemPendingRoll? Pending
                => IsAwaitingDecision
                    ? new Rollgeon.Items.Active.ActiveItemPendingRoll(null, 1, 0)
                    : (Rollgeon.Items.Active.ActiveItemPendingRoll?)null;

            public bool CanRequestReroll => false;
            public bool IsSelecting => false;
            public bool IsAwaitingChoice => false;

#pragma warning disable 67 // el fake nunca dispara la fase de eleccion
            public event Action OnChoicePending;
            public event Action OnChoiceResolved;
#pragma warning restore 67

            public Rollgeon.Items.Active.ActiveItemBlock CanActivate()
                => Rollgeon.Items.Active.ActiveItemBlock.None;

            public bool BeginActivation() => false;
            public void CancelActivation() { }

            public Rollgeon.Items.Active.ActiveItemPendingRoll? Confirm(
                Rollgeon.Effects.Selection.TargetSelectionResult selection) => null;

            public bool RequestReroll() => false;

            public Rollgeon.Items.Active.ActiveItemActivationResult? AcceptRoll()
            {
                AcceptCalls++;
                IsAwaitingDecision = false;
                var result = new Rollgeon.Items.Active.ActiveItemActivationResult(
                    null, 1, Rollgeon.Items.Active.ActiveItemBand.Negative, true, 1);
                OnResolved?.Invoke(result);
                return result;
            }

            public void RaisePending()
            {
                IsAwaitingDecision = true;
                OnRollPending?.Invoke(new Rollgeon.Items.Active.ActiveItemPendingRoll(null, 1, 0));
            }

            public event Action<Rollgeon.Items.Active.ActiveItemPendingRoll> OnRollPending;
            public event Action<Rollgeon.Items.Active.ActiveItemActivationResult> OnResolved;
#pragma warning disable CS0067
            public event Action OnSelectionStarted;
            public event Action OnSelectionCancelled;
#pragma warning restore CS0067
        }

        // ==================================================================
        // Sprite contextual por modo (FinishTurnButton / Confirm2)
        // ==================================================================

        [Test]
        public void test_turn_button_paints_the_end_turn_sprite_on_bind()
        {
            // Arrange
            WireModeSprites();

            // Act
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Assert
            Assert.AreSame(_endTurnSprite, _buttonImage.sprite,
                "En modo End Turn el botón usa el arte de FinishTurnButton.");
        }

        [Test]
        public void test_turn_button_paints_the_confirm_sprite_while_a_dice_flow_is_active()
        {
            // Arrange
            WireModeSprites();
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            // Act
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);

            // Assert
            Assert.AreSame(_confirmSprite, _buttonImage.sprite,
                "Con una tirada en curso el botón usa el arte de Confirm.");
        }

        [Test]
        public void test_turn_button_paints_the_pass_sprite_while_a_paid_roll_is_pending()
        {
            // Arrange
            WireModeSprites();
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnChainStarted, _playerGuid);

            // Act
            _view.SetChainPaidRollPending(true);

            // Assert
            Assert.AreSame(_passSprite, _buttonImage.sprite,
                "Con un roll pago pendiente el botón usa el arte de Pass.");
        }

        [Test]
        public void test_turn_button_returns_to_the_end_turn_sprite_after_the_roll_resolves()
        {
            // Arrange
            WireModeSprites();
            _view.Bind(_playerGuid);
            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid);
            Assume.That(_buttonImage.sprite, Is.SameAs(_confirmSprite),
                "Precondición: modo Confirm con su arte.");

            // Act
            EventManager.Trigger(EventName.OnRollResolved, _playerGuid);

            // Assert
            Assert.AreSame(_endTurnSprite, _buttonImage.sprite,
                "Resuelta la tirada fuera de chain, el botón vuelve al arte de End Turn.");
        }

        /// <summary>
        /// Cablea el swap de sprites sobre el botón del SetUp. Solo lo usan los tests
        /// de sprite — el resto del fixture no necesita Image ni sets.
        /// </summary>
        private void WireModeSprites()
        {
            _buttonImage = _button.gameObject.AddComponent<Image>();
            var swap = _button.gameObject.AddComponent<HudButtonSpriteSwap>();

            _endTurnSprite = MakeSprite();
            _confirmSprite = MakeSprite();
            _passSprite = MakeSprite();
            AssignPrivate(_view, "_buttonSprites", swap);
            AssignPrivate(_view, "_endTurnSprites", new ButtonSpriteSet(_endTurnSprite, null));
            AssignPrivate(_view, "_confirmSprites", new ButtonSpriteSet(_confirmSprite, null));
            AssignPrivate(_view, "_passSprites", new ButtonSpriteSet(_passSprite, null));
        }

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(2, 2);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _spriteCleanup.Add(tex);
            _spriteCleanup.Add(sprite);
            return sprite;
        }

        private static Button CreateButton(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<Button>();
        }

        /// <summary>
        /// DiceZoneView con holds simulados, cableado ANTES del Bind (así el view no
        /// intenta el auto-resolve por escena). Sin Bind del zone: solo hace falta
        /// que GetHeldStates devuelva los holds.
        /// </summary>
        private void WireDiceZoneWithHolds(bool[] holds)
        {
            var go = new GameObject("DiceZone");
            go.transform.SetParent(_go.transform, false);
            var zone = go.AddComponent<DiceZoneView>();
            AssignPrivate(zone, "_heldStates", holds);
            AssignPrivate(_view, "_diceZone", zone);
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
