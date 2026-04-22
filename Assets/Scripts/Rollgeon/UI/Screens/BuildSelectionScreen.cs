using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Run;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Build selection screen (UI#0013a). Shows the selected hero's info,
    /// dice bag preview, and confirm/back buttons. On confirm, stores the
    /// selected hero in <see cref="PendingRunRequest"/> and loads
    /// <c>02_Gameplay</c>. <see cref="RunBootstrapper.StartRun"/> is fired
    /// downstream by <c>GameplayBootstrapper</c> in the new scene.
    /// </summary>
    /// <remarks>
    /// [SETUP] GameObject lives as child of the Canvas in <c>01_MainMenu.unity</c>.
    /// See <c>docs/setup/UI#0013a_BuildSelectionScreen.md</c> for wiring instructions.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Build Selection Screen")]
    public class BuildSelectionScreen : BaseScreen
    {
        private const string LogPrefix = "[BuildSelectionScreen] ";
        private const string ScreenId = "BuildSelectionScreen";

        // ---- Inspector refs ----
        [Title("Screen — Build Selection")]
        [SerializeField] private TextMeshProUGUI _heroNameLabel;
        [SerializeField] private TextMeshProUGUI _heroDescriptionLabel;
        [SerializeField] private Image _heroPortrait;
        [SerializeField] private Transform _diceContainer;
        [SerializeField] private DiceSlotView _diceSlotPrefab;
        [SerializeField] private TextMeshProUGUI _diceBagFallbackLabel;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _backButton;

        [Title("Dice Bag Builder (Fase 2)")]
        [Tooltip("Container donde se instancia un PoolOfferingRow por cada DicePoolEntry del hero. " +
                 "Si es null o el hero no trae DiceBagPool, la screen cae al modo legacy.")]
        [SerializeField] private Transform _poolOfferingsContainer;

        [Tooltip("Prefab de la fila +/- del pool. Tener en cuenta que se instancia uno por tipo de dado.")]
        [SerializeField] private PoolOfferingRow _poolOfferingPrefab;

        [Tooltip("Label que muestra 'X / RequiredBagSize'. Opcional.")]
        [SerializeField, Optional] private TextMeshProUGUI _bagCounterLabel;

        [Tooltip("Boton para vaciar la bolsa actual. Opcional.")]
        [SerializeField, Optional] private Button _clearBagButton;

        // ---- State ----
        private ClassHeroSO _selectedHero;
        private Guid _runId;
        private string _rulesetId;

        // Builder mode (Fase 2). _builderMode == true cuando el hero trae un DiceBagPool
        // y la screen tiene container/prefab cableados; si no, mantiene el flujo legacy.
        private bool _builderMode;
        private readonly List<DiceType> _currentBag = new();
        private readonly List<PoolOfferingRow> _poolRows = new();

        public override string ScreenStringId => ScreenId;

        protected override void OnPushed(IScreenPayload payload)
        {
            var p = payload as BuildSelectionPayload;
            if (p == null)
            {
                Debug.LogWarning(LogPrefix + "Payload null or wrong type — showing empty.", this);
                return;
            }

            _selectedHero = p.SelectedHero;
            _runId = p.RunId;
            _rulesetId = p.RulesetId;

            // Populate hero info
            if (_heroNameLabel != null && _selectedHero != null)
                _heroNameLabel.text = _selectedHero.DisplayName ?? "";
            if (_heroDescriptionLabel != null && _selectedHero != null)
                _heroDescriptionLabel.text = _selectedHero.Description ?? "";
            if (_heroPortrait != null && _selectedHero != null && _selectedHero.Portrait != null)
                _heroPortrait.sprite = _selectedHero.Portrait;

            // Populate dice bag (builder o legacy)
            PopulateDiceBag();

            // Wire buttons
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_backButton != null) _backButton.onClick.AddListener(OnBackClicked);
            if (_clearBagButton != null) _clearBagButton.onClick.AddListener(OnClearBagClicked);
        }

        protected override void OnPopped()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);
            if (_clearBagButton != null) _clearBagButton.onClick.RemoveListener(OnClearBagClicked);
            ClearPoolRows();
            ClearDiceSlots();
            _currentBag.Clear();
            _builderMode = false;
            _selectedHero = null;
        }

        private void PopulateDiceBag()
        {
            ClearPoolRows();
            ClearDiceSlots();
            _currentBag.Clear();
            _builderMode = false;

            // Modo builder (Fase 2): el hero trae un pool valido y la screen esta cableada.
            var pool = _selectedHero != null ? _selectedHero.DiceBagPool : null;
            if (pool != null && _poolOfferingsContainer != null && _poolOfferingPrefab != null)
            {
                BuildPoolUI(pool);
                _builderMode = true;
                if (_diceBagFallbackLabel != null) _diceBagFallbackLabel.gameObject.SetActive(false);
                RefreshUI();
                return;
            }

            // Modo legacy (Fase 1 fallback): mostrar nombre del SO opaco si existe.
            bool hasBag = false;
            if (_selectedHero != null && _selectedHero.StartingDiceBagRef != null)
            {
                if (_diceSlotPrefab != null && _diceContainer != null)
                {
                    var slot = Instantiate(_diceSlotPrefab, _diceContainer);
                    slot.Bind(_selectedHero.StartingDiceBagRef.name);
                    hasBag = true;
                }
            }

            if (_diceBagFallbackLabel != null)
                _diceBagFallbackLabel.gameObject.SetActive(!hasBag);

            // Confirm en legacy queda habilitado (no hay bolsa que armar).
            if (_confirmButton != null) _confirmButton.interactable = true;
        }

        private void BuildPoolUI(DiceBagPoolSO pool)
        {
            foreach (var entry in pool.Offerings)
            {
                var row = Instantiate(_poolOfferingPrefab, _poolOfferingsContainer);
                row.Bind(entry.Type, entry.MaxInBag);
                row.OnAddRequested += OnAddDice;
                row.OnRemoveRequested += OnRemoveDice;
                _poolRows.Add(row);
            }
        }

        private void OnAddDice(DiceType type)
        {
            var pool = _selectedHero?.DiceBagPool;
            if (pool == null) return;
            if (_currentBag.Count >= pool.RequiredBagSize) return;

            int currentOfType = _currentBag.Count(d => d == type);
            int maxOfType = pool.MaxFor(type);
            if (currentOfType >= maxOfType) return;

            _currentBag.Add(type);
            RefreshUI();
        }

        private void OnRemoveDice(DiceType type)
        {
            // Saca la ultima ocurrencia (LIFO se siente natural en UI).
            int lastIndex = _currentBag.LastIndexOf(type);
            if (lastIndex < 0) return;
            _currentBag.RemoveAt(lastIndex);
            RefreshUI();
        }

        private void OnClearBagClicked()
        {
            _currentBag.Clear();
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (!_builderMode) return;

            var pool = _selectedHero.DiceBagPool;
            int required = pool.RequiredBagSize;
            bool bagHasRoom = _currentBag.Count < required;

            // Refrescar contadores de cada fila.
            foreach (var row in _poolRows)
            {
                int currentOfType = _currentBag.Count(d => d == row.Type);
                row.Refresh(currentOfType, bagHasRoom);
            }

            // Reconstruir la preview de la bolsa armada.
            RebuildSelectedSlots();

            // Counter "X / Y".
            if (_bagCounterLabel != null)
                _bagCounterLabel.text = $"{_currentBag.Count} / {required}";

            // Confirm habilitado solo cuando esta en target.
            if (_confirmButton != null)
                _confirmButton.interactable = _currentBag.Count == required;
        }

        private void RebuildSelectedSlots()
        {
            ClearDiceSlots();
            if (_diceContainer == null || _diceSlotPrefab == null) return;
            foreach (var dice in _currentBag)
            {
                var slot = Instantiate(_diceSlotPrefab, _diceContainer);
                slot.Bind(dice.ToString());
            }
        }

        private void ClearPoolRows()
        {
            foreach (var row in _poolRows)
            {
                if (row == null) continue;
                row.OnAddRequested -= OnAddDice;
                row.OnRemoveRequested -= OnRemoveDice;
                row.Unbind();
                Destroy(row.gameObject);
            }
            _poolRows.Clear();
        }

        private void ClearDiceSlots()
        {
            if (_diceContainer == null) return;
            for (int i = _diceContainer.childCount - 1; i >= 0; i--)
                Destroy(_diceContainer.GetChild(i).gameObject);
        }

        private void OnConfirmClicked()
        {
            if (!TryBuildAndStoreRequest()) return;
            SceneManager.LoadScene("02_Gameplay");
        }

        /// <summary>
        /// Valida estado, construye el <see cref="DiceBagSO"/> runtime si esta en
        /// builder mode, y lo persiste en <see cref="PendingRunRequest"/>. Devuelve
        /// <c>false</c> sin tocar nada si el estado no permite navegar (hero null o
        /// bolsa incompleta). Aislado de <see cref="SceneManager.LoadScene"/> para
        /// poder testearlo en EditMode.
        /// </summary>
        private bool TryBuildAndStoreRequest()
        {
            if (_selectedHero == null)
            {
                Debug.LogWarning(LogPrefix + "Confirm with null hero — ignoring.", this);
                return false;
            }

            DiceBagSO builtBag = null;
            if (_builderMode)
            {
                var pool = _selectedHero.DiceBagPool;
                if (_currentBag.Count != pool.RequiredBagSize)
                {
                    Debug.LogWarning(LogPrefix + $"Confirm con bolsa incompleta ({_currentBag.Count}/{pool.RequiredBagSize}) — ignoring.", this);
                    return false;
                }

                builtBag = ScriptableObject.CreateInstance<DiceBagSO>();
                builtBag.name = $"BuiltBag.{_selectedHero.EntityId}";
                builtBag.Dice = new List<DiceType>(_currentBag);
            }

            PendingRunRequest.Set(_selectedHero, _runId, _rulesetId, builtBag);
            Debug.Log(LogPrefix + $"Navigating to gameplay. hero={_selectedHero.EntityId}, runId={_runId}, builtBag={(builtBag != null ? builtBag.Dice.Count + " dice" : "null")}", this);
            return true;
        }

        private void OnBackClicked()
        {
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PopCurrent();
            }
        }
    }
}
