using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// View del panel que orquesta la UI de las acciones con tirada (Forzar Puerta,
    /// Curarse). Listenea los cambios de fase del <see cref="IActionRollService"/>
    /// y muestra/esconde sub-paneles segun corresponda. La wiring de prefabs +
    /// referencias se hace en el Inspector.
    /// </summary>
    /// <remarks>
    /// <b>Setup esperado.</b> Un GameObject con este componente vive bajo el
    /// ExplorationHUD (y opcionalmente CombatHUD si se va a curar en combate).
    /// Adentro debe tener cuatro sub-GameObjects (uno por sub-panel) y una fila
    /// de 5 <see cref="DiceSlotView"/> reusables. El componente se auto-bind al
    /// servicio en <c>OnEnable</c> via <see cref="ServiceLocator"/>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Action Roll Panel View")]
    public sealed class ActionRollPanelView : MonoBehaviour
    {
        [Title("Root")]
        [SerializeField, Required] private GameObject _rootPanel;

        [Title("Confirm sub-panel (visible cuando RequireConfirm == true)")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmActionLabel;
        [SerializeField] private TMP_Text _confirmThresholdLabel;
        [SerializeField] private TMP_Text _confirmCostLabel;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        [Title("Dice display (siempre visible mientras hay roll)")]
        [InfoBox("5 slots — usar el mismo prefab DiceSlotView que el HUD de combate.")]
        [SerializeField] private List<DiceSlotView> _diceSlots = new List<DiceSlotView>();

        [Tooltip("Contenedor padre de los DiceSlots. Se oculta durante el confirm dialog " +
                 "(antes de rodar) y vuelve a mostrarse al pasar a Rolling/AwaitingRerollDecision. " +
                 "Si es null, los slots quedan visibles todo el tiempo.")]
        [SerializeField] private GameObject _diceRowContainer;

        [Title("Reroll prompt (visible en AwaitingRerollDecision)")]
        [SerializeField] private GameObject _rerollPanel;
        [SerializeField] private TMP_Text _rerollSummaryLabel;
        [SerializeField] private Button _rerollAcceptButton;
        [SerializeField] private Button _rerollDeclineButton;

        [Title("Resolved overlay (visible brevemente en Resolved)")]
        [SerializeField] private GameObject _resolvedPanel;
        [SerializeField] private TMP_Text _resolvedHeader;
        [SerializeField] private TMP_Text _resolvedDetail;
        [SerializeField, MinValue(0f)]
        [Tooltip("Tiempo que el panel Resolved queda visible antes de auto-ocultarse. " +
                 "Subirlo si los jugadores no llegan a leer el resultado.")]
        private float _resolvedAutoHideSeconds = 3.5f;

        private IActionRollService _service;
        private Coroutine _autoHideRoutine;
        private CanvasGroup _rootCanvasGroup;
        // Estado de hold por dado, paralelo a _diceSlots. true = el user quiere
        // conservar ese dado en el reroll. Se resetea cada vez que se tiran nuevos
        // dados (HandleDiceRolled).
        private bool[] _holds = new bool[0];

        private void Awake()
        {
            // Si el ActionRollPanelView vive en el MISMO GameObject que _rootPanel, no
            // podemos usar SetActive(false) en Awake — eso desactiva el GameObject del
            // componente y nunca se llama OnEnable, lo que rompe la subscripcion al
            // service. En su lugar usamos CanvasGroup (alpha + blocksRaycasts).
            if (_rootPanel != null)
            {
                _rootCanvasGroup = _rootPanel.GetComponent<CanvasGroup>();
                if (_rootCanvasGroup == null)
                    _rootCanvasGroup = _rootPanel.AddComponent<CanvasGroup>();
            }

            AutoResolveDiceRowContainer();

            HideAllPanels();
            SetRootVisible(false);

            // Cada dice slot dispara OnToggled cuando el user lo clickea — eso togglea
            // su hold (igual que el HUD de combate). El user puede holdear/un-holdear
            // cualquier subset antes de Reroll/Confirm.
            _holds = new bool[_diceSlots != null ? _diceSlots.Count : 0];
            if (_diceSlots != null)
            {
                for (int i = 0; i < _diceSlots.Count; i++)
                {
                    if (_diceSlots[i] == null) continue;
                    int captured = i;
                    _diceSlots[i].OnToggled.AddListener(() => HandleSlotToggled(captured));
                }
            }

            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClick);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(OnCancelClick);
            if (_rerollAcceptButton != null) _rerollAcceptButton.onClick.AddListener(OnRerollAcceptClick);
            if (_rerollDeclineButton != null) _rerollDeclineButton.onClick.AddListener(OnRerollDeclineClick);
        }

        private void HandleSlotToggled(int index)
        {
            // Solo permitimos toggle durante la fase de decision — fuera de eso el
            // hold no tiene efecto y confunde visualmente.
            if (_service == null || _service.Phase != ActionRollPhase.AwaitingRerollDecision) return;
            if (index < 0 || index >= _diceSlots.Count) return;

            EnsureHoldsLength();
            _holds[index] = !_holds[index];
            _diceSlots[index]?.SetHeld(_holds[index]);

            // El service necesita conocer los holds para recomputar el combo y el
            // effective total — solo cuentan los dados que el user marca. Refrescar
            // el summary del panel para que el text muestre el nuevo combo/total.
            _service.SetHolds(_holds);
            ShowRerollPrompt();
        }

        private void EnsureHoldsLength()
        {
            int n = _diceSlots != null ? _diceSlots.Count : 0;
            if (_holds == null || _holds.Length != n) _holds = new bool[n];
        }

        private void SetRootVisible(bool visible)
        {
            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = visible ? 1f : 0f;
                _rootCanvasGroup.interactable = visible;
                _rootCanvasGroup.blocksRaycasts = visible;
                return;
            }
            // Fallback (no deberia pasar — Awake garantiza que _rootCanvasGroup exista
            // mientras _rootPanel no sea null).
            if (_rootPanel != null) _rootPanel.SetActive(visible);
        }

        private void OnEnable()
        {
            TrySubscribeToService();
        }

        // Retry de subscripcion: si OnEnable corrio antes de que ActionRollServiceBootstrap
        // registrara el service (race comun: la scene se carga antes que arranque el Run),
        // pollear hasta lograr suscribirse. Una vez subscripto, es no-op por frame.
        private void Update()
        {
            if (_service != null) return;
            TrySubscribeToService();
        }

        private void TrySubscribeToService()
        {
            if (_service != null) return;
            if (!ServiceLocator.TryGetService<IActionRollService>(out _service) || _service == null)
            {
                // No spammeamos el warning una vez por frame — solo el primer OnEnable lo loggea
                // (controlado via _subscribeWarningLogged). Si el service nunca aparece, hay un
                // bug de bootstrap (no algo que el retry resuelva).
                if (!_subscribeWarningLogged)
                {
                    Debug.LogWarning("[ActionRollPanelView] IActionRollService no registrado todavia. " +
                                     "El panel se autosuscribira cuando este disponible.");
                    _subscribeWarningLogged = true;
                }
                return;
            }

            _service.OnPhaseChanged += HandlePhaseChanged;
            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            // DiceZoneView (HUD compartido) dispara TypedEvent<ComboMatchedPayload>
            // cada vez que el user togglea un hold via su ToggleHold. El service
            // SetHolds recomputa combo+total pero NO dispara evento — escuchamos
            // este como proxy para refrescar el SummaryLabel del panel con el
            // combo/total actualizados.
            TypedEvent<ComboMatchedPayload>.Subscribe(HandleComboRefresh);

            // Por si entramos a una fase ya en curso (ej. domain reload / re-enable o
            // suscripcion tardia tras el race de bootstrap):
            HandlePhaseChanged(_service.Phase);
        }

        private void HandleComboRefresh(ComboMatchedPayload payload)
        {
            if (_service == null || !_service.IsActive) return;
            if (_service.Phase != ActionRollPhase.AwaitingRerollDecision) return;
            // Re-render: lee _service.CurrentCombo / CurrentEffectiveTotal frescos
            // (ya actualizados por SetHolds → RecomputeComboAndTotal).
            ShowRerollPrompt();
        }

        private bool _subscribeWarningLogged;

        private void OnDisable()
        {
            if (_service != null) _service.OnPhaseChanged -= HandlePhaseChanged;
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            TypedEvent<ComboMatchedPayload>.Unsubscribe(HandleComboRefresh);

            // Clearear _service para que el proximo OnEnable / Update vuelva a
            // intentar la subscripcion. Sin esto, re-enable encontraria _service
            // != null y haria skip del subscribe (que recien acabamos de undo).
            _service = null;
            _subscribeWarningLogged = false;

            if (_autoHideRoutine != null)
            {
                StopCoroutine(_autoHideRoutine);
                _autoHideRoutine = null;
            }
            HideAllPanels();
            SetRootVisible(false);
        }

        private void OnDestroy()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClick);
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(OnCancelClick);
            if (_rerollAcceptButton != null) _rerollAcceptButton.onClick.RemoveListener(OnRerollAcceptClick);
            if (_rerollDeclineButton != null) _rerollDeclineButton.onClick.RemoveListener(OnRerollDeclineClick);
        }

        // ---- Phase routing ---------------------------------------------------

        private void HandlePhaseChanged(ActionRollPhase phase)
        {
            // El display de dados es responsabilidad del DiceZoneView compartido
            // (Canvas/DiceZoneView). Este panel solo muestra los CONTROLES (combo+
            // threshold summary + botones reroll/confirm). El DiceRow interno
            // queda permanentemente oculto — sus _diceSlots no se renderean, pero
            // el subscribe a OnDiceRolled lo mantenemos para no romper el contrato
            // del componente.
            switch (phase)
            {
                case ActionRollPhase.Rolling:
                    // Sin botones — todavia no hay decision. DiceZoneView se encarga
                    // de mostrar los dados; el panel queda invisible hasta AwaitingReroll.
                    SetRootVisible(false);
                    HideAllPanels();
                    break;
                case ActionRollPhase.AwaitingRerollDecision:
                    ShowRerollPrompt();
                    break;
                case ActionRollPhase.Resolved:
                    ShowResolved();
                    break;
                default:
                    HideAllPanels();
                    SetRootVisible(false);
                    break;
            }
        }

        private void ShowConfirm()
        {
            SetRootVisible(true);
            HideAllPanels();
            if (_confirmPanel != null) _confirmPanel.SetActive(true);
            // Pre-roll: el DiceRow se oculta para que el user no vea slots vacíos al lado
            // del confirm dialog. Se vuelve a mostrar al pasar a Rolling.
            SetDiceRowVisible(false);

            var spec = _service.CurrentSpec;
            if (_confirmActionLabel != null) _confirmActionLabel.text = spec.ActionLabel ?? string.Empty;
            if (_confirmThresholdLabel != null) _confirmThresholdLabel.text = $"Necesitás >= {spec.Threshold}";
            if (_confirmCostLabel != null) _confirmCostLabel.text = $"Cuesta {spec.EnergyCost} de energía";

            ClearDiceSlots();
        }

        private void ShowDice()
        {
            SetRootVisible(true);
            HideAllPanels();
            SetDiceRowVisible(true);
            // Las caras se actualizan via HandleDiceRolled — no hay panel dedicado para "rolling"
            // mas alla de los slots, que viven directamente bajo _rootPanel.
        }

        private void ShowRerollPrompt()
        {
            // Root visible + RerollPanel activo. El DiceRow interno queda OCULTO —
            // los dados los muestra DiceZoneView (HUD compartido con combat).
            SetRootVisible(true);
            SetDiceRowVisible(false);
            if (_rerollPanel != null) _rerollPanel.SetActive(true);
            if (_confirmPanel != null) _confirmPanel.SetActive(false);
            if (_resolvedPanel != null) _resolvedPanel.SetActive(false);

            var spec = _service.CurrentSpec;
            int threshold = spec.Threshold;
            int cost = spec.RerollEnergyCost;
            int currentTotal = _service.CurrentEffectiveTotal;
            var combo = _service.CurrentCombo;
            // Multi-shot: el reroll se puede repetir mientras haya energía. El service
            // expone CanAffordReroll que combina "fase correcta" + "energía suficiente".
            bool rerollAvailable = _service.CanAffordReroll;
            bool canConfirm = _service.CanConfirm;

            // Summary con dos "secciones" visuales tipo combat HUD: combo arriba,
            // threshold debajo. TMP rich text — ambos en el mismo label porque
            // el panel no tiene labels separados (legacy del merge). El designer
            // puede splittear esto en dos GO con TMP en el inspector si quiere.
            if (_rerollSummaryLabel != null)
            {
                string comboName = combo != null ? combo.DisplayName : "Sin combo";
                string thresholdColor = currentTotal >= threshold ? "#88ff88" : "#ffcc66";
                string actionTag = string.IsNullOrEmpty(spec.ActionLabel) ? "Acción" : spec.ActionLabel;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"<size=130%><b>{comboName}</b></size>");
                sb.AppendLine($"<color={thresholdColor}>Total: {currentTotal} / Umbral: {threshold}</color>");
                sb.AppendLine();
                if (!canConfirm)
                {
                    sb.Append($"<size=85%>{actionTag} — clickeá los dados para armar tu combo</size>");
                }
                else if (rerollAvailable)
                {
                    sb.Append($"<size=85%>Reroll: {cost} energía. O confirmá la tirada.</size>");
                }
                else
                {
                    sb.Append($"<size=85%>Sin energía para reroll — confirmá.</size>");
                }
                _rerollSummaryLabel.text = sb.ToString();
            }

            if (_rerollAcceptButton != null) _rerollAcceptButton.interactable = rerollAvailable;
            // Confirm/Decline solo se habilita si hay al menos un dado holdeado — sin dados
            // seleccionados el user no puede aceptar la tirada (spec: armar combo del contrato).
            if (_rerollDeclineButton != null) _rerollDeclineButton.interactable = canConfirm;
        }

        private void ShowResolved()
        {
            SetRootVisible(true);
            // DiceRow oculto — DiceZoneView (HUD compartido) ya muestra los dados.
            SetDiceRowVisible(false);
            if (_confirmPanel != null) _confirmPanel.SetActive(false);
            if (_rerollPanel != null) _rerollPanel.SetActive(false);
            if (_resolvedPanel != null) _resolvedPanel.SetActive(true);

            var spec = _service.CurrentSpec;
            int total = _service.CurrentEffectiveTotal;
            int threshold = spec.Threshold;
            bool passed = total >= threshold;
            var combo = _service.CurrentCombo;
            string comboTag = combo != null ? combo.DisplayName : "sin combo";

            // Curarse no tiene "fallo" — bajo el umbral igual cura el monto base.
            // Forzar Puerta sí: bajo umbral = energía perdida + sigue en sala.
            if (spec.AlwaysSucceeds)
            {
                if (_resolvedHeader != null)
                    _resolvedHeader.text = spec.ActionLabel ?? "Curado";
                if (_resolvedDetail != null)
                    _resolvedDetail.text = passed
                        ? $"{comboTag} - bonus +{total - threshold} HP ({total} >= {threshold})"
                        : $"{comboTag} - curación base ({total} / {threshold})";
            }
            else
            {
                if (_resolvedHeader != null)
                    _resolvedHeader.text = passed ? "¡Exito!" : "Fallaste";
                if (_resolvedDetail != null)
                    _resolvedDetail.text = passed
                        ? $"{comboTag} · {total} >= {threshold}"
                        : $"{comboTag} · {total} < {threshold}";
            }

            if (_autoHideRoutine != null) StopCoroutine(_autoHideRoutine);
            _autoHideRoutine = StartCoroutine(AutoHideAfterDelay(_resolvedAutoHideSeconds));
        }

        private IEnumerator AutoHideAfterDelay(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            HideAllPanels();
            SetRootVisible(false);
            _autoHideRoutine = null;
        }

        private void SetDiceRowVisible(bool visible)
        {
            if (_diceRowContainer != null) _diceRowContainer.SetActive(visible);
        }

        // Si el user no cableó _diceRowContainer en Inspector, lo intenta resolver:
        // primero un hijo del rootPanel llamado "DiceRow"; si no, el parent común de los
        // _diceSlots (asumiendo que viven todos bajo el mismo contenedor).
        private void AutoResolveDiceRowContainer()
        {
            if (_diceRowContainer != null) return;
            if (_rootPanel != null)
            {
                var byName = _rootPanel.transform.Find("DiceRow");
                if (byName != null)
                {
                    _diceRowContainer = byName.gameObject;
                    return;
                }
            }
            if (_diceSlots != null && _diceSlots.Count > 0 && _diceSlots[0] != null)
            {
                var parent = _diceSlots[0].transform.parent;
                if (parent != null) _diceRowContainer = parent.gameObject;
            }
        }

        private void HideAllPanels()
        {
            if (_confirmPanel != null) _confirmPanel.SetActive(false);
            if (_rerollPanel != null) _rerollPanel.SetActive(false);
            if (_resolvedPanel != null) _resolvedPanel.SetActive(false);
        }

        private void ClearDiceSlots()
        {
            if (_diceSlots == null) return;
            foreach (var slot in _diceSlots) slot?.Clear();
        }

        // ---- Bus handlers ----------------------------------------------------

        private void HandleDiceRolled(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (_service == null || !_service.IsActive)
            {
                Debug.LogWarning($"[ActionRollPanelView] HandleDiceRolled: _service={_service != null} IsActive={_service?.IsActive} → ignored.");
                return;
            }
            if (args[0] is not Guid guid || guid != _service.CurrentPlayerGuid)
            {
                Debug.LogWarning($"[ActionRollPanelView] HandleDiceRolled: guid mismatch — got={(args[0] is Guid g ? g.ToString() : "non-Guid")} expected={_service.CurrentPlayerGuid} → ignored.");
                return;
            }

            var faces = args[1] as IReadOnlyList<int>;
            if (faces == null || _diceSlots == null) return;
            Debug.LogWarning($"[ActionRollPanelView] HandleDiceRolled: actualizando dados a [{string.Join(",", faces)}] (slots={_diceSlots.Count}).");

            EnsureHoldsLength();

            // Reset de holds SOLO en el initial roll. Si la fase post-roll es
            // AwaitingRerollDecision Y el rollIndex ya es >=2, significa que esto
            // fue un reroll → los holds visuales se conservan (los dados que el
            // user marco antes del reroll quedan held; el array logico tambien
            // se conserva para que coincida con la visual).
            bool isInitialRoll = _service.RollIndex <= 1;
            if (isInitialRoll)
            {
                for (int i = 0; i < _holds.Length; i++) _holds[i] = false;
            }

            for (int i = 0; i < _diceSlots.Count; i++)
            {
                if (_diceSlots[i] == null) continue;
                int face = i < faces.Count ? faces[i] : 0;
                _diceSlots[i].ShowFace(face);
                _diceSlots[i].SetHeld(_holds[i]);
            }

            // Si estamos en AwaitingRerollDecision, refrescar el summary del panel
            // (reroll button enable/disable, texto del threshold) — el bus de
            // OnPhaseChanged no se dispara en post-reroll porque la fase no cambia
            // (sigue siendo AwaitingRerollDecision).
            if (_service.Phase == ActionRollPhase.AwaitingRerollDecision)
            {
                ShowRerollPrompt();
            }
        }

        // ---- Button callbacks ------------------------------------------------

        private void OnConfirmClick() => _service?.Confirm();
        private void OnCancelClick() => _service?.Cancel();

        // Reroll respeta los holds — los dados con _holds[i]=true se conservan, los
        // demas se re-tiran. Single shot: el button se deshabilita post-reroll en
        // ShowRerollPrompt (rollIndex >= 2).
        private void OnRerollAcceptClick()
        {
            if (_service == null) return;
            EnsureHoldsLength();
            int heldCount = 0;
            for (int i = 0; i < _holds.Length; i++) if (_holds[i]) heldCount++;
            Debug.LogWarning($"[ActionRollPanelView] Reroll click — held={heldCount}/{_holds.Length}");
            _service.RequestReroll(_holds);
        }

        // Decline = resolver con la tirada actual (igual que Confirm en AwaitingRerollDecision).
        private void OnRerollDeclineClick() => _service?.DeclineReroll();
    }
}
