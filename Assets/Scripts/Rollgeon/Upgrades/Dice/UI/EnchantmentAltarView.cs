using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Upgrades.Dice.UI
{
    /// <summary>
    /// Pantalla mínima de la Sala de Encantamiento — el ticket "Sala mínima" del
    /// brief. Se subscribe a <c>OnEnchantmentAltarActivated</c> y abre el flow de
    /// selección de dado + slot + confirmar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UI mínima.</b> No es la versión final — el ticket explícitamente dice "UI
    /// minimal — no arte final, solo funcional para testing". Layout: lista de
    /// dados arriba, slots del dado seleccionado abajo, cost / preview / confirm
    /// al costado, label de resultado al pie.
    /// </para>
    /// <para>
    /// <b>State.</b> No usamos un FSM — todos los elementos están visibles, se
    /// actualizan según selección. Confirm queda disabled hasta tener slot
    /// seleccionado + oro suficiente.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/Upgrades/Dice/UI/Enchantment Altar View")]
    public sealed class EnchantmentAltarView : MonoBehaviour
    {
        private const string LogPrefix = "[EnchantmentAltarView] ";

        [Title("Root + chrome")]
        [Required, SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _goldLabel;

        [Title("Dice list")]
        [InfoBox("Container donde se instancian los 5 botones (uno por dado del bag). " +
                 "El prefab debe tener EnchantmentItemButtonView.")]
        [Required, SerializeField] private Transform _diceContainer;
        [Required, SerializeField] private EnchantmentItemButtonView _diceButtonPrefab;

        [Title("Slot list (del dado seleccionado)")]
        [Required, SerializeField] private Transform _slotContainer;
        [Required, SerializeField] private EnchantmentItemButtonView _slotButtonPrefab;

        [Title("Selection feedback + actions")]
        [SerializeField] private TextMeshProUGUI _costLabel;
        [SerializeField] private TextMeshProUGUI _facesPreviewLabel;
        [Required, SerializeField] private Button _confirmButton;
        [SerializeField] private Button _closeButton;

        [Title("Result feedback")]
        [SerializeField] private TextMeshProUGUI _resultLabel;

        /// <summary>La pantalla se cerró con el botón — el tutorial encadena el paso siguiente.</summary>
        public event Action OnPanelClosed;

        /// <summary>RectTransform del botón de cerrar — anchor del overlay del tutorial.</summary>
        public bool TryGetCloseButtonRect(out RectTransform rect)
        {
            rect = _closeButton != null ? _closeButton.transform as RectTransform : null;
            return rect != null;
        }

        // ----- Runtime state ----------------------------------------------------
        private bool _subscribed;
        private Guid _currentRoomInstanceId;
        private int _selectedBagIndex = -1;
        private int _selectedSlotIndex = -1;
        private readonly List<EnchantmentItemButtonView> _diceButtons = new List<EnchantmentItemButtonView>();
        private readonly List<EnchantmentItemButtonView> _slotButtons = new List<EnchantmentItemButtonView>();

        // ====================================================================
        // Lifecycle
        // ====================================================================

        private void Awake()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(HandleConfirmClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDestroy()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            NotifyClosedIfPanelOpen();
        }

        /// <summary>
        /// Si el view muere con el panel abierto (cambio de escena, run end) sin pasar
        /// por el botón de cerrar, los suscriptores de <c>OnEnchantmentAltarClosed</c>
        /// (ej. el gate de movimiento de <c>TileClickHandler</c>) quedarían bloqueados
        /// para siempre — este safety emite el Closed que el flujo normal emitiría.
        /// No invoca <see cref="OnPanelClosed"/>: ese callback es exclusivo del cierre
        /// explícito (encadena pasos de tutorial).
        /// </summary>
        private void NotifyClosedIfPanelOpen()
        {
            if (_panelRoot == null || !_panelRoot.activeSelf) return;
            _panelRoot.SetActive(false);
            EventManager.Trigger(EventName.OnEnchantmentAltarClosed);
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnEnchantmentAltarActivated, HandleAltarActivated);
            EventManager.Subscribe(EventName.OnGoldChanged, HandleGoldChanged);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnEnchantmentAltarActivated, HandleAltarActivated);
            EventManager.UnSubscribe(EventName.OnGoldChanged, HandleGoldChanged);
            _subscribed = false;
        }

        // ====================================================================
        // Event handlers (event bus)
        // ====================================================================

        private void HandleAltarActivated(params object[] args)
        {
            if (args == null || args.Length < 2 || !(args[1] is Guid roomId))
            {
                Debug.LogWarning(LogPrefix + "OnEnchantmentAltarActivated con payload inesperado — la mesa no se abre.");
                return;
            }
            _currentRoomInstanceId = roomId;
            OpenPanel();
        }

        private void HandleGoldChanged(params object[] args)
        {
            RefreshGoldLabel();
            RefreshConfirmButtonState();
        }

        // ====================================================================
        // Panel open / close
        // ====================================================================

        private void OpenPanel()
        {
            if (_panelRoot == null)
            {
                Debug.LogWarning(LogPrefix + "_panelRoot null — no se puede abrir la pantalla.");
                return;
            }
            _panelRoot.SetActive(true);

            _selectedBagIndex = -1;
            _selectedSlotIndex = -1;
            if (_resultLabel != null) _resultLabel.text = string.Empty;

            if (_titleLabel != null) _titleLabel.text = "Altar de Encantamiento";

            PopulateDiceButtons();
            ClearSlotButtons();
            RefreshGoldLabel();
            RefreshSelectionLabels();
            RefreshConfirmButtonState();
        }

        private void HandleCloseClicked()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
            EventManager.Trigger(EventName.OnEnchantmentAltarClosed);
            OnPanelClosed?.Invoke();
        }

        // ====================================================================
        // Dice list
        // ====================================================================

        private void PopulateDiceButtons()
        {
            // Limpiar previos
            foreach (var btn in _diceButtons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            _diceButtons.Clear();

            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                || enchSvc == null || !enchSvc.IsReady || enchSvc.Bag == null)
            {
                Debug.LogWarning(LogPrefix + "DiceEnchantmentService no listo — no se pueden listar dados.");
                ShowEmptyStateMessage();
                return;
            }
            if (_diceButtonPrefab == null || _diceContainer == null)
            {
                // Este return era 100% mudo — un ref desconectado en el prefab dejaba
                // la mesa abierta y vacía sin ninguna pista (bug de la mesa del tutorial).
                Debug.LogError(LogPrefix + "_diceButtonPrefab o _diceContainer sin asignar — la lista de dados queda vacía.", this);
                ShowEmptyStateMessage();
                return;
            }

            var bag = enchSvc.Bag;
            for (int b = 0; b < bag.Dice.Count; b++)
            {
                int bagIndex = b; // capture
                var dice = bag.Dice[b];
                int total = dice.MaxEnchantmentSlots();
                int used = CountUsedSlots(bag, b);

                var slots = bag.GetEnchantments(b);

                var btn = Instantiate(_diceButtonPrefab, _diceContainer);
                btn.Configure(
                    label: $"{dice} (slot {b + 1})",
                    subLabel: $"{used}/{total} cupos",
                    onClick: () => HandleDiceClicked(bagIndex),
                    tooltipProvider: () => BuildDiceTooltip(slots));
                _diceButtons.Add(btn);
            }

            if (_diceButtons.Count == 0)
            {
                Debug.LogWarning(LogPrefix + "Populate terminó con 0 botones de dado — bag vacío?");
                ShowEmptyStateMessage();
            }
        }

        /// <summary>
        /// Mensaje visible cuando la lista de dados no se pudo poblar — la mesa nunca
        /// debe abrirse vacía y muda: sin esto el jugador queda mirando un panel sin
        /// información y sin saber que es un bug (y en el tutorial, sin poder avanzar).
        /// </summary>
        private void ShowEmptyStateMessage()
        {
            if (_resultLabel == null) return;
            _resultLabel.text = "<color=#ff8888>No se pudieron cargar los dados — cerrá la mesa y volvé a intentar.</color>";
        }

        private static int CountUsedSlots(RuntimeDiceBag bag, int bagIndex)
        {
            var slots = bag.GetEnchantments(bagIndex);
            int used = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null) used++;
            }
            return used;
        }

        private void HandleDiceClicked(int bagIndex)
        {
            _selectedBagIndex = bagIndex;
            _selectedSlotIndex = -1;
            for (int i = 0; i < _diceButtons.Count; i++)
            {
                _diceButtons[i].SetSelected(i == bagIndex);
            }
            PopulateSlotButtons(bagIndex);
            RefreshSelectionLabels();
            RefreshConfirmButtonState();
        }

        // ====================================================================
        // Slot list (del dado seleccionado)
        // ====================================================================

        private void PopulateSlotButtons(int bagIndex)
        {
            ClearSlotButtons();

            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                || enchSvc?.Bag == null)
            {
                Debug.LogWarning(LogPrefix + "DiceEnchantmentService/Bag no disponible — slots del dado no se pueden listar.");
                return;
            }
            if (_slotButtonPrefab == null || _slotContainer == null)
            {
                Debug.LogError(LogPrefix + "_slotButtonPrefab o _slotContainer sin asignar — lista de slots vacía.", this);
                return;
            }

            var bag = enchSvc.Bag;
            int slotCount = bag.GetEnchantmentSlotCount(bagIndex);
            for (int s = 0; s < slotCount; s++)
            {
                int slotIndex = s; // capture
                var existing = bag.GetEnchantmentAt(bagIndex, s);

                string label = $"Cupo {s + 1}";
                string subLabel = existing != null
                    ? $"{Rollgeon.Localization.LocalizedContent.Ui("altar.enchanted", "Encantado")}: {Rollgeon.Localization.LocalizedContent.Name(existing.UpgradeId, existing.DisplayName)}"
                    : Rollgeon.Localization.LocalizedContent.Ui("altar.empty", "Vacío");

                // Capturamos el SO (no el índice) para el tooltip — los botones se
                // reconstruyen en cada Populate (incluido tras un apply), así que no
                // hay riesgo de que quede stale apuntando a un encantamiento viejo.
                Func<string> tooltipProvider = existing != null
                    ? () => BuildEnchantmentTooltip(existing)
                    : null;

                var btn = Instantiate(_slotButtonPrefab, _slotContainer);
                btn.Configure(label, subLabel, () => HandleSlotClicked(slotIndex), tooltipProvider);
                _slotButtons.Add(btn);
            }
        }

        // ====================================================================
        // Tooltip text builders — puros y testeables sin UI real (CNF-011)
        // ====================================================================

        /// <summary>
        /// Texto de hover para un slot con encantamiento aplicado: nombre en bold +
        /// descripción reducida. <c>public</c> (no <c>internal</c>) porque el asmdef
        /// de tests de Dice no tiene <c>InternalsVisibleTo</c> hacia el assembly
        /// raíz — ver nota de deviation en el resumen de la tarea.
        /// </summary>
        public static string BuildEnchantmentTooltip(EnchantmentSO ench)
        {
            if (ench == null) return string.Empty;
            string name = Rollgeon.Localization.LocalizedContent.Name(ench.UpgradeId, !string.IsNullOrEmpty(ench.DisplayName) ? ench.DisplayName : ench.UpgradeId);
            string desc = Rollgeon.Localization.LocalizedContent.Description(ench.UpgradeId, ench.Description);
            return string.IsNullOrEmpty(desc)
                ? $"<b>{name}</b>"
                : $"<b>{name}</b>\n<size=80%>{desc}</size>";
        }

        /// <summary>
        /// Texto de hover para un dado: una línea por cupo ocupado
        /// ("• Nombre — Descripción"), o placeholder si no tiene ninguno.
        /// </summary>
        public static string BuildDiceTooltip(IReadOnlyList<EnchantmentSO> slots)
        {
            const string none = "Sin encantamientos";
            if (slots == null || slots.Count == 0) return none;

            var lines = new List<string>();
            for (int i = 0; i < slots.Count; i++)
            {
                var ench = slots[i];
                if (ench == null) continue;
                string name = Rollgeon.Localization.LocalizedContent.Name(ench.UpgradeId, !string.IsNullOrEmpty(ench.DisplayName) ? ench.DisplayName : ench.UpgradeId);
                lines.Add($"• {name} — {Rollgeon.Localization.LocalizedContent.Description(ench.UpgradeId, ench.Description)}");
            }
            return lines.Count > 0 ? string.Join("\n", lines) : none;
        }

        private void ClearSlotButtons()
        {
            foreach (var btn in _slotButtons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            _slotButtons.Clear();
        }

        private void HandleSlotClicked(int slotIndex)
        {
            _selectedSlotIndex = slotIndex;
            for (int i = 0; i < _slotButtons.Count; i++)
            {
                _slotButtons[i].SetSelected(i == slotIndex);
            }
            RefreshSelectionLabels();
            RefreshConfirmButtonState();
        }

        // ====================================================================
        // Selection feedback labels
        // ====================================================================

        private void RefreshSelectionLabels()
        {
            int cost = ResolveCurrentCost();
            if (_costLabel != null)
            {
                _costLabel.text = _selectedBagIndex >= 0 && _selectedSlotIndex >= 0
                    ? $"{Rollgeon.Localization.LocalizedContent.Ui("altar.cost", "Costo")}: {cost} {Rollgeon.Localization.LocalizedContent.Ui("gold.unit", "oro")}"
                    : $"{Rollgeon.Localization.LocalizedContent.Ui("altar.cost", "Costo")}: —";
            }
            if (_facesPreviewLabel != null)
            {
                _facesPreviewLabel.text = _selectedBagIndex >= 0
                    ? Rollgeon.Localization.LocalizedContent.Ui("altar.current_faces", "Caras actuales") + ": " + FormatFaces(ComputeAllowedFacesForSelection())
                    : string.Empty;
            }
        }

        private void RefreshGoldLabel()
        {
            if (_goldLabel == null) return;
            if (ServiceLocator.TryGetService<IEconomyService>(out var economy) && economy != null)
            {
                _goldLabel.text = $"{Rollgeon.Localization.LocalizedContent.Ui("altar.gold", "Oro")}: {economy.CurrentGold}";
            }
        }

        private void RefreshConfirmButtonState()
        {
            if (_confirmButton == null) return;
            bool valid = _selectedBagIndex >= 0 && _selectedSlotIndex >= 0;
            bool canAfford = valid && CanAffordCurrentSelection();
            _confirmButton.interactable = valid && canAfford;
        }

        private bool CanAffordCurrentSelection()
        {
            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null) return false;
            return economy.CanAfford(ResolveCurrentCost());
        }

        private int ResolveCurrentCost()
        {
            if (_selectedBagIndex < 0 || _selectedSlotIndex < 0) return 0;
            if (!ServiceLocator.TryGetService<IEnchantmentRoomService>(out var roomSvc) || roomSvc == null) return 0;
            return roomSvc.ResolveCost(_selectedBagIndex, _selectedSlotIndex);
        }

        private IReadOnlyCollection<int> ComputeAllowedFacesForSelection()
        {
            if (_selectedBagIndex < 0) return Array.Empty<int>();
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc) || enchSvc == null)
                return Array.Empty<int>();
            return enchSvc.ComputeAllowedFaces(_selectedBagIndex);
        }

        private static string FormatFaces(IReadOnlyCollection<int> faces)
        {
            if (faces == null || faces.Count == 0) return "—";
            var sorted = faces.OrderBy(f => f);
            return string.Join(", ", sorted);
        }

        // ====================================================================
        // Confirm flow
        // ====================================================================

        private void HandleConfirmClicked()
        {
            if (_selectedBagIndex < 0 || _selectedSlotIndex < 0) return;
            if (!ServiceLocator.TryGetService<IEnchantmentRoomService>(out var roomSvc) || roomSvc == null)
            {
                Debug.LogWarning(LogPrefix + "RoomService no registrado — no se puede confirmar.");
                return;
            }

            var result = roomSvc.PerformEnchantment(_currentRoomInstanceId, _selectedBagIndex, _selectedSlotIndex);
            ShowResult(result);

            // Refrescar UI tras el apply — slots cambiaron, oro cambió.
            PopulateDiceButtons();
            if (_selectedBagIndex >= 0) PopulateSlotButtons(_selectedBagIndex);
            // La selección del slot se mantiene por ergonomía (re-enchant del mismo slot).
            for (int i = 0; i < _diceButtons.Count; i++)
                _diceButtons[i].SetSelected(i == _selectedBagIndex);
            for (int i = 0; i < _slotButtons.Count; i++)
                _slotButtons[i].SetSelected(i == _selectedSlotIndex);
            RefreshGoldLabel();
            RefreshSelectionLabels();
            RefreshConfirmButtonState();
        }

        private void ShowResult(EnchantmentRollResult result)
        {
            if (_resultLabel == null) return;
            if (!result.Success)
            {
                _resultLabel.text = $"<color=#ff8888>{result.ErrorMessage}</color>";
                return;
            }
            var rolled = result.RolledEnchantment;
            string name = rolled != null ? Rollgeon.Localization.LocalizedContent.Name(rolled.UpgradeId, rolled.DisplayName ?? rolled.UpgradeId) : "?";
            string faces = FormatFaces(result.ProjectedFaces);
            // Descripción inline en el resultado (CNF-011) — el jugador ve qué hace el
            // encantamiento sin tener que ir a buscarlo de nuevo en la lista de slots.
            // Formato compacto en 2 líneas: el label vive al pie de un panel que ya
            // ocupa la pantalla completa — 4 líneas desbordaban por debajo.
            string rolledDesc = rolled != null ? Rollgeon.Localization.LocalizedContent.Description(rolled.UpgradeId, rolled.Description) : string.Empty;
            string descPart = string.IsNullOrEmpty(rolledDesc)
                ? string.Empty
                : $" — <size=80%><i>{rolledDesc}</i></size>";
            _resultLabel.text =
                $"<color=#88ff88>Recibiste:</color> <b>{name}</b>{descPart}\n" +
                $"Caras del dado: {faces}  ·  Oro gastado: {result.GoldPaid}";
        }
    }
}
