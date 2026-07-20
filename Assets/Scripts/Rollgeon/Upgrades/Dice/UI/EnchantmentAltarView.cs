using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using PrimeTween;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.UI.HUD.DiceAnim;
using Rollgeon.UI.Screens;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LocalizedContent = Rollgeon.Localization.LocalizedContent;

namespace Rollgeon.Upgrades.Dice.UI
{
    /// <summary>
    /// Pantalla de la Sala de Encantamiento. Se subscribe a
    /// <c>OnEnchantmentAltarActivated</c> y abre el flow de selección de dado +
    /// cupo + confirmar, con el layout del mock del altar: fondo del sheet de UI,
    /// dados del inventario como cards (sprite + nº de caras), cupos como selects
    /// con la descripción del encantamiento abajo y pila de oro completa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>State.</b> No usamos un FSM — todos los elementos están visibles, se
    /// actualizan según selección. Confirm queda disabled hasta tener slot
    /// seleccionado + oro suficiente.
    /// </para>
    /// <para>
    /// <b>Cierre.</b> <c>OnEnchantmentAltarClosed</c> se emite SIEMPRE sincrónico
    /// (el gate de movimiento de <c>TileClickHandler</c> lo espera); el tween de
    /// salida es puramente cosmético y corre después.
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

        [Tooltip("Pila de oro completa del panel — el label de cantidad es _goldLabel.")]
        [SerializeField, Optional] private AltarGoldDisplayView _goldDisplay;

        [Tooltip("Pila de oro junto al costo — mismo icono que la del oro.")]
        [SerializeField, Optional] private AltarGoldDisplayView _costGoldDisplay;

        [Title("Dice list")]
        [InfoBox("Container donde se instancian los 5 botones (uno por dado del bag). " +
                 "El prefab debe tener EnchantmentItemButtonView.")]
        [Required, SerializeField] private Transform _diceContainer;
        [Required, SerializeField] private EnchantmentItemButtonView _diceButtonPrefab;

        [Title("Slot list (del dado seleccionado)")]
        [Required, SerializeField] private Transform _slotContainer;
        [Required, SerializeField] private EnchantmentItemButtonView _slotButtonPrefab;

        [Tooltip("Descripción del encantamiento del cupo seleccionado, debajo de la fila de cupos.")]
        [SerializeField, Optional] private TextMeshProUGUI _slotDescriptionLabel;

        [Title("Caras actuales")]
        [Tooltip("Container de mini-cards de caras. Sin asignar, cae al texto de _facesPreviewLabel.")]
        [SerializeField, Optional] private Transform _facesContainer;
        [SerializeField, Optional] private EnchantmentFaceCardView _faceCardPrefab;

        [Title("Captions (opcionales — el view los localiza al abrir)")]
        [SerializeField, Optional] private TextMeshProUGUI _diceCaptionLabel;
        [SerializeField, Optional] private TextMeshProUGUI _slotCaptionLabel;
        [SerializeField, Optional] private TextMeshProUGUI _facesCaptionLabel;

        [Title("Selection feedback + actions")]
        [SerializeField] private TextMeshProUGUI _costLabel;
        [SerializeField] private TextMeshProUGUI _facesPreviewLabel;
        [Required, SerializeField] private Button _confirmButton;
        [SerializeField] private Button _closeButton;

        [Title("Result feedback")]
        [SerializeField] private TextMeshProUGUI _resultLabel;

        [Title("Settings")]
        [Tooltip("Sprites por tipo de dado — mismos que la build selection.")]
        [SerializeField, Optional] private DiceBuildUiSettingsSO _diceUiSettings;

        [Tooltip("Tuning de tweens del panel. Sin asignar, la view abre/cierra sin juice.")]
        [SerializeField, Optional] private EnchantmentAltarUiSettingsSO _uiSettings;

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
        private bool _closing;
        private RectTransform _panelRect;
        private CanvasGroup _panelCanvasGroup;
        private CanvasGroup _resultCanvasGroup;
        private readonly List<EnchantmentItemButtonView> _diceButtons = new List<EnchantmentItemButtonView>();
        private readonly List<EnchantmentItemButtonView> _slotButtons = new List<EnchantmentItemButtonView>();
        private readonly List<EnchantmentFaceCardView> _faceCards = new List<EnchantmentFaceCardView>();

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
            StopPanelTweens();
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
            RestorePanelPose();

            // Si el disable llegó en medio del tween-out del botón de cerrar, el
            // Closed ya se emitió sincrónico en HandleCloseClicked — no duplicar.
            if (_closing)
            {
                _closing = false;
                return;
            }
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
            RefreshGoldDisplay(animate: true);
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
            // Reabrir en medio de un tween-out: matar tweens y arrancar limpio.
            StopPanelTweens();
            RestorePanelPose();
            _closing = false;
            _panelRoot.SetActive(true);

