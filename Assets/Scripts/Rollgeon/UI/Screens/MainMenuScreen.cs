using Patterns;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Screen principal del juego. Dos botones: Jugar y Salir.
    /// Plan §4.4 / TECHNICAL.md §17.D.
    /// </summary>
    /// <remarks>
    /// [SETUP] El GameObject vive como hijo del Canvas de la escena <c>01_MainMenu</c>. Los
    /// botones se arman en engine y se cablean al componente via Inspector — ver
    /// <c>docs/setup/UI#0102_MainMenu.md §8.4</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Main Menu Screen")]
    public class MainMenuScreen : BaseScreen
    {
        private const string LogPrefix = "[MainMenuScreen] ";
        private const string ClassSelectionScreenId = "ClassSelectionScreen";

        [Title("Main Menu — Buttons")]
        [Required("Arrastrar el boton 'Jugar' del Canvas (ver instructivo §8.4).")]
        [SerializeField]
        private Button _playButton;

        [Required("Arrastrar el boton 'Salir' del Canvas (ver instructivo §8.4).")]
        [SerializeField]
        private Button _quitButton;

        /// <inheritdoc/>
        public override string ScreenStringId => "MainMenu";

        private void OnEnable()
        {
            if (_playButton != null)
            {
                _playButton.onClick.AddListener(OnPlayClicked);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "_playButton no esta cableado en el Inspector.", this);
            }

            if (_quitButton != null)
            {
                _quitButton.onClick.AddListener(OnQuitClicked);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "_quitButton no esta cableado en el Inspector.", this);
            }
        }

        private void OnDisable()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(OnPlayClicked);
            if (_quitButton != null) _quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        /// <summary>
        /// Handler del boton "Jugar". Intenta navegar a <c>ClassSelectionScreen</c> via
        /// <see cref="IScreenManager.PushByStringId"/>. Fallback graceful si T98 aun no mergeo
        /// (plan §10 R1).
        /// </summary>
        // [STUB] ClassSelectionScreen from T98 — cuando T98 mergee, cambiar a
        //        ServiceLocator.GetService<IScreenManager>().Push<ClassSelectionScreen>()
        //        y borrar esta seccion de fallback por string-id.
        private void OnPlayClicked()
        {
            Debug.Log(LogPrefix + "Play clicked.", this);

            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no esta registrado en ServiceLocator. " +
                                 "Verificar que exista un ScreenHost en la escena (instructivo §8.3).", this);
                return;
            }

            screens.PushByStringId(ClassSelectionScreenId);
        }

        /// <summary>
        /// Handler del boton "Salir". En editor apaga playmode; en build cierra la app.
        /// </summary>
        private void OnQuitClicked()
        {
            Debug.Log(LogPrefix + "Quit requested.", this);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
