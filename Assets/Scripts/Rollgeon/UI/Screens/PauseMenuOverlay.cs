using Patterns;
using Rollgeon.Phase;
using Rollgeon.Run;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Pause menu overlay (UI#0014c). Provides resume, settings (stub),
    /// and quit-run buttons. Pushes <see cref="PhaseOverlay.Pause"/> on
    /// the <see cref="IPhaseService"/> while active.
    /// </summary>
    /// <remarks>
    /// [SETUP] GameObject lives as child of the Canvas in the run scene,
    /// starts deactivated. See <c>docs/setup/UI#0014c_PauseMenuOverlay.md</c>
    /// for wiring instructions.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Pause Menu Overlay")]
    public class PauseMenuOverlay : BaseScreen
    {
        private const string LogPrefix = "[PauseMenuOverlay] ";
        private const string ScreenId = "PauseMenu";

        // ---- Inspector refs ----
        [Title("Overlay — Pause Menu")]
        [Required("Arrastrar el Button de resume.")]
        [SerializeField] private Button _resumeButton;

        [Required("Arrastrar el Button de settings.")]
        [SerializeField] private Button _settingsButton;

        [Required("Arrastrar el Button de quit run.")]
        [SerializeField] private Button _quitRunButton;

        // ---- State ----
        [ShowInInspector, ReadOnly]
        private bool _phasePushed;

        public override string ScreenStringId => ScreenId;

        protected override void OnPushed(IScreenPayload payload)
        {
            if (ServiceLocator.TryGetService<IPhaseService>(out var phase))
            {
                phase.PushOverlay(PhaseOverlay.Pause);
                _phasePushed = true;
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IPhaseService not registered — skipping overlay push.", this);
                _phasePushed = false;
            }

            if (_resumeButton != null) _resumeButton.onClick.AddListener(OnResumeClicked);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_quitRunButton != null) _quitRunButton.onClick.AddListener(OnQuitRunClicked);
        }

        protected override void OnPopped()
        {
            if (_resumeButton != null) _resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (_quitRunButton != null) _quitRunButton.onClick.RemoveListener(OnQuitRunClicked);

            if (_phasePushed)
            {
                if (ServiceLocator.TryGetService<IPhaseService>(out var phase))
                {
                    phase.PopOverlay();
                }

                _phasePushed = false;
            }
        }

        private void OnResumeClicked()
        {
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PopOverlay();
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager not registered — can't close overlay.", this);
            }
        }

        private void OnSettingsClicked()
        {
            Debug.Log(LogPrefix + "Settings pressed (stub — pending settings screen).");
        }

        private void OnQuitRunClicked()
        {
            // Pop phase overlay before leaving
            if (_phasePushed)
            {
                if (ServiceLocator.TryGetService<IPhaseService>(out var phase))
                {
                    phase.PopOverlay();
                }

                _phasePushed = false;
            }

            // End the current run if context is available
            if (ServiceLocator.TryGetService<IRunContextService>(out var runCtx))
            {
                RunBootstrapper.EndRun(runCtx.RunId);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IRunContextService not available — skipping EndRun.", this);
            }

            SceneManager.LoadScene("01_MainMenu");
        }
    }
}
