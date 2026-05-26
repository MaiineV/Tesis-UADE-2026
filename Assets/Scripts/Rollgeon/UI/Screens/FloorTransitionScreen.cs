using Patterns;
using Rollgeon.Run;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Floor transition screen (UI#0013b). Shows the current floor number,
    /// an optional floor title, and a continue button that navigates to
    /// the next screen (default: ExplorationHUD).
    /// </summary>
    /// <remarks>
    /// [SETUP] GameObject lives as child of the Canvas in the run scene.
    /// See <c>docs/setup/UI#0013b_FloorTransitionScreen.md</c> for wiring instructions.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Floor Transition Screen")]
    public class FloorTransitionScreen : BaseScreen
    {
        private const string LogPrefix = "[FloorTransitionScreen] ";
        private const string ScreenId = "FloorTransitionScreen";

        // ---- Inspector refs ----
        [Title("Screen — Floor Transition")]
        [SerializeField] private TextMeshProUGUI _floorNumberLabel;
        [SerializeField] private TextMeshProUGUI _floorTitleLabel;
        [SerializeField] private Button _continueButton;

        [Tooltip("ScreenStringId to navigate to when Continue is clicked.")]
        [SerializeField] private string _nextScreenStringId = "ExplorationHUD";

        // ---- State ----
        private int _currentFloorNumber;

        public override string ScreenStringId => ScreenId;

        protected override void OnPushed(IScreenPayload payload)
        {
            var p = payload as FloorTransitionPayload;
            if (p != null)
            {
                _currentFloorNumber = p.FloorNumber;
            }
            else
            {
                // Fallback: try to read from IRunContextService
                if (ServiceLocator.TryGetService<IRunContextService>(out var runCtx))
                {
                    _currentFloorNumber = runCtx.FloorIndex + 1;
                }
                else
                {
                    _currentFloorNumber = 0;
                    Debug.LogWarning(LogPrefix + "Payload null and IRunContextService unavailable — showing fallback.", this);
                }
            }

            // Populate floor number label
            if (_floorNumberLabel != null)
                _floorNumberLabel.text = _currentFloorNumber > 0
                    ? $"Piso {_currentFloorNumber}"
                    : "Piso ?";

            // Populate or hide floor title label
            if (_floorTitleLabel != null)
            {
                bool hasTitle = p != null && !string.IsNullOrEmpty(p.FloorTitle);
                if (hasTitle)
                {
                    _floorTitleLabel.text = p.FloorTitle;
                    _floorTitleLabel.gameObject.SetActive(true);
                }
                else
                {
                    _floorTitleLabel.gameObject.SetActive(false);
                }
            }

            // Wire continue button
            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnContinueClicked);
        }

        protected override void OnPopped()
        {
            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(OnContinueClicked);

            _currentFloorNumber = 0;
        }

        private void OnContinueClicked()
        {
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PushByStringId(_nextScreenStringId);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager not registered — can't navigate.", this);
            }
        }
    }
}
