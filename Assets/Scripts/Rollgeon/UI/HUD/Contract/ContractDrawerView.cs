using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Rollgeon.Player;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Drawer del contrato: desliza desde la izquierda con la tabla de combos del héroe.
    /// Es consulta, no decisión — NO pausa el juego.
    /// </summary>
    /// <remarks>
    /// Se cierra por tres caminos porque los tres son gestos que el jugador ya tiene: el
    /// mismo ícono que lo abrió, Esc (igual que la consola de dev) y click afuera. El
    /// backdrop que captura ese click solo existe mientras está abierto: dejarlo activo
    /// comería los clicks del tablero.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Contract Drawer View")]
    public class ContractDrawerView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private RectTransform _panel;

        [SerializeField, Required]
        [Tooltip("Captura el click afuera para cerrar. Se apaga junto con el drawer.")]
        private Button _backdrop;

        [SerializeField, Required] private RectTransform _rowsContainer;
        [SerializeField, Required] private ContractComboRowView _rowPrefab;
        [SerializeField, Required] private ContractSheetUiSettingsSO _settings;

        [Title("Encabezados")]
        [SerializeField] private TextMeshProUGUI _exampleHeader;
        [SerializeField] private TextMeshProUGUI _nameHeader;
        [SerializeField] private TextMeshProUGUI _damageHeader;

        [Title("Animación")]
        [SerializeField, Tooltip("X del panel cerrado — fuera de pantalla por la izquierda.")]
        private float _closedX = -1000f;

        [SerializeField, Tooltip("X del panel abierto.")]
        private float _openX = 0f;

        [SerializeField] private float _slideSeconds = 0.28f;
        [SerializeField] private Ease _slideEase = Ease.OutCubic;

        [ShowInInspector, ReadOnly] private bool _isOpen;

        private readonly List<ContractComboRowView> _rows = new();
        private Tween _slide;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_backdrop != null) _backdrop.onClick.AddListener(Close);
            ApplyClosedInstantly();
            RefreshHeaders();
            LocalizationRefresh.Subscribe(RefreshHeaders);
        }

        private void OnDestroy()
        {
            if (_backdrop != null) _backdrop.onClick.RemoveListener(Close);
            LocalizationRefresh.Unsubscribe(RefreshHeaders);
            if (_slide.isAlive) _slide.Stop();
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

        private void OnDisable()
        {
            // Un slide en vuelo cuando el HUD se apaga dejaría el panel a mitad de camino
            // y a PrimeTween tweeneando un target destruido en el teardown de escena.
            if (_slide.isAlive) _slide.Complete();
        }

        private void Update()
        {
            if (!_isOpen) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        // ==================================================================
        // API
        // ==================================================================

        /// <summary>Lo llama el ícono de contrato.</summary>
        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            // Rebuild al abrir y no al bindear: el contrato cambia durante la run (combos
            // tachados, cambios de daño base) y así el drawer no necesita escuchar nada.
            RebuildRows();

            if (_backdrop != null) _backdrop.gameObject.SetActive(true);
            Slide(_openX);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            // El backdrop se va YA, no al terminar el tween: mientras el panel sale, el
            // jugador ya tiene que poder clickear el tablero.
            if (_backdrop != null) _backdrop.gameObject.SetActive(false);
            Slide(_closedX);
        }

        // ==================================================================
        // Internals
        // ==================================================================

        private void Slide(float targetX)
        {
            if (_panel == null) return;
            if (_slide.isAlive) _slide.Stop();

            if (!Application.isPlaying || DiceUiMotionPrefs.ReducedMotion || _slideSeconds <= 0f)
            {
                SetPanelX(targetX);
                return;
            }

            _slide = Tween.UIAnchoredPositionX(_panel, targetX, _slideSeconds, _slideEase,
                useUnscaledTime: true);
        }

        private void ApplyClosedInstantly()
        {
            _isOpen = false;
            if (_backdrop != null) _backdrop.gameObject.SetActive(false);
            SetPanelX(_closedX);
        }

        private void SetPanelX(float x)
        {
            if (_panel == null) return;
            var pos = _panel.anchoredPosition;
            pos.x = x;
            _panel.anchoredPosition = pos;
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

        private void EnsureRows(int needed)
        {
            while (_rows.Count < needed)
                _rows.Add(Instantiate(_rowPrefab, _rowsContainer));
        }
    }
}
