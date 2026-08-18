using System.Collections.Generic;
using Patterns;
using Rollgeon.Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// La planilla: cartel persistente con las filas del contrato que tienen una regla
    /// encima. Requisito de entrega del Anotador — su mecánica central es corregir la hoja,
    /// y sin verla en pantalla la pelea es ilegible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es el complemento de <see cref="ContractDrawerView"/>, no un reemplazo: el drawer
    /// muestra la tabla completa cuando el jugador la pide, la planilla muestra siempre
    /// SÓLO lo que cambió. Sin ninguna regla activa se apaga entera y no ocupa pantalla.
    /// </para>
    /// <para>
    /// Comparte fila (<see cref="ContractComboRowView"/>), settings y orden con el drawer
    /// para que la misma regla se lea igual en los dos lados.
    /// </para>
    /// <para>
    /// [SETUP] <see cref="_panel"/> tiene que ser un HIJO, no este mismo GameObject: se
    /// apaga y se prende, y apagarse a sí mismo dejaría la vista sin poder volver.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Contract Rule Board View")]
    public class ContractRuleBoardView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private RectTransform _rowsContainer;
        [SerializeField, Required] private ContractComboRowView _rowPrefab;
        [SerializeField, Required] private ContractSheetUiSettingsSO _settings;

        [SerializeField, Required]
        [Tooltip("Raíz visible del cartel (un HIJO). Se apaga cuando no hay ninguna regla activa.")]
        private GameObject _panel;

        [SerializeField] private TextMeshProUGUI _titleLabel;

        [Title("Límites")]
        [SerializeField, MinValue(1)]
        [Tooltip("Tope de filas visibles. La planilla es un aviso, no la tabla entera: " +
                 "pasado el tope el jugador abre el drawer.")]
        private int _maxRows = 4;

        private readonly List<ContractComboRowView> _rows = new();

        private void Awake()
        {
            RefreshTitle();
            LocalizationRefresh.Subscribe(RefreshTitle);
            SubscribeToRuleChanges();
            Refresh();
        }

        private void OnDestroy()
        {
            LocalizationRefresh.Unsubscribe(RefreshTitle);
            UnsubscribeFromRuleChanges();
        }

        // ------------------------------------------------------------------
        // Suscripciones
        // ------------------------------------------------------------------

        private void SubscribeToRuleChanges()
        {
            EventManager.Subscribe(EventName.OnContractModifierChanged, HandleRuleChanged);
            EventManager.Subscribe(EventName.OnComboBlocked, HandleRuleChanged);
            EventManager.Subscribe(EventName.OnComboUnblocked, HandleRuleChanged);
            // El bloqueo descuenta turnos al cerrarse cada turno sin emitir nada propio.
            EventManager.Subscribe(EventName.OnTurnFinished, HandleRuleChanged);

            // Al salir del combate se limpian los servicios, pero no todos avisan: el cartel
            // se baja por cuenta propia en vez de arriesgarse a mostrar reglas de un jefe muerto.
            EventManager.Subscribe(EventName.OnCombatEnd, HandleCombatEnded);
            EventManager.Subscribe(EventName.OnRunEnd, HandleCombatEnded);
        }

        private void UnsubscribeFromRuleChanges()
        {
            EventManager.UnSubscribe(EventName.OnContractModifierChanged, HandleRuleChanged);
            EventManager.UnSubscribe(EventName.OnComboBlocked, HandleRuleChanged);
            EventManager.UnSubscribe(EventName.OnComboUnblocked, HandleRuleChanged);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleRuleChanged);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleCombatEnded);
            EventManager.UnSubscribe(EventName.OnRunEnd, HandleCombatEnded);
        }

        private void HandleRuleChanged(params object[] args) => Refresh();

        private void HandleCombatEnded(params object[] args) => Hide();

        private void RefreshTitle()
        {
            if (_titleLabel != null)
                _titleLabel.text = LocalizedContent.Ui(ContractTextKeys.RuleBoardTitle, "PLANILLA");
        }

        // ------------------------------------------------------------------
        // Contenido
        // ------------------------------------------------------------------

        /// <summary>
        /// Repinta el cartel con las filas alteradas del contrato actual. Pública para
        /// tooling y tests: la vista no expone otro camino para forzar el repintado.
        /// </summary>
        public void Refresh()
        {
            if (_rowsContainer == null || _rowPrefab == null) return;

            var sheet = ContractRowStateResolver.ResolvePlayerSheet();
            var combos = ContractRowStateResolver.SortByBaseDamage(sheet);
            var states = ContractRowStateResolver.ResolveAll(combos, sheet);

            int shown = 0;
            for (int i = 0; i < combos.Count && shown < _maxRows; i++)
            {
                if (!states[i].IsAltered) continue;

                EnsureRows(shown + 1);
                _rows[shown].gameObject.SetActive(true);
                _rows[shown].Bind(combos[i], _settings, states[i]);
                shown++;
            }

            for (int i = shown; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(false);

            if (_panel != null) _panel.SetActive(shown > 0);
        }

        /// <summary>Baja el cartel sin consultar servicios.</summary>
        public void Hide()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(false);

            if (_panel != null) _panel.SetActive(false);
        }

        // Mismo criterio que el drawer: los slots se reusan y sólo se apagan.
        private void EnsureRows(int needed)
        {
            while (_rows.Count < needed)
                _rows.Add(Instantiate(_rowPrefab, _rowsContainer));
        }
    }
}
