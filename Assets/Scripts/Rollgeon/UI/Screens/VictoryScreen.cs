using Patterns;
using Rollgeon.Run;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Victory screen (UI#0013c). Shown when all floors are cleared.
    /// Subscribes to <see cref="EventName.OnFloorCleared"/> in Awake and
    /// auto-pushes itself via <see cref="IScreenManager.PushByStringId"/>.
    /// </summary>
    /// <remarks>
    /// [SETUP] GameObject lives as child of the Canvas in the run scene,
    /// starts ACTIVE so Awake fires before ScreenHost deactivates it.
    /// See <c>docs/setup/UI#0013c_VictoryDefeatScreens.md</c> for wiring instructions.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Victory Screen")]
    public class VictoryScreen : BaseScreen
    {
        private const string LogPrefix = "[VictoryScreen] ";
        private const string ScreenId = "VictoryScreen";

        // ---- Inspector refs ----
        [Title("Screen — Victory")]
        [Required("Arrastrar el Button de return to menu.")]
        [SerializeField] private Button _returnToMenuButton;

        [Required("Arrastrar el TextMeshProUGUI del titulo.")]
        [SerializeField] private TextMeshProUGUI _titleLabel;

        // ---- State ----
        [ShowInInspector, ReadOnly]
        private bool _pushed;

        private EventManager.EventReceiver _onFloorClearedHandler;

        public override string ScreenStringId => ScreenId;

        private void Awake()
        {
            // Idempotencia: si ya hay handler (Awake doble-fired por tests /
            // lifecycle cuirks), salir sin re-suscribir.
            if (_onFloorClearedHandler != null) return;
            _onFloorClearedHandler = HandleFloorCleared;
            EventManager.Subscribe(EventName.OnFloorCleared, _onFloorClearedHandler);
        }

        private void OnDestroy()
        {
            if (_onFloorClearedHandler == null) return;
            EventManager.UnSubscribe(EventName.OnFloorCleared, _onFloorClearedHandler);
            _onFloorClearedHandler = null;
        }

        private void HandleFloorCleared(params object[] args)
        {
            if (_pushed) return;

            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                _pushed = true;
                screens.PushByStringId(ScreenId);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager not registered — can't push screen.", this);
            }
        }

        protected override void OnPushed(IScreenPayload payload)
        {
            if (_titleLabel != null)
                _titleLabel.text = "Victory!";

            if (_returnToMenuButton != null)
                _returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
        }

        protected override void OnPopped()
        {
            if (_returnToMenuButton != null)
                _returnToMenuButton.onClick.RemoveListener(OnReturnToMenuClicked);

            _pushed = false;
        }

        private void OnReturnToMenuClicked()
        {
            if (ServiceLocator.TryGetService<IRunContextService>(out var runCtx))
            {
                RunBootstrapper.EndRun(runCtx.RunId);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IRunContextService not available — skipping EndRun.", this);
            }

            LoadMainMenu();
        }

        /// <summary>
        /// Hook de carga de escena. Virtual para que tests EditMode puedan
        /// overridear sin hitearle al <see cref="SceneManager"/> (que solo
        /// funciona en PlayMode).
        /// </summary>
        protected virtual void LoadMainMenu()
        {
            SceneManager.LoadScene("01_MainMenu");
        }
    }
}
