using System;
using System.Collections.Generic;
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
    /// Pantalla de la Sala de Encantamiento, versión máquina (mock
    /// <c>nuevaUIMesa.jpeg</c>): el fondo del modal ES la slot machine
    /// (<c>SlotMachine_0</c>). Flujo palanca-primero: el jugador tira la palanca
    /// (paga el roll — <c>RollOffer</c>), los 3 slots giran y revelan
    /// encantamientos; elige UNO (outline fijo), después elige un dado válido de
    /// la repisa (sube y brilla con el holo de encantado) y el botón Confirmar
    /// —que pulsa entre prendido y apagado— aplica (<c>ConfirmChoice</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>State.</b> Oferta (fuente de verdad:
    /// <see cref="IEnchantmentRoomService.CurrentOffer"/>) + índice de opción y
    /// de dado elegidos + flag de spin. Re-tirar la palanca con oferta activa
    /// la reemplaza (re-roll del GDD) y limpia las selecciones.
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

        [Title("Root")]
        [Required, SerializeField] private GameObject _panelRoot;

        [Title("Slot machine — opciones")]
        [InfoBox("Los 3 slots de la slot machine, izquierda a derecha.")]
        [SerializeField] private EnchantmentOptionSlotView[] _optionSlots;

        [Tooltip("Descripción / hints / resultado — la barra bajo los slots.")]
        [SerializeField] private TextMeshProUGUI _optionDescriptionLabel;

        [Title("Slot machine — repisa de dados")]
        [InfoBox("Las 5 posiciones de la repisa de la máquina, izquierda a derecha.")]
        [SerializeField] private AltarDieSlotView[] _dieSlots;

        [Title("Slot machine — palanca")]
        [SerializeField] private AltarLeverView _lever;

        [Tooltip("Título de la caja de costo ('Tirada'), arriba al centro.")]
        [SerializeField, Optional] private TextMeshProUGUI _costTitleLabel;

        [Tooltip("Valor del costo del próximo roll — objeto separado, abajo junto a la pila.")]
        [SerializeField] private TextMeshProUGUI _costLabel;

        [Tooltip("Pila de fichas de oro de la caja de costo — el ícono canónico de oro.")]
        [SerializeField, Optional] private AltarGoldDisplayView _costGoldDisplay;

        [Title("Botones")]
        [SerializeField] private AltarConfirmButtonView _confirmButton;
        [SerializeField] private Button _closeButton;

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

        /// <summary>
        /// RectTransform del panel de la slot-machine — anchor del overlay del tutorial
        /// (BUG-036: los pasos del altar se anclan al panel para que el popup se coloque
        /// al costado en vez de taparlo centrado).
        /// </summary>
        public bool TryGetPanelRect(out RectTransform rect)
        {
            rect = _panelRoot != null ? _panelRoot.transform as RectTransform : null;
            return rect != null;
        }

        // ----- Runtime state ----------------------------------------------------
        private bool _subscribed;
        private Guid _currentRoomInstanceId;
        private int _selectedOptionIndex = -1;
        private int _selectedBagIndex = -1;
        private bool _spinning;
        private bool _closing;
        private RectTransform _panelRect;
        private CanvasGroup _panelCanvasGroup;
        private CanvasGroup _descriptionCanvasGroup;
        private int _reelsPending;

        // ====================================================================
        // Lifecycle
        // ====================================================================

        private void Awake()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (_closeButton != null) _closeButton.onClick.AddListener(HandleCloseClicked);
            if (_confirmButton != null && _confirmButton.Button != null)
                _confirmButton.Button.onClick.AddListener(HandleConfirmClicked);
            if (_lever != null) _lever.OnPulled += HandleLeverPulled;
            if (_optionSlots != null)
            {
                for (int i = 0; i < _optionSlots.Length; i++)
                {
                    if (_optionSlots[i] == null) continue;
                    _optionSlots[i].Configure(i, HandleOptionClicked, HandleOptionHoverChanged);
                }
            }
            if (_dieSlots != null)
            {
                for (int i = 0; i < _dieSlots.Length; i++)
                {
                    if (_dieSlots[i] == null) continue;
                    _dieSlots[i].Configure(i, HandleDieClicked);
                }
            }
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(HandleCloseClicked);
            if (_confirmButton != null && _confirmButton.Button != null)
                _confirmButton.Button.onClick.RemoveListener(HandleConfirmClicked);
            if (_lever != null) _lever.OnPulled -= HandleLeverPulled;
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
            ClearRoomOffer();

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
            RefreshLeverAndCost();
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
            _spinning = false;
            _panelRoot.SetActive(true);

            ClearSelections();
            ClearRoomOffer();
            ResetOptionSlots();
            BindDiceShelf();
            if (_confirmButton != null) _confirmButton.SetReady(false);

            ApplyCaptions();
            SetDescriptionHint(DescriptionHint.PullLever);
            RefreshLeverAndCost();
            if (_costGoldDisplay != null) _costGoldDisplay.Refresh(animate: false);

            PlayOpenJuice();
        }

        private void HandleCloseClicked()
        {
            if (_closing) return;
            ClearRoomOffer();

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
            if (_descriptionCanvasGroup != null) Tween.StopAll(onTarget: _descriptionCanvasGroup);
            _spinning = false; // los reels en vuelo los frena cada slot (OnDisable / SetEmpty)
        }

        private void RestorePanelPose()
        {
            if (_panelRect != null) _panelRect.localScale = Vector3.one;
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = 1f;
                _panelCanvasGroup.blocksRaycasts = true;
            }
            if (_descriptionCanvasGroup != null) _descriptionCanvasGroup.alpha = 1f;
        }

        private void ApplyCaptions()
        {
            if (_costTitleLabel != null)
                _costTitleLabel.text = LocalizedContent.Ui("altar.roll", "Tirada");
            if (_confirmButton != null)
                SetButtonLabel(_confirmButton.Button, LocalizedContent.Ui("altar.confirm", "Confirmar"));
            SetButtonLabel(_closeButton, LocalizedContent.Ui("altar.close", "Cerrar"));
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }

        // ====================================================================
        // Repisa de dados
        // ====================================================================

        private void BindDiceShelf()
        {
            if (_dieSlots == null || _dieSlots.Length == 0)
            {
                Debug.LogError(LogPrefix + "_dieSlots sin asignar — la repisa queda vacía.", this);
                ShowError(LocalizedContent.Ui("altar.load_error",
                    "No se pudieron cargar los dados — cierra la mesa y vuelve a intentar."));
                return;
            }

            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                || enchSvc == null || !enchSvc.IsReady || enchSvc.Bag == null)
            {
                Debug.LogWarning(LogPrefix + "DiceEnchantmentService no listo — no se pueden mostrar dados.");
                ShowError(LocalizedContent.Ui("altar.load_error",
                    "No se pudieron cargar los dados — cierra la mesa y vuelve a intentar."));
                return;
            }

            var bag = enchSvc.Bag;
            for (int i = 0; i < _dieSlots.Length; i++)
            {
                var slot = _dieSlots[i];
                if (slot == null) continue;

                bool occupied = i < bag.Dice.Count;
                slot.SetOccupied(occupied);
                if (!occupied) continue;

                var dice = bag.Dice[i];
                var enchants = bag.GetEnchantments(i);
                slot.Bind(
                    _diceUiSettings != null ? _diceUiSettings.GetSprite(dice) : null,
                    dice.MaxFace().ToString(),
                    () => BuildDiceTooltip(enchants));
            }
        }

        private void RefreshDiceSelectable()
        {
            if (_dieSlots == null) return;
            var option = GetSelectedOption();
            bool anyValid = false;

            ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc);
            for (int i = 0; i < _dieSlots.Length; i++)
            {
                var slot = _dieSlots[i];
                if (slot == null) continue;
                bool valid = option != null && enchSvc != null
                             && enchSvc.ValidateApply(i, option).Success;
                slot.SetSelectable(valid);
                anyValid |= valid;
            }

            if (option != null && !anyValid)
            {
                // Pre-filtrado en RollOffer garantiza ≥1 dado válido por opción —
                // esto solo puede pasar si el estado cambió por afuera (dev console).
                Debug.LogWarning(LogPrefix + "La opción elegida no tiene ningún dado válido.");
            }
        }

        private void ClearDiceSelection()
        {
            _selectedBagIndex = -1;
            if (_dieSlots == null) return;
            foreach (var slot in _dieSlots)
            {
                if (slot != null) slot.SetSelected(false);
            }
        }

        private void HandleDieClicked(int bagIndex)
        {
            if (_spinning || _selectedOptionIndex < 0) return;

            _selectedBagIndex = bagIndex;
            for (int i = 0; i < _dieSlots.Length; i++)
            {
                if (_dieSlots[i] != null) _dieSlots[i].SetSelected(i == bagIndex);
            }
            if (_confirmButton != null) _confirmButton.SetReady(true);
            SetDescriptionHint(DescriptionHint.Confirm);
        }

        // ====================================================================
        // Palanca + roll
        // ====================================================================

        private void HandleLeverPulled()
        {
            if (_spinning) return;
            if (!ServiceLocator.TryGetService<IEnchantmentRoomService>(out var roomSvc) || roomSvc == null)
            {
                Debug.LogWarning(LogPrefix + "RoomService no registrado — la palanca no puede rolear.");
                return;
            }

            var result = roomSvc.RollOffer(_currentRoomInstanceId);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage);
                RefreshLeverAndCost();
                return;
            }

            // Re-roll con selecciones activas: se descartan junto con la oferta vieja.
            ClearSelections();
            if (_confirmButton != null) _confirmButton.SetReady(false);
            StartSpin(result.Offer);
        }

        private void RefreshLeverAndCost()
        {
            int cost = ResolveCurrentCost();

            if (_costLabel != null)
                _costLabel.text = cost.ToString();

            if (_lever != null)
                _lever.SetInteractable(!_spinning && CanAfford(cost));
        }

        private bool CanAfford(int cost)
        {
            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null) return false;
            return economy.CanAfford(cost);
        }

        private int ResolveCurrentCost()
        {
            if (!ServiceLocator.TryGetService<IEnchantmentRoomService>(out var roomSvc) || roomSvc == null) return 0;
            return roomSvc.ResolveCost();
        }

        // ====================================================================
        // Reels
        // ====================================================================

        private void StartSpin(EnchantmentOffer offer)
        {
            if (_optionSlots == null || _optionSlots.Length == 0)
            {
                Debug.LogError(LogPrefix + "_optionSlots sin asignar — no hay dónde mostrar la oferta.", this);
                return;
            }

            SetDescriptionHint(DescriptionHint.Spinning);
            RefreshDiceSelectable(); // sin opción elegida ⇒ apaga los outlines

            if (!CanJuice())
            {
                LandAllReels(offer);
                return;
            }

            _spinning = true;
            RefreshLeverAndCost(); // apaga la palanca durante el spin
            SetOptionsInteractable(false);

            var cycleNames = BuildCycleNames(offer);
            _reelsPending = 0;

            for (int i = 0; i < _optionSlots.Length; i++)
            {
                var slot = _optionSlots[i];
                if (slot == null) continue;
                _reelsPending++;

                var final = i < offer.Options.Count ? offer.Options[i] : null;
                float duration = _uiSettings.ReelSpinDuration + i * _uiSettings.ReelStopStagger;
                // Más ciclos en los reels que giran más tiempo — la densidad de
                // nombres desfilando se mantiene mientras el vecino ya frenó.
                int cycles = Mathf.Max(4, _uiSettings.ReelTotalCycles
                    + Mathf.RoundToInt(i * _uiSettings.ReelStopStagger * 8f));

                // Offset primo por reel para que no desfilen la misma secuencia.
                slot.PlaySpin(duration, cycles, cycleNames, i * 7, final, HandleReelLanded);
            }

            if (_reelsPending == 0) LandAllReels(offer);
        }

        private void HandleReelLanded()
        {
            _reelsPending--;
            if (_reelsPending <= 0) FinishSpin();
        }

        private void LandAllReels(EnchantmentOffer offer)
        {
            for (int i = 0; i < _optionSlots.Length; i++)
            {
                var slot = _optionSlots[i];
                if (slot == null) continue;
                slot.SetOption(i < offer.Options.Count ? offer.Options[i] : null);
            }
            FinishSpin();
        }

        private void FinishSpin()
        {
            _spinning = false;
            SetOptionsInteractable(true);
            SetDescriptionHint(DescriptionHint.ChooseOption);
            RefreshLeverAndCost(); // el costo subió (escala global) y la palanca vuelve (re-roll)
        }

        /// <summary>
        /// Nombres que ciclan durante el spin: el catálogo completo da variedad;
        /// fallback a las opciones de la oferta si no está registrado.
        /// </summary>
        private static List<string> BuildCycleNames(EnchantmentOffer offer)
        {
            var names = new List<string>();
            if (ServiceLocator.TryGetService<EnchantmentCatalogSO>(out var catalog) && catalog != null)
            {
                foreach (var entry in catalog.Entries)
                {
                    if (entry == null) continue;
                    names.Add(EnchantmentOptionSlotView.FormatName(entry));
                }
            }
            if (names.Count == 0)
            {
                foreach (var opt in offer.Options)
                {
                    if (opt != null) names.Add(EnchantmentOptionSlotView.FormatName(opt));
                }
            }
            if (names.Count == 0) names.Add("?");
            return names;
        }

        // ====================================================================
        // Opciones — hover + selección
        // ====================================================================

        private void ResetOptionSlots()
        {
            if (_optionSlots == null) return;
            foreach (var slot in _optionSlots)
            {
                if (slot != null) slot.SetEmpty();
            }
        }

        private void SetOptionsInteractable(bool interactable)
        {
            if (_optionSlots == null) return;
            foreach (var slot in _optionSlots)
            {
                if (slot == null) continue;
                slot.SetInteractable(interactable && slot.Option != null);
            }
        }

        private void ClearSelections()
        {
            _selectedOptionIndex = -1;
            ClearDiceSelection();
            if (_optionSlots != null)
            {
                foreach (var slot in _optionSlots)
                {
                    if (slot != null) slot.SetSelected(false);
                }
            }
            RefreshDiceSelectable();
        }

        private EnchantmentSO GetSelectedOption()
        {
            if (_optionSlots == null) return null;
            if (_selectedOptionIndex < 0 || _selectedOptionIndex >= _optionSlots.Length) return null;
            var slot = _optionSlots[_selectedOptionIndex];
            return slot != null ? slot.Option : null;
        }

        private void HandleOptionClicked(int index)
        {
            if (_spinning) return;
            if (index < 0 || _optionSlots == null || index >= _optionSlots.Length) return;
            var clicked = _optionSlots[index];
            if (clicked == null || clicked.Option == null) return;

            _selectedOptionIndex = index;
            for (int i = 0; i < _optionSlots.Length; i++)
            {
                if (_optionSlots[i] != null) _optionSlots[i].SetSelected(i == index);
            }

            // Cambiar de encantamiento invalida el dado elegido (puede no ser
            // coherente con el nuevo) — se re-elige entre los marcados.
            ClearDiceSelection();
            if (_confirmButton != null) _confirmButton.SetReady(false);
            RefreshDiceSelectable();

            if (_optionDescriptionLabel != null)
                _optionDescriptionLabel.text = BuildEnchantmentTooltip(clicked.Option);
        }

        private void HandleOptionHoverChanged(int index, bool hovering)
        {
            if (_spinning || _optionDescriptionLabel == null) return;
            if (hovering && index >= 0 && index < _optionSlots.Length && _optionSlots[index] != null
                && _optionSlots[index].Option != null)
            {
                _optionDescriptionLabel.text = BuildEnchantmentTooltip(_optionSlots[index].Option);
                return;
            }

            // Al salir del hover: la descripción del elegido, o el hint del paso.
            var selected = GetSelectedOption();
            if (selected != null)
            {
                _optionDescriptionLabel.text = BuildEnchantmentTooltip(selected);
                return;
            }
            SetDescriptionHint(GetCurrentOffer().HasValue ? DescriptionHint.ChooseOption
                : DescriptionHint.PullLever);
        }

        // ====================================================================
        // Confirmar
        // ====================================================================

        private void HandleConfirmClicked()
        {
            if (_spinning || _selectedOptionIndex < 0 || _selectedBagIndex < 0) return;
            if (!ServiceLocator.TryGetService<IEnchantmentRoomService>(out var roomSvc) || roomSvc == null)
            {
                Debug.LogWarning(LogPrefix + "RoomService no registrado — no se puede confirmar.");
                return;
            }

            var result = roomSvc.ConfirmChoice(_selectedOptionIndex, _selectedBagIndex);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage);
                // La oferta se conserva — el dado elegido pudo ser el problema.
                ClearDiceSelection();
                if (_confirmButton != null) _confirmButton.SetReady(false);
                RefreshDiceSelectable();
                return;
            }

            // Aplicado: la máquina vuelve a reposo con el resultado a la vista.
            ClearSelections();
            ResetOptionSlots();
            BindDiceShelf();
            if (_confirmButton != null) _confirmButton.SetReady(false);
            ShowResult(result);
            RefreshLeverAndCost();
        }

        private EnchantmentOffer? GetCurrentOffer()
        {
            return ServiceLocator.TryGetService<IEnchantmentRoomService>(out var roomSvc) && roomSvc != null
                ? roomSvc.CurrentOffer
                : null;
        }

        private static void ClearRoomOffer()
        {
            if (ServiceLocator.TryGetService<IEnchantmentRoomService>(out var roomSvc) && roomSvc != null)
                roomSvc.ClearOffer();
        }

        // ====================================================================
        // Description bar (hints + resultado)
        // ====================================================================

        private enum DescriptionHint { PullLever, Spinning, ChooseOption, SelectDie, Confirm }

        private void SetDescriptionHint(DescriptionHint hint)
        {
            if (_optionDescriptionLabel == null) return;
            _optionDescriptionLabel.text = hint switch
            {
                DescriptionHint.PullLever => LocalizedContent.Ui("altar.pull_hint",
                    "Tira de la palanca para revelar 3 encantamientos."),
                DescriptionHint.Spinning => "...",
                DescriptionHint.ChooseOption => LocalizedContent.Ui("altar.choose_option_hint",
                    "Elige un encantamiento — pasa el cursor para leerlos."),
                DescriptionHint.SelectDie => LocalizedContent.Ui("altar.select_die_hint",
                    "Elige un dado para encantar."),
                DescriptionHint.Confirm => LocalizedContent.Ui("altar.confirm_hint",
                    "Aprieta Confirmar para encantar el dado."),
                _ => string.Empty,
            };
        }

        // ====================================================================
        // Tooltip text builders — puros y testeables sin UI real (CNF-011)
        // ====================================================================

        /// <summary>
        /// Texto de hover para un encantamiento: nombre en bold + descripción
        /// reducida. <c>public</c> (no <c>internal</c>) porque el asmdef de tests
        /// de Dice no tiene <c>InternalsVisibleTo</c> hacia el assembly raíz.
        /// </summary>
        public static string BuildEnchantmentTooltip(EnchantmentSO ench)
        {
            if (ench == null) return string.Empty;
            string name = $"<color=#{EnchantmentPalette.TitleHex(ench)}>{LocalizedContent.Name(ench.UpgradeId, !string.IsNullOrEmpty(ench.DisplayName) ? ench.DisplayName : ench.UpgradeId)}</color>";
            string desc = LocalizedContent.Description(ench.UpgradeId, ench.Description);
            return string.IsNullOrEmpty(desc)
                ? $"<b>{name}</b>"
                : $"<b>{name}</b>\n<size=80%>{desc}</size>";
        }

        /// <summary>
        /// Texto de hover para un dado: una línea por encantamiento aplicado
        /// ("• Nombre — Descripción"), o placeholder si no tiene ninguno.
        /// </summary>
        public static string BuildDiceTooltip(IReadOnlyList<EnchantmentSO> slots)
        {
            string none = LocalizedContent.Ui("altar.no_enchantments", "Sin encantamientos");
            if (slots == null || slots.Count == 0) return none;

            var lines = new List<string>();
            for (int i = 0; i < slots.Count; i++)
            {
                var ench = slots[i];
                if (ench == null) continue;
                string name = $"<color=#{EnchantmentPalette.TitleHex(ench)}>{LocalizedContent.Name(ench.UpgradeId, !string.IsNullOrEmpty(ench.DisplayName) ? ench.DisplayName : ench.UpgradeId)}</color>";
                lines.Add($"• {name} — {LocalizedContent.Description(ench.UpgradeId, ench.Description)}");
            }
            return lines.Count > 0 ? string.Join("\n", lines) : none;
        }

        // ====================================================================
        // Result feedback
        // ====================================================================

        private void ShowError(string message)
        {
            if (_optionDescriptionLabel == null) return;
            _optionDescriptionLabel.text = $"<color=#ff8888>{message}</color>";
            PlayDescriptionJuice();
        }

        private void ShowResult(EnchantmentRollResult result)
        {
            if (_optionDescriptionLabel == null) return;
            if (!result.Success)
            {
                ShowError(result.ErrorMessage);
                return;
            }
            var rolled = result.RolledEnchantment;
            string name = rolled != null
                ? $"<color=#{EnchantmentPalette.TitleHex(rolled)}>{LocalizedContent.Name(rolled.UpgradeId, rolled.DisplayName ?? rolled.UpgradeId)}</color>"
                : "?";
            string faces = FormatFaces(result.ProjectedFaces);
            string received = LocalizedContent.Ui("altar.received", "Recibiste");
            string dieFaces = LocalizedContent.Ui("altar.die_faces", "Caras del dado");
            _optionDescriptionLabel.text =
                $"<color=#88ff88>{received}:</color> <b>{name}</b>  ·  <size=85%>{dieFaces}: {faces}</size>";
            PlayDescriptionJuice();
        }

        private void PlayDescriptionJuice()
        {
            if (!CanJuice() || _optionDescriptionLabel == null) return;

            if (_descriptionCanvasGroup == null)
            {
                _descriptionCanvasGroup = _optionDescriptionLabel.GetComponent<CanvasGroup>();
                if (_descriptionCanvasGroup == null)
                    _descriptionCanvasGroup = _optionDescriptionLabel.gameObject.AddComponent<CanvasGroup>();
            }
            Tween.StopAll(onTarget: _descriptionCanvasGroup);
            _descriptionCanvasGroup.alpha = 0f;
            Tween.Alpha(_descriptionCanvasGroup, 1f, _uiSettings.ResultFadeDuration, _uiSettings.ResultEase,
                useUnscaledTime: true);
        }

        private static string FormatFaces(IReadOnlyCollection<int> faces)
        {
            if (faces == null || faces.Count == 0) return "—";
            var sorted = new List<int>(faces);
            sorted.Sort();
            return string.Join(", ", sorted);
        }
    }
}
