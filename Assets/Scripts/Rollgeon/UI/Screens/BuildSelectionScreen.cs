using System;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Heroes;
using Rollgeon.Run;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Build selection screen (UI#0013a). Shows the selected hero's info,
    /// dice bag preview, and confirm/back buttons. On confirm, starts the
    /// run via <see cref="RunBootstrapper.StartRun"/> and navigates to
    /// <c>ExplorationHUD</c>.
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

        // ---- State ----
        private ClassHeroSO _selectedHero;
        private Guid _runId;
        private string _rulesetId;

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

            // Populate dice bag
            PopulateDiceBag();

            // Wire buttons
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_backButton != null) _backButton.onClick.AddListener(OnBackClicked);
        }

        protected override void OnPopped()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);
            ClearDiceSlots();
            _selectedHero = null;
        }

        private void PopulateDiceBag()
        {
            ClearDiceSlots();
            // StartingDiceBagRef is typed as ScriptableObject (opaque stub).
            // DiceBagSO doesn't exist yet. For now, show fallback.
            // When DiceBagSO merges, this method will cast and iterate.

            bool hasBag = false;
            if (_selectedHero != null && _selectedHero.StartingDiceBagRef != null)
            {
                // Future: cast to DiceBagSO and iterate dice
                // For now, show the SO name as a placeholder
                if (_diceSlotPrefab != null && _diceContainer != null)
                {
                    var slot = Instantiate(_diceSlotPrefab, _diceContainer);
                    slot.Bind(_selectedHero.StartingDiceBagRef.name);
                    hasBag = true;
                }
            }

            if (_diceBagFallbackLabel != null)
                _diceBagFallbackLabel.gameObject.SetActive(!hasBag);
        }

        private void ClearDiceSlots()
        {
            if (_diceContainer == null) return;
            for (int i = _diceContainer.childCount - 1; i >= 0; i--)
                Destroy(_diceContainer.GetChild(i).gameObject);
        }

        private void OnConfirmClicked()
        {
            if (_selectedHero == null)
            {
                Debug.LogWarning(LogPrefix + "Confirm with null hero — ignoring.", this);
                return;
            }

            // Resolve RulesetSO
            RulesetSO ruleset = null;
            ServiceLocator.TryGetService<RulesetSO>(out ruleset);

            // Start the run via RunBootstrapper
            RunBootstrapper.StartRun(_selectedHero, ruleset, _runId);

            Debug.Log(LogPrefix + $"Run started. hero={_selectedHero.EntityId}, runId={_runId}", this);

            // Navigate to exploration (stub-graceful if not registered)
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PushByStringId("ExplorationHUD");
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager not registered — can't navigate.", this);
            }
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
