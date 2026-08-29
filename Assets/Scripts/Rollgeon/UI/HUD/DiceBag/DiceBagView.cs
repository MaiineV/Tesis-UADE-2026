using System.Collections.Generic;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Localization;
using Rollgeon.UI.Screens;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.UI;
// EnchantmentFaceCardView es lo único que se reusa de la mesa: es una card de arte, no
// lógica de pantalla.
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.DiceBag
{
    /// <summary>
    /// Contenido del panel de la bolsa de dados (mock "new dice bag drawer"): los
    /// dados de la run (solo sprite, el seleccionado resaltado), las caras del
    /// elegido en una fila responsive, y el acordeón de encantamientos — filas
    /// "Nombre - Tipo" cuya descripción se expande de a una.
    /// </summary>
    /// <remarks>
    /// SOLO INFORMATIVO: acá no se encanta nada, así que no hay costo, ni confirmar,
    /// ni oro. Por eso vive en el HUD y está disponible en combate y en exploración —
    /// es la ayuda visual de "qué tengo".
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Bag View")]
    [RequireComponent(typeof(SlidingDrawer))]
    public class DiceBagView : MonoBehaviour
    {
        [Title("Dados")]
        [SerializeField, Required] private RectTransform _diceContainer;
        [SerializeField, Required] private DiceBagDieCardView _dieCardPrefab;

        [Title("Caras")]
        [SerializeField, Required] private RectTransform _facesContainer;
        [SerializeField, Required] private EnchantmentFaceCardView _faceCardPrefab;

        [Title("Encantamientos")]
        [SerializeField, Required] private RectTransform _enchantListContainer;
        [SerializeField, Required] private DiceBagEnchantRowView _enchantRowPrefab;
        [SerializeField] private TextMeshProUGUI _noEnchantmentsLabel;

        [Title("Textos")]
        [SerializeField] private TextMeshProUGUI _titleLabel;

        [Title("Settings")]
        [SerializeField, Required]
        [Tooltip("Sprites por tipo de dado — los mismos que la build selection y la mesa.")]
        private DiceBuildUiSettingsSO _diceUiSettings;

        private readonly List<DiceBagDieCardView> _dieCards = new();
        private readonly List<EnchantmentFaceCardView> _faceCards = new();
        private readonly List<DiceBagEnchantRowView> _enchantRows = new();
        private readonly List<EnchantmentSO> _selectedDieEnchantments = new();

        private SlidingDrawer _drawer;
        private int _selectedDie = -1;
        private int _expandedRow = -1;

        private void Awake()
        {
            if (_drawer == null) TryGetComponent(out _drawer);
            if (_drawer != null) _drawer.Opened += Rebuild;

            RefreshCaptions();
            LocalizationRefresh.Subscribe(RefreshCaptions);
        }

        private void OnDestroy()
        {
            if (_drawer != null) _drawer.Opened -= Rebuild;
            LocalizationRefresh.Unsubscribe(RefreshCaptions);
        }

        private void RefreshCaptions()
        {
            if (_titleLabel != null)
                _titleLabel.text = LocalizedContent.Ui(DiceBagTextKeys.Title, "Bolsa de Dados");
        }

        // ==================================================================
        // Rebuild
        // ==================================================================

        /// <summary>
        /// Repuebla con la bolsa actual. Se llama al abrir, así que refleja los
        /// encantamientos comprados desde la última vez sin escuchar nada.
        /// </summary>
        public void Rebuild()
        {
            var bag = ResolveBag();
            int count = bag?.Dice?.Count ?? 0;

            EnsureDieCards(count);

            for (int i = 0; i < _dieCards.Count; i++)
            {
                bool used = i < count;
                _dieCards[i].gameObject.SetActive(used);
                if (!used) continue;

                int index = i; // capture
                var type = bag.Dice[i];
                _dieCards[i].Bind(
                    _diceUiSettings != null ? _diceUiSettings.GetSprite(type) : null,
                    type.MaxFace(),
                    () => SelectDie(index));
                // Mismo holo que los dados encantados de la zona de combate: se identifica
                // de un vistazo cuáles tienen al menos un encantamiento.
                _dieCards[i].SetEnchantVisual(
                    DiceEnchantVisualResolver.ResolvePrimary(bag.GetEnchantments(i)));
            }

            // Se mantiene la selección si el dado sigue existiendo; si no, el primero. Sin
            // dados no hay nada elegido y las secciones de abajo quedan vacías.
            int target = _selectedDie >= 0 && _selectedDie < count ? _selectedDie : (count > 0 ? 0 : -1);
            SelectDie(target);
        }

        private void SelectDie(int index)
        {
            _selectedDie = index;
            _expandedRow = -1;

            for (int i = 0; i < _dieCards.Count; i++)
                _dieCards[i].SetSelected(i == index);

            RebuildFaces();
            RebuildEnchantList();
        }

        private void RebuildFaces()
        {
            var bag = ResolveBag();
            int count = 0;
            Sprite sprite = null;

            if (_selectedDie >= 0 && bag != null && _selectedDie < bag.Dice.Count)
            {
                var type = bag.Dice[_selectedDie];
                count = type.MaxFace();
                sprite = _diceUiSettings != null ? _diceUiSettings.GetSprite(type) : null;
            }

            ApplyResponsiveCellSize(count);
            EnsureFaceCards(count);

            for (int i = 0; i < _faceCards.Count; i++)
            {
                bool used = i < count;
                _faceCards[i].gameObject.SetActive(used);
                // Las caras de un dado son 1..MaxFace, así que el índice + 1 ES la cara.
                if (used) _faceCards[i].Set(sprite, i + 1);
            }
        }

        // Más caras = celdas más chicas, siempre una fila que entra en la banda.
        private void ApplyResponsiveCellSize(int faces)
        {
            if (_facesContainer == null || faces <= 0) return;
            if (!_facesContainer.TryGetComponent<GridLayoutGroup>(out var grid)) return;

            float cell = DiceBagFaceLayout.CellSize(faces, _facesContainer.rect.width, grid.spacing.x);
            grid.cellSize = new Vector2(cell, cell);
        }

        // ==================================================================
        // Acordeón de encantamientos
        // ==================================================================

        private void RebuildEnchantList()
        {
            // Solo los aplicados — los nulls son tombstones de removes y no se dibujan.
            _selectedDieEnchantments.Clear();
            if (_selectedDie >= 0)
            {
                var slots = ResolveBag()?.GetEnchantments(_selectedDie);
                if (slots != null)
                {
                    for (int i = 0; i < slots.Count; i++)
                        if (slots[i] != null) _selectedDieEnchantments.Add(slots[i]);
                }
            }

            int count = _selectedDieEnchantments.Count;
            EnsureEnchantRows(count);

            for (int i = 0; i < _enchantRows.Count; i++)
            {
                bool used = i < count;
                _enchantRows[i].gameObject.SetActive(used);
                if (!used) continue;

                int index = i; // capture
                _enchantRows[i].Bind(_selectedDieEnchantments[i], () => OnRowClicked(index));
            }

            if (_noEnchantmentsLabel != null)
            {
                bool empty = _selectedDie >= 0 && count == 0;
                _noEnchantmentsLabel.gameObject.SetActive(empty);
                if (empty)
                    _noEnchantmentsLabel.text =
                        LocalizedContent.Ui(DiceBagTextKeys.NoEnchantments, "Sin encantamientos.");
            }
        }

        /// <summary>
        /// Acordeón exclusivo: click abre la fila y cierra la que estuviera abierta;
        /// re-click sobre la abierta la cierra.
        /// </summary>
        private void OnRowClicked(int index)
        {
            _expandedRow = _expandedRow == index ? -1 : index;
            for (int i = 0; i < _enchantRows.Count; i++)
                _enchantRows[i].SetExpanded(i == _expandedRow);
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private static RuntimeDiceBag ResolveBag()
        {
            return ServiceLocator.TryGetService<IDiceEnchantmentService>(out var svc)
                   && svc != null && svc.IsReady
                ? svc.Bag
                : null;
        }

        // Los tres pools se reusan y solo se apagan: el panel se repuebla en cada apertura.
        private void EnsureDieCards(int needed)
        {
            while (_dieCards.Count < needed)
                _dieCards.Add(Instantiate(_dieCardPrefab, _diceContainer));
        }

        private void EnsureFaceCards(int needed)
        {
            while (_faceCards.Count < needed)
                _faceCards.Add(Instantiate(_faceCardPrefab, _facesContainer));
        }

        private void EnsureEnchantRows(int needed)
        {
            while (_enchantRows.Count < needed)
                _enchantRows.Add(Instantiate(_enchantRowPrefab, _enchantListContainer));
        }
    }
}
