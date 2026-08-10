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

namespace Rollgeon.UI.HUD.DiceBag
{
    /// <summary>
    /// Contenido del panel de la bolsa de dados: los dados de la run, los cupos de
    /// encantamiento del que esté elegido, sus caras y la descripción.
    /// </summary>
    /// <remarks>
    /// Misma estructura que la mesa de encantamientos, pero SOLO INFORMATIVO: acá no se
    /// encanta nada, así que no hay costo, ni confirmar, ni oro. Por eso vive en el HUD y
    /// está disponible en combate y en exploración — es la ayuda visual de "qué tengo".
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Bag View")]
    [RequireComponent(typeof(SlidingDrawer))]
    public class DiceBagView : MonoBehaviour
    {
        [Title("Dados")]
        [SerializeField, Required] private RectTransform _diceContainer;
        [SerializeField, Required] private DiceBagDieCardView _dieCardPrefab;

        [Title("Cupos")]
        [SerializeField, Required] private RectTransform _slotsContainer;
        [SerializeField, Required] private DiceBagSlotView _slotPrefab;

        [Title("Caras")]
        [SerializeField, Required] private RectTransform _facesContainer;
        [SerializeField, Required] private EnchantmentFaceCardView _faceCardPrefab;

        [Title("Textos")]
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _slotsCaptionLabel;
        [SerializeField, Required] private TextMeshProUGUI _descriptionLabel;

        [Title("Settings")]
        [SerializeField, Required]
        [Tooltip("Sprites por tipo de dado — los mismos que la build selection y la mesa.")]
        private DiceBuildUiSettingsSO _diceUiSettings;

        private readonly List<DiceBagDieCardView> _dieCards = new();
        private readonly List<DiceBagSlotView> _slotViews = new();
        private readonly List<EnchantmentFaceCardView> _faceCards = new();

        private SlidingDrawer _drawer;
        private int _selectedDie = -1;
        private int _selectedSlot = -1;

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
            if (_slotsCaptionLabel != null)
                _slotsCaptionLabel.text = LocalizedContent.Ui(DiceBagTextKeys.SlotsCaption, "Cupos de encantamiento");
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
                    CountUsedSlots(bag, i),
                    type.MaxEnchantmentSlots(),
                    LocalizedContent.Ui(DiceBagTextKeys.SlotsSuffix, "cupos"),
                    () => SelectDie(index));
            }

            // Se mantiene la selección si el dado sigue existiendo; si no, el primero. Sin
            // dados no hay nada elegido y las secciones de abajo quedan vacías.
            int target = _selectedDie >= 0 && _selectedDie < count ? _selectedDie : (count > 0 ? 0 : -1);
            SelectDie(target);
        }

        private void SelectDie(int index)
        {
            _selectedDie = index;
            _selectedSlot = -1;

            for (int i = 0; i < _dieCards.Count; i++)
                _dieCards[i].SetSelected(i == index);

            RebuildSlots();
            RebuildFaces();
            RefreshDescription();
        }

        private void RebuildSlots()
        {
            var bag = ResolveBag();
            var slots = _selectedDie >= 0 ? bag?.GetEnchantments(_selectedDie) : null;
            int count = slots?.Count ?? 0;

            EnsureSlots(count);

            for (int i = 0; i < _slotViews.Count; i++)
            {
                bool used = i < count;
                _slotViews[i].gameObject.SetActive(used);
                if (!used) continue;

                int index = i; // capture
                _slotViews[i].Bind(slots[i] != null, () => SelectSlot(index));
                _slotViews[i].SetSelected(false);
            }

            if (_slotsCaptionLabel != null)
                _slotsCaptionLabel.gameObject.SetActive(count > 0);
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

            EnsureFaceCards(count);

            for (int i = 0; i < _faceCards.Count; i++)
            {
                bool used = i < count;
                _faceCards[i].gameObject.SetActive(used);
                // Las caras de un dado son 1..MaxFace, así que el índice + 1 ES la cara.
                if (used) _faceCards[i].Set(sprite, i + 1);
            }
        }

        private void SelectSlot(int index)
        {
            _selectedSlot = index;
            for (int i = 0; i < _slotViews.Count; i++)
                _slotViews[i].SetSelected(i == index);
            RefreshDescription();
        }

        /// <summary>
        /// Describe el cupo elegido; sin cupo elegido, resume los encantamientos del dado.
        /// </summary>
        private void RefreshDescription()
        {
            if (_descriptionLabel == null) return;

            var bag = ResolveBag();
            if (_selectedDie < 0 || bag == null)
            {
                _descriptionLabel.text = string.Empty;
                return;
            }

            var slots = bag.GetEnchantments(_selectedDie);

            if (_selectedSlot >= 0 && slots != null && _selectedSlot < slots.Count)
            {
                var ench = slots[_selectedSlot];
                _descriptionLabel.text = ench != null
                    ? DescribeEnchantment(ench)
                    : LocalizedContent.Ui(DiceBagTextKeys.EmptySlot, "Cupo libre.");
                return;
            }

            // Sin cupo elegido: el resumen del dado.
            _descriptionLabel.text = BuildSummary(slots);
        }

        /// <summary>
        /// Resumen de los encantamientos del dado, una línea por cupo ocupado.
        /// </summary>
        /// <remarks>
        /// Propio y no <c>EnchantmentAltarView.BuildDiceTooltip</c>: aquel tiene el "Sin
        /// encantamientos" hardcodeado en español, y además ata este HUD a una pantalla con
        /// la que no comparte ciclo de vida.
        /// </remarks>
        private static string BuildSummary(IReadOnlyList<EnchantmentSO> slots)
        {
            var lines = new List<string>();
            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] == null) continue;
                    lines.Add("• " + DescribeEnchantment(slots[i]).Replace("\n", " — "));
                }
            }

            return lines.Count > 0
                ? string.Join("\n", lines)
                : LocalizedContent.Ui(DiceBagTextKeys.NoEnchantments, "Sin encantamientos.");
        }

        private static string DescribeEnchantment(EnchantmentSO ench)
        {
            string name = LocalizedContent.Name(ench.UpgradeId,
                !string.IsNullOrEmpty(ench.DisplayName) ? ench.DisplayName : ench.UpgradeId);
            string body = LocalizedContent.Description(ench.UpgradeId, ench.Description ?? string.Empty);
            return string.IsNullOrEmpty(body) ? $"<b>{name}</b>" : $"<b>{name}</b>\n{body}";
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

        private static int CountUsedSlots(RuntimeDiceBag bag, int bagIndex)
        {
            var slots = bag?.GetEnchantments(bagIndex);
            if (slots == null) return 0;

            int used = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] != null) used++;
            return used;
        }

        // Los tres pools se reusan y solo se apagan: el panel se repuebla en cada apertura.
        private void EnsureDieCards(int needed)
        {
            while (_dieCards.Count < needed)
                _dieCards.Add(Instantiate(_dieCardPrefab, _diceContainer));
        }

        private void EnsureSlots(int needed)
        {
            while (_slotViews.Count < needed)
                _slotViews.Add(Instantiate(_slotPrefab, _slotsContainer));
        }

        private void EnsureFaceCards(int needed)
        {
            while (_faceCards.Count < needed)
                _faceCards.Add(Instantiate(_faceCardPrefab, _facesContainer));
        }
    }
}
