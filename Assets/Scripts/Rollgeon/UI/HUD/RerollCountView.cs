using System;
using Patterns;
using Rollgeon.Dice;
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
    /// Sub-view que muestra "{used}/{cap}" rerolls + un boton de roll cuyo label indica
    /// el costo del proximo tiro ("Reroll  -1 [icono energia]" cuando se paga).
    /// Consume <see cref="IRerollBudgetService"/> via <see cref="Patterns.ServiceLocator"/>
    /// y escucha <see cref="EventName.OnDiceRolled"/> / <see cref="EventName.OnRollResolved"/>
    /// + el evento tipado <see cref="IRerollBudgetService.OnRerollStarted"/>.
    /// Plan §3.7.
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

        /// <summary>Costo compacto del roll pago. Sin palabras — no hay nada que traducir.</summary>
        private const string PaidCostBadge = "-1 {ENERGY}";

        [Title("Reroll Count — Widgets")]
        [SerializeField]
        [Tooltip("Label '{used}/{cap}'. Fallback '-/-' si no hay IRerollBudgetService.")]
        private TextMeshProUGUI _countLabel;

        [SerializeField]
        [Tooltip("Boton de roll/reroll. Mirrea ActionButtonsView._energyRerollButton " +
                 "pero es una afordance separada pegada a la dice zone.")]
        private Button _extraRollButton;

        [Title("Reroll Count — Config")]
        [SerializeField]
        [Tooltip("Formato del label. Default '{0}/{1}'.")]
        private string _countFormat = "{0}/{1}";

        [SerializeField]
        [Tooltip("Texto fallback cuando no hay IRerollBudgetService.")]
        private string _fallbackText = "-/-";

        [SerializeField]
        [Tooltip("Label opcional de costo del proximo reroll (ej. 'Free', '-1 {ENERGY}'). Null = skip.")]
        private TextMeshProUGUI _costLabel;

        [SerializeField]
        [Tooltip("Label opcional del boton — cambia entre 'Roll', 'Reroll (Free)' y " +
                 "'Reroll  -1 {ENERGY}' segun el estado del budget. Null = skip.")]
        private TextMeshProUGUI _buttonLabel;

        [Title("Reroll Count — Button Texts")]
        [SerializeField]
        [Tooltip("Texto del boton para el primer roll (antes de gastar ningun roll).")]
        private string _firstRollText = "Roll";

        [SerializeField]
        [Tooltip("Texto del boton para un reroll gratis.")]
        private string _rerollFreeText = "Reroll (Free)";

        [SerializeField]
        [Tooltip("Texto del boton para un reroll pago con energia. {ENERGY} se expande al " +
                 "icono del atlas. Fallback si la tabla UI no tiene la key.")]
        private string _rerollPaidText = "Reroll  -1 {ENERGY}";

        [Title("Reroll Count — Events")]
        [SerializeField]
        private UnityEvent _onExtraRollPressed = new UnityEvent();

        public UnityEvent OnExtraRollPressed => _onExtraRollPressed;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private IRerollBudgetService _budget;
        private Action<RerollStartedPayload> _onRerollStartedTyped;
        private Action<RerollBudget> _onBudgetStartedTyped;
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

        private void Awake()
        {
            if (_extraRollButton != null) _extraRollButton.onClick.AddListener(HandleExtraRollClick);
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

            if (ServiceLocator.TryGetService<IRerollBudgetService>(out _budget) && _budget != null)
            {
                _onRerollStartedTyped = HandleRerollStartedTyped;
                _budget.OnRerollStarted += _onRerollStartedTyped;

                _onBudgetStartedTyped = HandleBudgetStartedTyped;
                _budget.OnBudgetStarted += _onBudgetStartedTyped;
            }
            else
            {
                Debug.Log(LogPrefix + "IRerollBudgetService no registrado — label en fallback.", this);
                _budget = null;
            }

            // Suscripción al ActionRollService: OnDiceRolled se dispara mientras la phase
            // todavía es Rolling — necesitamos refrescar también cuando entra a
            // AwaitingRerollDecision para que el botón se habilite si hay energía.
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

            // Los textos de costo se setean por codigo, asi que el package no los repinta
            // solo al cambiar de idioma — hay que re-correr el render a mano.
            _onLanguageChanged = RefreshTexts;
            LocalizationRefresh.Subscribe(_onLanguageChanged);

            _bound = true;
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshTexts();
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);

            if (_budget != null && _onRerollStartedTyped != null)
            {
                _budget.OnRerollStarted -= _onRerollStartedTyped;
                _onRerollStartedTyped = null;
            }
            if (_budget != null && _onBudgetStartedTyped != null)
            {
                _budget.OnBudgetStarted -= _onBudgetStartedTyped;
                _onBudgetStartedTyped = null;
            }
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
            _budget = null;
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

        /// <summary>Pinta el contador "{used}/{cap}" manualmente. Publico para tooling / tests.</summary>
        public void SetCount(int used, int cap)
        {
            if (_countLabel == null) return;
            _countLabel.text = string.Format(_countFormat, used, cap);
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

            // Si hay un ActionRoll activo (Heal / Forzar Puerta), Reroll = pagar 1 energía
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
        // budget/energía/holds que aplica RefreshButtonInteractable).
        private void OnHotkeyRoll(InputAction.CallbackContext _)
        {
            if (_extraRollButton != null && _extraRollButton.interactable)
                _extraRollButton.onClick.Invoke();
        }

        private void HandleDiceRolled(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            EnsureBudgetSubscribed();
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshTexts();
        }

        // Si Bind() corrió antes de que el bootstrap registrara IRerollBudgetService,
        // las suscripciones a OnBudgetStarted/OnRerollStarted nunca se hicieron y el
        // botón nunca recibe los eventos para repintarse. Resuscribimos lazy desde
        // cualquier handler que sepa que hubo actividad (un dado fue rolled).
        private void EnsureBudgetSubscribed()
        {
            if (_budget != null) return;
            if (!ServiceLocator.TryGetService<IRerollBudgetService>(out _budget) || _budget == null) return;

            if (_onRerollStartedTyped == null)
            {
                _onRerollStartedTyped = HandleRerollStartedTyped;
                _budget.OnRerollStarted += _onRerollStartedTyped;
            }
            if (_onBudgetStartedTyped == null)
            {
                _onBudgetStartedTyped = HandleBudgetStartedTyped;
                _budget.OnBudgetStarted += _onBudgetStartedTyped;
            }
        }

        private void HandleRollResolved(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            SetFallback();
            RefreshCostLabel();
            if (_buttonLabel != null) _buttonLabel.text = _firstRollText;
            if (_extraRollButton != null) _extraRollButton.interactable = false;
        }

        private void HandleRerollStartedTyped(RerollStartedPayload payload)
        {
            if (payload.PlayerGuid != _playerGuid) return;
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshTexts();
        }

        private void HandleBudgetStartedTyped(RerollBudget budget)
        {
            // Repinta el contador apenas se abre el budget (al seleccionar accion),
            // sin esperar al primer OnDiceRolled. Hace que el "3/3" sea visible
            // desde la seleccion como pide el flow manual de roll.
            RefreshLabel();
            RefreshButtonInteractable();
            RefreshTexts();
        }

        // ======================================================================
        // Internals
        // ======================================================================

        private void RefreshLabel()
        {
            if (_budget == null || _budget.Current == null || _budget.Current.Action == null)
            {
                SetFallback();
                return;
            }
            int total = _budget.Current.Action.FreeRollCount;
            int remaining = _budget.Current.FreeRollsRemaining;
            if (remaining < 0) remaining = 0;
            SetCount(remaining, total);
        }

        /// <summary>
        /// Repinta todo lo que depende del idioma y del costo del proximo roll. Van juntos
        /// porque las dos piezas (texto del boton y costo) leen el mismo
        /// <c>QueryExtraRoll</c> — separarlas invitaba a que una quedara desfasada.
        /// </summary>
        private void RefreshTexts()
        {
            RefreshCostLabel();
            RefreshButtonText();
        }

        private void RefreshCostLabel()
        {
            if (_costLabel == null) return;
            if (_budget == null)
            {
                _costLabel.text = "";
                return;
            }
            var query = _budget.QueryExtraRoll(_playerGuid);
            if (query.IsFreeRoll)
                _costLabel.text = "Free";
            else if (query.CostsEnergy)
                _costLabel.text = IconSpriteTags.ReplacePlaceholders(PaidCostBadge);
            else
                _costLabel.text = "";
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

            // Si hay un ActionRoll activo (Heal / Forzar Puerta), el budget de Generala
            // no aplica — el gating es por energía vía CanAffordReroll del service.
            // CanAffordReroll ya incluye el guard de "todos holdeados" (BUG-014).
            if (ServiceLocator.TryGetService<Rollgeon.ActionRolls.IActionRollService>(out var rs)
                && rs != null && rs.IsActive)
            {
                _extraRollButton.interactable = rs.CanAffordReroll && !IsGrabRerollMode();
                return;
            }

            if (_budget == null)
            {
                _extraRollButton.interactable = false;
                return;
            }

            // CNF-008 (grab-to-reroll): en modo 2D el botón solo dispara el PRIMER
            // roll — los rerolls se hacen agarrando los dados asentados y arrojándolos.
            if (!IsFirstRollPending() && IsGrabRerollMode())
            {
                _extraRollButton.interactable = false;
                return;
            }

            var query = _budget.QueryExtraRoll(_playerGuid);
            // Después del primer roll, si ningún dado va a volar no hay nada que
            // re-tirar — deshabilitar para no quemar free rolls / energía en una
            // tirada idéntica. Qué dado vuela depende del modo (RerollSelectionPrefs):
            // invertido (Balatro) ⇒ vuelan los seleccionados: sin selección, nada;
            // clásico ⇒ vuelan los NO seleccionados: con todo lockeado, nada.
            // El primer roll queda exento (todavía no hay dados que seleccionar).
            bool nothingToReroll = Rollgeon.Dice.RerollSelectionPrefs.KeepSelected
                ? ResolveDiceZone()?.AllDiceHeld() == true
                : ResolveDiceZone()?.AnyDieHeld() != true;
            if (query.IsAvailable && !IsFirstRollPending() && nothingToReroll)
            {
                _extraRollButton.interactable = false;
                return;
            }
            _extraRollButton.interactable = query.IsAvailable;
        }

        // Mismo criterio de "primer roll" que RefreshButtonText.
        private bool IsFirstRollPending()
            => _budget != null && _budget.Current != null && _budget.Current.Action != null
               && _budget.Current.FreeRollsRemaining == _budget.Current.Action.FreeRollCount
               && _budget.Current.PaidRollsUsed == 0;

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

            // Si el budget arranco pero no se rolo nada → primer roll
            // (mismo criterio que CombatHUDView.InvokeRollOrReroll para dispatch).
            if (_budget != null && _budget.Current != null && _budget.Current.Action != null
                && _budget.Current.FreeRollsRemaining == _budget.Current.Action.FreeRollCount
                && _budget.Current.PaidRollsUsed == 0)
            {
                _buttonLabel.text = _firstRollText;
                return;
            }

            if (_budget == null)
            {
                _buttonLabel.text = _firstRollText;
                return;
            }

            // Sino, es reroll — gratis si quedan free, paid si toca energia.
            var query = _budget.QueryExtraRoll(_playerGuid);
            _buttonLabel.text = query.CostsEnergy && !query.IsFreeRoll
                ? Localized(UiTextKeys.RerollPaid, _rerollPaidText)
                : _rerollFreeText;
        }
    }
}
