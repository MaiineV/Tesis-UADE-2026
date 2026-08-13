using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Contenido del drawer de contrato: la tabla de combos del héroe, de menor a mayor
    /// daño base. Es una cheat sheet — se puede jugar con ella abierta.
    /// </summary>
    /// <remarks>
    /// El gesto de abrir/cerrar lo maneja <see cref="SlidingDrawer"/>, en el mismo
    /// GameObject; acá vive solo lo que se muestra. Las filas se arman al ABRIR y no al
    /// bindear: el contrato cambia durante la run (combos tachados, cambios de daño base)
    /// y así esta vista no necesita escuchar nada.
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
        }

        private void OnDestroy()
        {
            if (_drawer != null) _drawer.Opened -= RebuildRows;
            LocalizationRefresh.Unsubscribe(RefreshHeaders);
        }

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
        /// Repuebla las filas con el contrato del héroe actual. Sin héroe (escena suelta en
        /// el editor) deja la tabla vacía en vez de romper.
        /// </summary>
        public void RebuildRows()
        {
            if (_rowsContainer == null || _rowPrefab == null) return;

            var sheet = ServiceLocator.TryGetService<IPlayerService>(out var players)
                ? players?.CurrentHero?.Sheet
                : null;

            var combos = SortedByBaseDamage(sheet);
            int count = combos.Count;

            EnsureRows(count);

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < count;
                _rows[i].gameObject.SetActive(used);
                if (used) _rows[i].Bind(combos[i], sheet, _settings);
            }
        }

        /// <summary>
        /// Combos de menor a mayor daño base — es el orden en que el jugador los va a
        /// buscar, y deja la escalera de valor a la vista.
        /// </summary>
        /// <remarks>
        /// Ordena una COPIA: <c>sheet.Combos</c> es la lista viva del contrato del héroe y
        /// reordenarla desde la UI le cambiaría el orden a todo el que la recorra.
        /// </remarks>
        private static List<BaseComboSO> SortedByBaseDamage(ContractSheet sheet)
        {
            var ordered = new List<BaseComboSO>();
            if (sheet?.Combos == null) return ordered;

            foreach (var combo in sheet.Combos)
                if (combo != null) ordered.Add(combo);

            ordered.Sort((a, b) =>
            {
                int byDamage = ComboRowView.ResolveBaseDamage(a, sheet)
                    .CompareTo(ComboRowView.ResolveBaseDamage(b, sheet));
                // Empate: por nombre, para que el orden no baile entre aperturas.
                return byDamage != 0 ? byDamage : string.CompareOrdinal(a.ComboId, b.ComboId);
            });
            return ordered;
        }

        // Los slots se reusan y solo se apagan: la tabla se repuebla en cada apertura y
        // destruir/instanciar nueve filas cada vez sería churn de GC en pleno combate.
        private void EnsureRows(int needed)
        {
            while (_rows.Count < needed)
                _rows.Add(Instantiate(_rowPrefab, _rowsContainer));
        }
    }
}
