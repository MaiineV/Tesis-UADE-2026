using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view que renderiza un <see cref="ContractSheet"/> como tabla de combos. Instancia
    /// un <see cref="ComboRowView"/> por combo de <see cref="ContractSheet.Combos"/>,
    /// ordenando por <see cref="BaseComboSO.Priority"/> ascendente (Par primero, Generala
    /// ultimo — matchea GDD §5.4).
    /// Plan §4.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No</b> se registra en <see cref="Patterns.ServiceLocator"/> ni suscribe a
    /// <see cref="Patterns.EventManager"/>: es pura render. Se invoca una vez via
    /// <see cref="Bind"/> desde <see cref="Rollgeon.UI.Screens.ClassSelectionScreen"/>.
    /// </para>
    /// <para>
    /// [SETUP] Arrastrar en el Inspector: el TMP del header, el Transform del container de
    /// rows (tipicamente un <c>VerticalLayoutGroup</c>) y el prefab del <see cref="ComboRowView"/>.
    /// Ver <c>docs/setup/UI#0098_ClassSelectionScreen.md §8.5</c>.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Contract Display View")]
    public class ContractDisplayView : MonoBehaviour
    {
        private const string LogPrefix = "[ContractDisplayView] ";

        [Title("Contract Display — Widget refs")]
        [Required("Arrastrar el TMP del header (ej. 'Contrato del Guerrero').")]
        [SerializeField]
        [Tooltip("Header label. Se rellena con ContractSheet.DisplayLabel (fallback 'Contrato').")]
        private TextMeshProUGUI _headerLabel;

        [Required("Arrastrar el Transform padre donde se instancian las rows (tipicamente un " +
                  "VerticalLayoutGroup).")]
        [SerializeField]
        private Transform _rowsContainer;

        [Required("Arrastrar el prefab de ComboRow.")]
        [SerializeField]
        [Tooltip("Prefab del ComboRowView. Se Instantiate() una copia por combo. El prefab lo " +
                 "arma el usuario en engine (instructivo §8.5).")]
        private ComboRowView _rowPrefab;

        [SerializeField]
        [Tooltip("Footer opcional (ej. 'Dano minimo = dado mas alto'). Se deja en null si no aplica.")]
        private TextMeshProUGUI _footerLabel;

        // Filas instanciadas en el último Bind — usadas para refrescar los valores efectivos
        // cuando el Boss 3 cambia la capa de modificadores del Contrato (§4).
        private readonly List<ComboRowView> _rows = new List<ComboRowView>();
        private bool _subscribed;

        /// <summary>
        /// Limpia rows previas, setea el header y re-instancia una <see cref="ComboRowView"/>
        /// por cada combo de <paramref name="sheet"/>.<see cref="ContractSheet.Combos"/>,
        /// en orden ascendente por <see cref="BaseComboSO.Priority"/>.
        /// </summary>
        /// <param name="sheet">Hoja de contrato. <c>null</c> o <c>Combos</c> vacio loggea warning.</param>
        public void Bind(ContractSheet sheet)
        {
            Clear();

            if (sheet == null)
            {
                Debug.LogWarning(LogPrefix + "Bind called with null sheet.", this);
                return;
            }

            if (_headerLabel != null)
            {
                var label = sheet.DisplayLabel;
                _headerLabel.text = string.IsNullOrEmpty(label) ? "Contrato" : label;
            }

            if (_rowPrefab == null)
            {
                Debug.LogWarning(LogPrefix + "_rowPrefab no esta cableado — no se renderean rows.", this);
                return;
            }

            if (_rowsContainer == null)
            {
                Debug.LogWarning(LogPrefix + "_rowsContainer no esta cableado — no se renderean rows.", this);
                return;
            }

            var combos = sheet.Combos;
            if (combos == null || combos.Count == 0)
            {
                Debug.LogWarning(LogPrefix + "ContractSheet.Combos esta vacio o null.", this);
                return;
            }

            // Copiamos a array local y ordenamos por Priority ascendente. No mutamos la lista
            // original del sheet (invariante del catalogo — ver ContractSheet docs).
            var ordered = new BaseComboSO[combos.Count];
            for (int i = 0; i < combos.Count; i++)
            {
                ordered[i] = combos[i];
            }
            System.Array.Sort(ordered, ComparePriorityAscending);

            for (int i = 0; i < ordered.Length; i++)
            {
                var combo = ordered[i];
                if (combo == null)
                {
                    Debug.LogWarning(LogPrefix + $"Skip null combo en index [{i}] del sheet.", this);
                    continue;
                }

                var row = Instantiate(_rowPrefab, _rowsContainer);
                row.Bind(combo);
                _rows.Add(row);
            }

            // Boss 3 (§4): refrescar los valores cuando cambie la capa de modificadores.
            if (!_subscribed)
            {
                EventManager.Subscribe(EventName.OnContractModifierChanged, HandleContractModifierChanged);
                _subscribed = true;
            }
        }

        private void HandleContractModifierChanged(params object[] args)
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i]?.RefreshDamage();
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                EventManager.UnSubscribe(EventName.OnContractModifierChanged, HandleContractModifierChanged);
                _subscribed = false;
            }
        }

        /// <summary>
        /// Destruye todas las rows previamente instanciadas bajo <see cref="_rowsContainer"/>.
        /// </summary>
        public void Clear()
        {
            _rows.Clear();
            if (_rowsContainer == null) return;
            for (int i = _rowsContainer.childCount - 1; i >= 0; i--)
            {
                var child = _rowsContainer.GetChild(i);
                if (child != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        private static int ComparePriorityAscending(BaseComboSO a, BaseComboSO b)
        {
            int pa = a != null ? a.Priority : int.MinValue;
            int pb = b != null ? b.Priority : int.MinValue;
            return pa.CompareTo(pb);
        }
    }
}