            _selectedBagIndex = -1;
            _selectedSlotIndex = -1;
            if (_resultLabel != null) _resultLabel.text = string.Empty;

            if (_titleLabel != null)
                _titleLabel.text = LocalizedContent.Ui("altar.title", "Altar de Encantamiento");
            ApplyCaptions();

            PopulateDiceButtons();
            ClearSlotButtons();
            RefreshGoldDisplay(animate: true);
            RefreshSelectionLabels();
            RefreshConfirmButtonState();

            PlayOpenJuice();
            PlayCardsEntrance(_diceButtons);
        }

        private void HandleCloseClicked()
        {
            if (_closing) return;

            // El gate de movimiento espera el Closed sincrónico — el tween de salida
            // es solo cosmético y corre después de avisar.
            EventManager.Trigger(EventName.OnEnchantmentAltarClosed);
            OnPanelClosed?.Invoke();

            if (!CanJuice() || _panelRoot == null || !_panelRoot.activeSelf)
            {
                if (_panelRoot != null) _panelRoot.SetActive(false);
                return;
            }

            _closing = true;
            EnsurePanelRefs();
            Tween.StopAll(onTarget: _panelRect);
            Tween.StopAll(onTarget: _panelCanvasGroup);
            _panelCanvasGroup.blocksRaycasts = false; // el fade-out no debe comer clicks
            Tween.Scale(_panelRect, Vector3.one * _uiSettings.OpenScaleFrom,
                _uiSettings.CloseDuration, _uiSettings.CloseEase, useUnscaledTime: true);
            Tween.Alpha(_panelCanvasGroup, 0f, _uiSettings.CloseDuration, _uiSettings.CloseEase,
                    useUnscaledTime: true)
                .OnComplete(this, self => self.FinishClose());
        }

        private void FinishClose()
        {
            _closing = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);
            RestorePanelPose();
        }

        private void PlayOpenJuice()
        {
            if (!CanJuice()) return;
            EnsurePanelRefs();
            Tween.StopAll(onTarget: _panelRect);
            Tween.StopAll(onTarget: _panelCanvasGroup);
            _panelRect.localScale = Vector3.one * _uiSettings.OpenScaleFrom;
            _panelCanvasGroup.alpha = 0f;
            Tween.Scale(_panelRect, Vector3.one, _uiSettings.OpenDuration, _uiSettings.OpenEase,
                useUnscaledTime: true);
            Tween.Alpha(_panelCanvasGroup, 1f, _uiSettings.OpenDuration, Ease.OutQuad,
                useUnscaledTime: true);
        }

        private bool CanJuice()
        {
            return _uiSettings != null
                   && Application.isPlaying
                   && !DiceUiMotionPrefs.ReducedMotion;
        }

        private void EnsurePanelRefs()
        {
            if (_panelRoot == null) return;
            if (_panelRect == null) _panelRect = _panelRoot.transform as RectTransform;
            if (_panelCanvasGroup == null)
            {
                _panelCanvasGroup = _panelRoot.GetComponent<CanvasGroup>();
                if (_panelCanvasGroup == null) _panelCanvasGroup = _panelRoot.AddComponent<CanvasGroup>();
            }
        }

        private void StopPanelTweens()
        {
            if (_panelRect != null) Tween.StopAll(onTarget: _panelRect);
            if (_panelCanvasGroup != null) Tween.StopAll(onTarget: _panelCanvasGroup);
            if (_resultCanvasGroup != null) Tween.StopAll(onTarget: _resultCanvasGroup);
        }

        private void RestorePanelPose()
        {
            if (_panelRect != null) _panelRect.localScale = Vector3.one;
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = 1f;
                _panelCanvasGroup.blocksRaycasts = true;
            }
            if (_resultCanvasGroup != null) _resultCanvasGroup.alpha = 1f;
        }

        private void ApplyCaptions()
        {
            if (_diceCaptionLabel != null)
                _diceCaptionLabel.text = LocalizedContent.Ui("altar.your_dice", "Tus dados:");
            if (_slotCaptionLabel != null)
                _slotCaptionLabel.text = LocalizedContent.Ui("altar.choose_slot", "Elige el cupo del dado seleccionado:");
            if (_facesCaptionLabel != null)
                _facesCaptionLabel.text = LocalizedContent.Ui("altar.current_faces", "Caras actuales") + ":";
            SetButtonLabel(_confirmButton, LocalizedContent.Ui("altar.confirm", "Confirmar"));
            SetButtonLabel(_closeButton, LocalizedContent.Ui("altar.close", "Cerrar"));
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
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
                // La card se identifica por el sprite + nº de caras — sin label de
                // texto del tipo ni del slot de equipamiento (decisión de diseño:
                // los dados van en orden de inventario, el slot no se muestra).
                btn.Configure(
                    label: string.Empty,
                    subLabel: $"{used}/{total} {LocalizedContent.Ui("altar.slots_suffix", "cupos")}",
                    onClick: () => HandleDiceClicked(bagIndex),
                    tooltipProvider: () => BuildDiceTooltip(slots));
                btn.SetDiceIcon(
                    _diceUiSettings != null ? _diceUiSettings.GetSprite(dice) : null,
                    dice.MaxFace().ToString());
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
            PlayCardsEntrance(_slotButtons);
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

                string label = $"{LocalizedContent.Ui("altar.slot", "Cupo")} {s + 1}";
                string subLabel = existing != null
                    ? $"{LocalizedContent.Ui("altar.enchantment", "Encantamiento")}:\n{LocalizedContent.Name(existing.UpgradeId, existing.DisplayName)}"
                    : LocalizedContent.Ui("altar.empty", "Vacío");

                // Capturamos el SO (no el índice) para el tooltip — los botones se
                // reconstruyen en cada Populate (incluido tras un apply), así que no
                // hay riesgo de que quede stale apuntando a un encantamiento viejo.
                Func<string> tooltipProvider = existing != null
                    ? () => BuildEnchantmentTooltip(existing)
                    : null;

                var btn = Instantiate(_slotButtonPrefab, _slotContainer);
                btn.Configure(label, subLabel, () => HandleSlotClicked(slotIndex), tooltipProvider);
                btn.SetMuted(existing == null);
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
            string name = LocalizedContent.Name(ench.UpgradeId, !string.IsNullOrEmpty(ench.DisplayName) ? ench.DisplayName : ench.UpgradeId);
            string desc = LocalizedContent.Description(ench.UpgradeId, ench.Description);
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
                string name = LocalizedContent.Name(ench.UpgradeId, !string.IsNullOrEmpty(ench.DisplayName) ? ench.DisplayName : ench.UpgradeId);
                lines.Add($"• {name} — {LocalizedContent.Description(ench.UpgradeId, ench.Description)}");
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
                    ? $"{LocalizedContent.Ui("altar.cost", "Costo")}: <color=#D9A44E>{cost} {LocalizedContent.Ui("gold.unit", "oro")}</color>"
                    : $"{LocalizedContent.Ui("altar.cost", "Costo")}: —";
            }
            RefreshFacesPreview();
            RefreshSlotDescription();
        }

        private void RefreshFacesPreview()
        {
            bool hasSelection = _selectedBagIndex >= 0;
            var faces = hasSelection ? ComputeAllowedFacesForSelection() : Array.Empty<int>();

            if (_facesContainer != null && _faceCardPrefab != null)
            {
                ClearFaceCards();
                if (_facesCaptionLabel != null) _facesCaptionLabel.gameObject.SetActive(hasSelection);
                if (!hasSelection) return;

                Sprite sprite = null;
                if (_diceUiSettings != null && TryGetSelectedDiceType(out var type))
                    sprite = _diceUiSettings.GetSprite(type);

                foreach (int face in faces.OrderBy(f => f))
                {
                    var card = Instantiate(_faceCardPrefab, _facesContainer);
                    card.Set(sprite, face);
                    _faceCards.Add(card);
                }
                return;
            }

            // Fallback texto — prefab viejo sin container de caras.
            if (_facesPreviewLabel != null)
            {
                _facesPreviewLabel.text = hasSelection
                    ? LocalizedContent.Ui("altar.current_faces", "Caras actuales") + ": " + FormatFaces(faces)
                    : string.Empty;
            }
        }

        private void ClearFaceCards()
        {
            foreach (var card in _faceCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            _faceCards.Clear();
        }

        private bool TryGetSelectedDiceType(out DiceType type)
        {
            type = default;
            if (_selectedBagIndex < 0) return false;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                || enchSvc?.Bag == null || _selectedBagIndex >= enchSvc.Bag.Dice.Count)
                return false;
            type = enchSvc.Bag.Dice[_selectedBagIndex];
            return true;
        }

        private void RefreshSlotDescription()
        {
            if (_slotDescriptionLabel == null) return;

            string text = string.Empty;
            if (_selectedBagIndex >= 0 && _selectedSlotIndex >= 0
                && ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                && enchSvc?.Bag != null)
            {
                var existing = enchSvc.Bag.GetEnchantmentAt(_selectedBagIndex, _selectedSlotIndex);
                if (existing != null)
                {
                    string name = LocalizedContent.Name(existing.UpgradeId,
                        !string.IsNullOrEmpty(existing.DisplayName) ? existing.DisplayName : existing.UpgradeId);
                    string desc = LocalizedContent.Description(existing.UpgradeId, existing.Description);
                    text = string.IsNullOrEmpty(desc)
                        ? $"<b>{name}</b>"
                        : $"<b>{name}</b> — <size=90%>{desc}</size>";
                }
            }
            _slotDescriptionLabel.text = text;
        }

        private void RefreshGoldDisplay(bool animate)
        {
            if (_goldDisplay != null) _goldDisplay.Refresh(animate);
            if (_costGoldDisplay != null) _costGoldDisplay.Refresh(animate);
            if (_goldLabel == null) return;
            if (ServiceLocator.TryGetService<IEconomyService>(out var economy) && economy != null)
            {
                _goldLabel.text = $"{LocalizedContent.Ui("altar.gold", "Oro")}: {economy.CurrentGold}";
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
        // Juice helpers
        // ====================================================================

        private void PlayCardsEntrance(List<EnchantmentItemButtonView> cards)
        {
            if (!CanJuice()) return;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;

                var t = card.transform;
                var cg = card.GetComponent<CanvasGroup>();
                if (cg == null) cg = card.gameObject.AddComponent<CanvasGroup>();

                Tween.StopAll(onTarget: t);
                Tween.StopAll(onTarget: cg);
                cg.alpha = 0f;
                t.localScale = Vector3.one * _uiSettings.CardEnterScaleFrom;

                float delay = i * _uiSettings.CardStagger;
                Tween.Alpha(cg, 1f, _uiSettings.CardEnterDuration, _uiSettings.CardEnterEase,
                    startDelay: delay, useUnscaledTime: true);
                Tween.Scale(t, Vector3.one, _uiSettings.CardEnterDuration, _uiSettings.CardEnterEase,
                    startDelay: delay, useUnscaledTime: true);
            }
        }

        private void PlayResultJuice()
        {
            if (!CanJuice() || _resultLabel == null) return;

            if (_resultCanvasGroup == null)
            {
                _resultCanvasGroup = _resultLabel.GetComponent<CanvasGroup>();
                if (_resultCanvasGroup == null) _resultCanvasGroup = _resultLabel.gameObject.AddComponent<CanvasGroup>();
            }
            Tween.StopAll(onTarget: _resultCanvasGroup);
            _resultCanvasGroup.alpha = 0f;
            Tween.Alpha(_resultCanvasGroup, 1f, _uiSettings.ResultFadeDuration, _uiSettings.ResultEase,
                useUnscaledTime: true);
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
            if (result.Success && _goldDisplay != null) _goldDisplay.Shake();

            // Refrescar UI tras el apply — slots cambiaron, oro cambió.
            PopulateDiceButtons();
            if (_selectedBagIndex >= 0) PopulateSlotButtons(_selectedBagIndex);
            // La selección del slot se mantiene por ergonomía (re-enchant del mismo slot).
            // SetSelected(true) sobre las cards recién pobladas dispara su punch — el
            // feedback visual del apply sobre el dado y el cupo elegidos.
            for (int i = 0; i < _diceButtons.Count; i++)
                _diceButtons[i].SetSelected(i == _selectedBagIndex);
            for (int i = 0; i < _slotButtons.Count; i++)
                _slotButtons[i].SetSelected(i == _selectedSlotIndex);
            RefreshGoldDisplay(animate: true);
            RefreshSelectionLabels();
            RefreshConfirmButtonState();
        }

        private void ShowResult(EnchantmentRollResult result)
        {
            if (_resultLabel == null) return;
            if (!result.Success)
            {
                _resultLabel.text = $"<color=#ff8888>{result.ErrorMessage}</color>";
                PlayResultJuice();
                return;
            }
            var rolled = result.RolledEnchantment;
            string name = rolled != null ? LocalizedContent.Name(rolled.UpgradeId, rolled.DisplayName ?? rolled.UpgradeId) : "?";
            string faces = FormatFaces(result.ProjectedFaces);
            // Descripción inline en el resultado (CNF-011) — el jugador ve qué hace el
            // encantamiento sin tener que ir a buscarlo de nuevo en la lista de slots.
            // Formato compacto en 2 líneas: el label vive al pie de un panel que ya
            // ocupa la pantalla completa — 4 líneas desbordaban por debajo.
            string rolledDesc = rolled != null ? LocalizedContent.Description(rolled.UpgradeId, rolled.Description) : string.Empty;
            string descPart = string.IsNullOrEmpty(rolledDesc)
                ? string.Empty
                : $" — <size=80%><i>{rolledDesc}</i></size>";
            _resultLabel.text =
                $"<color=#88ff88>Recibiste:</color> <b>{name}</b>{descPart}\n" +
                $"Caras del dado: {faces}  ·  Oro gastado: {result.GoldPaid}";
            PlayResultJuice();
        }
    }
}
