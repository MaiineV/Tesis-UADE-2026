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
    /// Defeat screen (UI#0013c). Shown when the player is defeated.
    /// Subscribes to <see cref="EventName.OnPlayerDefeated"/> in Awake and
    /// auto-pushes itself via <see cref="IScreenManager.PushByStringId"/>.
    /// </summary>
    /// <remarks>
    /// [SETUP] GameObject lives as child of the Canvas in the run scene,
    /// starts ACTIVE so Awake fires before ScreenHost deactivates it.
    /// See <c>docs/setup/UI#0013c_VictoryDefeatScreens.md</c> for wiring instructions.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Defeat Screen")]
    public class DefeatScreen : BaseScreen
    {
        private const string LogPrefix = "[DefeatScreen] ";
        private const string ScreenId = "DefeatScreen";

        // ---- Inspector refs ----
        [Title("Screen — Defeat")]
        [Required("Arrastrar el Button de return to menu.")]
        [SerializeField] private Button _returnToMenuButton;

        [Required("Arrastrar el TextMeshProUGUI del titulo.")]
        [SerializeField] private TextMeshProUGUI _titleLabel;

        // ---- State ----
        [ShowInInspector, ReadOnly]
        private bool _pushed;

        private EventManager.EventReceiver _onPlayerDefeatedHandler;

        public override string ScreenStringId => ScreenId;

        private void Awake() => EnsureSubscribed();

        // El ScreenHost desactiva las screens en su Awake; si el nuestro no corrió antes,
        // se saltea. Por eso el host nos llama explícitamente acá para garantizar la suscripción.
        public override void OnRegisteredByHost() => EnsureSubscribed();

        private void EnsureSubscribed()
        {
            // Idempotencia: Awake (tests) y OnRegisteredByHost (runtime) llaman acá; uno suscribe,
            // el resto es no-op.
            if (_onPlayerDefeatedHandler != null) return;
            Debug.Log("[DIAG-victory] DefeatScreen — suscribiendo a OnPlayerDefeated");
            _onPlayerDefeatedHandler = HandlePlayerDefeated;
            EventManager.Subscribe(EventName.OnPlayerDefeated, _onPlayerDefeatedHandler);
        }

        private void OnDestroy()
        {
            if (_onPlayerDefeatedHandler == null) return;
            EventManager.UnSubscribe(EventName.OnPlayerDefeated, _onPlayerDefeatedHandler);
            _onPlayerDefeatedHandler = null;
        }

        private void HandlePlayerDefeated(params object[] args)
        {
            Debug.Log($"[DIAG-victory] DefeatScreen.HandlePlayerDefeated — pushed={_pushed}");
            if (_pushed) return;

            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                _pushed = true;
                screens.PushByStringId(ScreenId);
                Debug.Log("[DIAG-victory] DefeatScreen.PushByStringId llamado");
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager not registered — can't push screen.", this);
            }
        }

        protected override void OnPushed(IScreenPayload payload)
        {
            if (_titleLabel != null)
                _titleLabel.text = "Defeat";

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
