using System;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Input;
using Rollgeon.Localization;
using Rollgeon.UI.Utility;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view pegada a la dice zone: espeja el Pool de Rolls ("{current}/{max}")
    /// y expone el boton de Roll/Reroll. Cada tirada cuesta 1 roll del pool
    /// (Feature#0050) — ya no hay distincion free/paid ni budget por accion.
    /// Consume <see cref="IRollPoolService"/> via <see cref="Patterns.ServiceLocator"/>
    /// y escucha <see cref="EventName.OnPlayerRollsChanged"/> +
    /// <see cref="EventName.OnDiceRolled"/> / <see cref="EventName.OnRollResolved"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Fallback</b>: si el servicio no esta registrado al Bind, el label muestra
    /// <c>"-/-"</c> y el boton queda deshabilitado.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Reroll Count View")]
    public class RerollCountView : MonoBehaviour
    {
        private const string LogPrefix = "[RerollCountView] ";

        [Title("Reroll Count — Widgets")]
        [SerializeField]
        [Tooltip("Label '{current}/{max}' del pool. Fallback '-/-' si no hay IRollPoolService.")]
        private TextMeshProUGUI _countLabel;

        [SerializeField]
        [Tooltip("Boton de roll/reroll pegado a la dice zone.")]
        private Button _extraRollButton;

        [Title("Reroll Count — Config")]
        [SerializeField]
        [Tooltip("Formato del label. Default '{0}/{1}'.")]
        private string _countFormat = "{0}/{1}";

        [SerializeField]
        [Tooltip("Texto fallback cuando no hay IRollPoolService.")]
        private string _fallbackText = "-/-";

        [SerializeField]
        [Tooltip("Label opcional de costo del proximo roll. Con el pool todas las tiradas " +
                 "cuestan 1 roll — queda vacio. Null = skip.")]
        private TextMeshProUGUI _costLabel;

        [SerializeField]
        [Tooltip("Label opcional del boton — cambia entre 'Roll' y 'Reroll'. Null = skip.")]
        private TextMeshProUGUI _buttonLabel;

        [Title("Reroll Count — Button Texts")]
        [SerializeField]
        [Tooltip("Texto del boton para el primer roll de la accion.")]
        private string _firstRollText = "Roll";

        [SerializeField]
        [Tooltip("Texto del boton para los rerolls (1 roll del pool cada uno).")]
        private string _rerollFreeText = "Reroll";

        [Title("Reroll Count — Button Sprites")]
        [SerializeField]
        [Tooltip("Swap de sprites del botón por estado. Null = sin swap " +
                 "(el botón conserva el estilo de texto).")]
        private HudButtonSpriteSwap _buttonSprites;

        [SerializeField]
        [Tooltip("Sprites del boton Roll/Reroll (Roll2: _1 normal, _0 hover).")]
        private ButtonSpriteSet _freeRollSprites;

        [SerializeField, Tooltip("Velocidad del hundimiento/regreso del botón mientras no se " +
                 "pueda usar — mismo feel que la ficha usada de ActionButton. " +
                 "Px de pantalla por segundo; <= 0 = instantáneo.")]
        private float _sinkSpeed = 900f;

        [Title("Reroll Count — Events")]
        [SerializeField]
        private UnityEvent _onExtraRollPressed = new UnityEvent();

        public UnityEvent OnExtraRollPressed => _onExtraRollPressed;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        // True desde el primer OnDiceRolled de la accion hasta OnRollResolved —
        // decide si el boton dice "Roll" o "Reroll" (antes lo decidia el budget).
        private bool _hasRolledThisAction;

        private Rollgeon.ActionRolls.IActionRollService _actionRoll;
        private Action<Rollgeon.ActionRolls.ActionRollPhase> _onActionRollPhase;
        // BUG-014: cache de DiceZoneView para gatear el botón si todos los dados
        // están holdeados — se resuelve lazy en el primer refresh.
        private DiceZoneView _diceZone;
        private bool _diceAnimHooked;
        private Action<ComboMatchedPayload> _onComboMatched;
        private IGameplayHotkeyService _hotkeys;
        private Action _onLanguageChanged;

        /// <summary>RectTransform del botón Roll/Reroll — anchor del overlay del tutorial.</summary>
        public bool TryGetRollButtonRect(out RectTransform rect)
        {
            rect = _extraRollButton != null ? _extraRollButton.transform as RectTransform : null;
            return rect != null;
        }

        /// <summary>
        /// True mientras la accion en curso todavia no tiro sus dados — el boton
        /// dispara el primer Roll. Publico: CombatHUDView lo usa para el dispatch
        /// Roll vs Reroll (antes lo decidia el estado del budget).
        /// </summary>
        public bool IsFirstRollPending => !_hasRolledThisAction;

        private void Awake()
        {
            if (_extraRollButton != null) _extraRollButton.onClick.AddListener(HandleExtraRollClick);
            // La ficha se hunde a media asta SIEMPRE que el botón no se pueda usar
            // (sin rolls, dados girando, entre acciones) — antes solo se hundía
            // sin rerolls y los demás estados se leían como un botón muerto.
            HudButtonSink.Attach(_extraRollButton, _sinkSpeed);
        }

        private void OnDestroy()
        {
            if (_extraRollButton != null) _extraRollButton.onClick.RemoveListener(HandleExtraRollClick);
        }

        public void Bind(Guid playerGuid)
        {
            // Idempotente para soporte multi-HUD (CombatHUD + ExplorationHUD ambos bindean
            // ahora que vive en el Canvas raíz). Skip si ya estoy bindeado al mismo guid.
            if (_bound)
            {
                if (_playerGuid == playerGuid) return;
                Unbind();
            }
            _playerGuid = playerGuid;

            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.Subscribe(EventName.OnPlayerRollsChanged, HandleRollsChanged);

            // Suscripción al ActionRollService: OnDiceRolled se dispara mientras la phase
            // todavía es Rolling — necesitamos refrescar también cuando entra a
            // AwaitingRerollDecision para que el botón se habilite si hay rolls.
            if (ServiceLocator.TryGetService<Rollgeon.ActionRolls.IActionRollService>(out _actionRoll)
                && _actionRoll != null)
            {
                _onActionRollPhase = _ => RefreshButtonInteractable();
                _actionRoll.OnPhaseChanged += _onActionRollPhase;
            }

            // ComboMatchedPayload se dispara cada vez que el user togglea un hold
            // (DiceZoneView.RunComboDetection) — refrescamos el botón para que se
            // habilite con ≥1 dado seleccionado y se apague al quedar sin selección.
            _onComboMatched = _ => RefreshButtonInteractable();
            TypedEvent<ComboMatchedPayload>.Subscribe(_onComboMatched);

            if (ServiceLocator.TryGetService<IGameplayHotkeyService>(out _hotkeys) && _hotkeys != null)
                _hotkeys.Subscribe(GameplayHotkey.Roll, OnHotkeyRoll);

            // Los textos se setean por codigo, asi que el package no los repinta
            // solo al cambiar de idioma — hay que re-correr el render a mano.
            _onLanguageChanged = RefreshTexts;
            LocalizationRefresh.Subscribe(_onLanguageChanged);

            _bound = true;
            _hasRolledThisAction = false;
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshTexts();
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.UnSubscribe(EventName.OnPlayerRollsChanged, HandleRollsChanged);

            if (_actionRoll != null && _onActionRollPhase != null)
            {
                _actionRoll.OnPhaseChanged -= _onActionRollPhase;
                _onActionRollPhase = null;
                _actionRoll = null;
            }
            if (_onComboMatched != null)
            {
                TypedEvent<ComboMatchedPayload>.Unsubscribe(_onComboMatched);
                _onComboMatched = null;
            }
            if (_hotkeys != null)
            {
                _hotkeys.Unsubscribe(GameplayHotkey.Roll, OnHotkeyRoll);
                _hotkeys = null;
            }
            if (_onLanguageChanged != null)
            {
                LocalizationRefresh.Unsubscribe(_onLanguageChanged);
                _onLanguageChanged = null;
            }
            if (_diceZone != null && _diceAnimHooked)
                _diceZone.DiceAnimationStateChanged -= RefreshButtonInteractable;
            _diceAnimHooked = false;
            _diceZone = null;
            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // ======================================================================
        // API publica
        // ======================================================================

        /// <summary>Pinta el contador "{current}/{max}" manualmente. Publico para tooling / tests.</summary>
        public void SetCount(int current, int max)
        {
            if (_countLabel == null) return;
            _countLabel.text = string.Format(_countFormat, current, max);
        }

        /// <summary>Pinta el label en fallback (servicio ausente).</summary>
        public void SetFallback()
        {
            if (_countLabel == null) return;
            _countLabel.text = _fallbackText;
        }

        // ======================================================================
        // Handlers
        // ======================================================================

        private void HandleExtraRollClick()
        {
            // Dados girando o volando (modo Classic): el resultado aún no se reveló —
            // un reroll acá pisaría la animación (backstop del gate de interactable).
            if (ResolveDiceZone()?.IsDiceAnimating == true) return;

            // Si hay un ActionRoll activo (Heal / Forzar Puerta), Reroll = pagar 1 roll
            // y rerollear via service. El service re-tira los dados SELECCIONADOS
            // (_currentHolds, seteado por DiceZoneView.ToggleHold → SetHolds) y
            // conserva el resto (reroll invertido).
            if (ServiceLocator.TryGetService<Rollgeon.ActionRolls.IActionRollService>(out var rs)
                && rs != null && rs.IsActive)
            {
                rs.RequestReroll();
                return;
            }
            _onExtraRollPressed?.Invoke();
        }

        // R = click del botón Roll/Reroll, solo si está interactable (mismo gating de
        // pool/holds que aplica RefreshButtonInteractable).
        private void OnHotkeyRoll(InputAction.CallbackContext _)
        {
            if (_extraRollButton != null && _extraRollButton.interactable)
                _extraRollButton.onClick.Invoke();
        }

        private void HandleDiceRolled(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _hasRolledThisAction = true;
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshTexts();
        }

        private void HandleRollResolved(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _hasRolledThisAction = false;
            RefreshLabel();
            RefreshCostLabel();
            if (_buttonLabel != null) _buttonLabel.text = _firstRollText;
            // Resuelto el roll el botón vuelve a "primer roll".
            if (_buttonSprites != null) _buttonSprites.Apply(_freeRollSprites);
            if (_extraRollButton != null) _extraRollButton.interactable = false;
        }

        // Schema OnPlayerRollsChanged: [Guid playerGuid, int current, int max].
        private void HandleRollsChanged(params object[] args)
        {
            if (args == null || args.Length < 3 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            RefreshLabel();
            RefreshButtonInteractable();
        }

        // ======================================================================
        // Internals
        // ======================================================================

        private void RefreshLabel()
        {
            if (!ServiceLocator.TryGetService<IRollPoolService>(out var rolls) || rolls == null)
            {
                SetFallback();
                return;
            }
            SetCount(rolls.GetCurrent(_playerGuid), rolls.GetMax(_playerGuid));
        }

        /// <summary>Repinta todo lo que depende del idioma.</summary>
        private void RefreshTexts()
        {
            RefreshCostLabel();
            RefreshButtonText();
            if (_buttonSprites != null) _buttonSprites.Apply(_freeRollSprites);
        }

        private void RefreshCostLabel()
        {
            // Con el pool todas las tiradas cuestan 1 roll — no hay costo variable
            // que anunciar. El label queda vacio (el widget sobrevive en el prefab
            // hasta el pase visual del pool).
            if (_costLabel != null) _costLabel.text = "";
        }

        /// <summary>
        /// Traduce y despues expande los <c>{ICON}</c>. En ese orden: el placeholder viaja
        /// dentro del texto traducido, asi que expandirlo antes no encontraria nada.
        /// </summary>
        private static string Localized(string key, string fallback)
            => IconSpriteTags.ReplacePlaceholders(LocalizedContent.Ui(key, fallback));

        private void RefreshButtonInteractable()
        {
            if (_extraRollButton == null) return;

            // Spin/outro en curso (modo Classic): nada de re-rollear hasta que los
            // dados terminen de revelarse / volar.
            if (ResolveDiceZone()?.IsDiceAnimating == true)
            {
                _extraRollButton.interactable = false;
                return;
            }

            // Si hay un ActionRoll activo (Heal / Forzar Puerta), el gating es por
            // pool vía CanAffordReroll del service.
            // CanAffordReroll ya incluye el guard de "todos holdeados" (BUG-014).
            if (ServiceLocator.TryGetService<Rollgeon.ActionRolls.IActionRollService>(out var rs)
                && rs != null && rs.IsActive)
            {
                _extraRollButton.interactable = rs.CanAffordReroll && !IsGrabRerollMode();
                return;
            }

            if (!ServiceLocator.TryGetService<IRollPoolService>(out var rolls) || rolls == null)
            {
                _extraRollButton.interactable = false;
                return;
            }

            // CNF-008 (grab-to-reroll): en modo 2D el botón solo dispara el PRIMER
            // roll — los rerolls se hacen agarrando los dados asentados y arrojándolos.
            if (!IsFirstRollPending && IsGrabRerollMode())
            {
                _extraRollButton.interactable = false;
                return;
            }

            bool canAfford = rolls.GetCurrent(_playerGuid) >= 1;

            // Después del primer roll, si ningún dado va a volar no hay nada que
            // re-tirar — deshabilitar para no quemar rolls en una tirada idéntica.
            // Qué dado vuela depende del modo (RerollSelectionPrefs): invertido
            // (Balatro) ⇒ vuelan los seleccionados: sin selección, nada; clásico ⇒
            // vuelan los NO seleccionados: con todo lockeado, nada. El primer roll
            // queda exento (todavía no hay dados que seleccionar).
            bool nothingToReroll = Rollgeon.Dice.RerollSelectionPrefs.KeepSelected
                ? ResolveDiceZone()?.AllDiceHeld() == true
                : ResolveDiceZone()?.AnyDieHeld() != true;
            if (canAfford && !IsFirstRollPending && nothingToReroll)
            {
                _extraRollButton.interactable = false;
                return;
            }
            _extraRollButton.interactable = canAfford;
        }

        private static bool IsGrabRerollMode()
            => ServiceLocator.TryGetService<Rollgeon.Dice.Throw.IDiceThrowService>(out var t)
               && t != null && t.Mode == Rollgeon.Dice.Throw.DiceThrowMode.TwoD;

        private DiceZoneView ResolveDiceZone()
        {
            if (_diceZone != null) return _diceZone;
            // FindAnyObjectByType es válido en runtime; el HUD tiene exactamente uno.
            // Cache local para evitar el costo del find en cada toggle.
            _diceZone = UnityEngine.Object.FindAnyObjectByType<DiceZoneView>();
            if (_diceZone != null && !_diceAnimHooked)
            {
                // El botón debe re-habilitarse solo cuando el spin/outro termina.
                _diceZone.DiceAnimationStateChanged += RefreshButtonInteractable;
                _diceAnimHooked = true;
            }
            return _diceZone;
        }

        private void RefreshButtonText()
        {
            if (_buttonLabel == null) return;
            _buttonLabel.text = IsFirstRollPending ? _firstRollText : _rerollFreeText;
        }
    }
}
