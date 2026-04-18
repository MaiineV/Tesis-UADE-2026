using System;
using Patterns;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view que muestra "{used}/{cap}" rerolls + un boton "extra roll (1E)".
    /// Consume <see cref="IRerollBudgetService"/> via <see cref="Patterns.ServiceLocator"/>
    /// y escucha <see cref="EventName.OnRerollBudgetChanged"/> + el evento tipado
    /// <see cref="IRerollBudgetService.OnRerollStarted"/>.
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

        [Title("Reroll Count — Widgets")]
        [SerializeField]
        [Tooltip("Label '{used}/{cap}'. Fallback '-/-' si no hay IRerollBudgetService.")]
        private TextMeshProUGUI _countLabel;

        [SerializeField]
        [Tooltip("Boton 'extra roll (1E)'. Mirrea ActionButtonsView._energyRerollButton " +
                 "pero es una afordance separada pegada a la dice zone.")]
        private Button _extraRollButton;

        [Title("Reroll Count — Config")]
        [SerializeField]
        [Tooltip("Formato del label. Default '{0}/{1}'.")]
        private string _countFormat = "{0}/{1}";

        [SerializeField]
        [Tooltip("Texto fallback cuando no hay IRerollBudgetService.")]
        private string _fallbackText = "-/-";

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
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            // Legacy stub event (T104 puede o no emitirlo).
            EventManager.Subscribe(EventName.OnRerollBudgetChanged, HandleBudgetChangedLegacy);

            // Subscripcion typed al servicio si esta registrado.
            if (ServiceLocator.TryGetService<IRerollBudgetService>(out _budget) && _budget != null)
            {
                _onRerollStartedTyped = HandleRerollStartedTyped;
                _budget.OnRerollStarted += _onRerollStartedTyped;
            }
            else
            {
                Debug.Log(LogPrefix + "IRerollBudgetService no registrado — label en fallback.", this);
                _budget = null;
            }

            _bound = true;
            RefreshLabel();
            RefreshButtonInteractable();
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnRerollBudgetChanged, HandleBudgetChangedLegacy);

            if (_budget != null && _onRerollStartedTyped != null)
            {
                _budget.OnRerollStarted -= _onRerollStartedTyped;
                _onRerollStartedTyped = null;
            }
            _budget = null;
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
            _onExtraRollPressed?.Invoke();
        }

        private void HandleBudgetChangedLegacy(params object[] args)
        {
            // [STUB T104] schema: [Guid playerGuid, int used, int cap]
            if (args == null || args.Length < 3) return;
            if (!(args[0] is Guid guid) || guid != _playerGuid) return;
            if (!(args[1] is int used) || !(args[2] is int cap)) return;

            SetCount(used, cap);
            RefreshButtonInteractable();
        }

        private void HandleRerollStartedTyped(RerollStartedPayload payload)
        {
            if (payload.PlayerGuid != _playerGuid) return;
            RefreshLabel();
            RefreshButtonInteractable();
        }

        // ======================================================================
        // Internals
        // ======================================================================

        private void RefreshLabel()
        {
            if (_budget == null || _budget.Current == null)
            {
                SetFallback();
                return;
            }
            int used = _budget.Current.PaidRollsUsed;
            int cap = _budget.Current.FreeRollsRemaining + _budget.Current.PaidRollsUsed;
            // Nota: el "cap" real (tope de rerolls) depende del ActionDefinitionSO.FreeRollCount;
            // lo aproximamos con used+remaining como proxy visual. T104 puede exponer un
            // getter especifico y entonces se reemplaza.
            SetCount(used, cap);
        }

        private void RefreshButtonInteractable()
        {
            if (_extraRollButton == null) return;

            if (_budget == null)
            {
                _extraRollButton.interactable = false;
                return;
            }

            var query = _budget.QueryExtraRoll(_playerGuid);
            _extraRollButton.interactable = query.IsAvailable;
        }
    }
}
