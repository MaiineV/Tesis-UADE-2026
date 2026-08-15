using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Contenido del drawer de contrato: la tabla de combos del héroe, de menor a mayor
    /// daño base, con la marca de cada regla que el jefe le puso encima. Es una cheat
    /// sheet — se puede jugar con ella abierta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El gesto de abrir/cerrar lo maneja <see cref="SlidingDrawer"/>, en el mismo
    /// GameObject; acá vive solo lo que se muestra. Las filas se arman al ABRIR, así que
    /// una tabla cerrada ya se pinta correcta sola al abrirse.
    /// </para>
    /// <para>
    /// <b>Y además escucha.</b> Se puede jugar con el drawer abierto, y el Anotador corre
    /// filas en pleno turno del jefe: sin la suscripción, la tabla que el jugador está
    /// mirando se quedaba con el valor viejo justo cuando cambia. Estando cerrada no
    /// repinta — no hay a quién mentirle, y la planilla persistente
    /// (<see cref="ContractRuleBoardView"/>) es la que avisa que algo cambió.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Contract Drawer View")]
    [RequireComponent(typeof(SlidingDrawer))]
    public class ContractDrawerView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private RectTransform _rowsContainer;
        [SerializeField, Required] private ContractComboRowView _rowPrefab;
        [SerializeField, Required] private ContractSheetUiSettingsSO _settings;

        [Title("Encabezados")]
        [SerializeField] private TextMeshProUGUI _exampleHeader;
        [SerializeField] private TextMeshProUGUI _nameHeader;
        [SerializeField] private TextMeshProUGUI _damageHeader;

        private readonly List<ContractComboRowView> _rows = new();
        private SlidingDrawer _drawer;

        private void Awake()
        {
            if (_drawer == null) TryGetComponent(out _drawer);
            if (_drawer != null) _drawer.Opened += RebuildRows;

            RefreshHeaders();
            LocalizationRefresh.Subscribe(RefreshHeaders);
            SubscribeToRuleChanges();
        }

        private void OnDestroy()
        {
            if (_drawer != null) _drawer.Opened -= RebuildRows;
            LocalizationRefresh.Unsubscribe(RefreshHeaders);
            UnsubscribeFromRuleChanges();
        }

        // ------------------------------------------------------------------
        // Reglas en vivo
        // ------------------------------------------------------------------

        private void SubscribeToRuleChanges()
        {
            EventManager.Subscribe(EventName.OnContractModifierChanged, HandleRuleChanged);
            EventManager.Subscribe(EventName.OnComboBlocked, HandleRuleChanged);
            EventManager.Subscribe(EventName.OnComboUnblocked, HandleRuleChanged);
            // La cuenta regresiva del bloqueo baja al cerrarse un turno, y nadie emite un
            // evento por el decremento — sin esto el badge muestra los turnos de cuando abriste.
            EventManager.Subscribe(EventName.OnTurnFinished, HandleRuleChanged);
        }

        private void UnsubscribeFromRuleChanges()
        {
            EventManager.UnSubscribe(EventName.OnContractModifierChanged, HandleRuleChanged);
            EventManager.UnSubscribe(EventName.OnComboBlocked, HandleRuleChanged);
            EventManager.UnSubscribe(EventName.OnComboUnblocked, HandleRuleChanged);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleRuleChanged);
        }

        private void HandleRuleChanged(params object[] args)
        {
            if (_drawer != null && !_drawer.IsOpen) return;
            RebuildRows();
        }

        // ------------------------------------------------------------------
        // Contenido
        // ------------------------------------------------------------------

        /// <summary>
        /// Encabezados desde la tabla UI. El texto autorado en el prefab queda de fallback
        /// para cuando Localization todavía no resolvió (o en escenas de test).
        /// </summary>
        private void RefreshHeaders()
        {
            SetHeader(_exampleHeader, ContractTextKeys.HeaderExample, "Ejemplo");
            SetHeader(_nameHeader, ContractTextKeys.HeaderName, "Combo");
            SetHeader(_damageHeader, ContractTextKeys.HeaderDamage, "Daño base");
        }

        private static void SetHeader(TextMeshProUGUI label, string key, string fallback)
        {
            if (label != null) label.text = LocalizedContent.Ui(key, fallback);
        }

        /// <summary>
        /// Repuebla las filas con el contrato del héroe actual y las marcas vigentes. Sin
        /// héroe (escena suelta en el editor) deja la tabla vacía en vez de romper.
        /// </summary>
        public void RebuildRows()
        {
            if (_rowsContainer == null || _rowPrefab == null) return;

            var sheet = ContractRowStateResolver.ResolvePlayerSheet();
            var combos = SortedByBaseDamage(sheet);
            var states = ContractRowStateResolver.ResolveAll(combos, sheet);
            int count = combos.Count;

            EnsureRows(count);

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < count;
                _rows[i].gameObject.SetActive(used);
                if (used) _rows[i].Bind(combos[i], _settings, states[i]);
            }
        }

        // El orden vive en el resolver porque la planilla persistente tiene que listar las
        // mismas filas en el mismo orden que la tabla: dos criterios distintos harían que la
        // fila corrida apareciera en distinto lugar según dónde la mires.
        private static List<BaseComboSO> SortedByBaseDamage(ContractSheet sheet)
            => ContractRowStateResolver.SortByBaseDamage(sheet);

        // Los slots se reusan y solo se apagan: la tabla se repuebla en cada apertura y
        // destruir/instanciar nueve filas cada vez sería churn de GC en pleno combate.
        private void EnsureRows(int needed)
        {
            while (_rows.Count < needed)
                _rows.Add(Instantiate(_rowPrefab, _rowsContainer));
        }
    }
}
