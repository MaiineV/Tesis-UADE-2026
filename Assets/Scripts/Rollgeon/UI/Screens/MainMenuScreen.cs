using System;
using System.Collections.Generic;
using Patterns;
using Patterns.Save;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Meta;
using Rollgeon.Run;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Screen principal del juego: Jugar, Continue (resume de run guardada), Salir.
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

        [Tooltip("Boton 'Continue' — clon del Play. Habilitado solo si hay una run " +
                 "guardada en curso (SaveSystem.HasSave). Opcional hasta cablearse.")]
        [SerializeField]
        private Button _continueButton;

        [Required("Arrastrar el boton 'Salir' del Canvas (ver instructivo §8.4).")]
        [SerializeField]
        private Button _quitButton;

        [Tooltip("Boton 'Desbloqueos' (#164). Opcional hasta que la screen se cablee en engine.")]
        [SerializeField]
        private Button _unlocksButton;

        [Tooltip("Boton 'Opciones'. Stub hasta que exista la settings screen — mismo patrón " +
                 "que el settings de PauseMenuOverlay. Opcional.")]
        [SerializeField]
        private Button _optionsButton;

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

            // Opcionales (#164): sin botón cableado el menú sigue funcionando igual.
            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(OnContinueClicked);
                RefreshContinueButton();
            }

            if (_unlocksButton != null)
            {
                _unlocksButton.onClick.AddListener(OnUnlocksClicked);
            }

            if (_optionsButton != null)
            {
                _optionsButton.onClick.AddListener(OnOptionsClicked);
            }

            ConsumePostTutorialRouting();
            TryShowAnalyticsConsent();
        }

        private void OnDisable()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(OnPlayClicked);
            if (_continueButton != null) _continueButton.onClick.RemoveListener(OnContinueClicked);
            if (_quitButton != null) _quitButton.onClick.RemoveListener(OnQuitClicked);
            if (_unlocksButton != null) _unlocksButton.onClick.RemoveListener(OnUnlocksClicked);
            if (_optionsButton != null) _optionsButton.onClick.RemoveListener(OnOptionsClicked);
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

            // FTUE: primera run de la partida → tutorial automático. Si el launcher
            // no puede (config ausente/incompleta), degrada al flujo normal.
            if (ServiceLocator.TryGetService<Rollgeon.Meta.IMetaProgressionService>(out var meta)
                && meta != null && !meta.IsTutorialCompleted
                && Rollgeon.Tutorial.TutorialLauncher.CanLaunch())
            {
                Debug.Log(LogPrefix + "Primera run — arrancando tutorial.", this);
                Rollgeon.Tutorial.TutorialLauncher.Launch();
                return;
            }

            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no esta registrado en ServiceLocator. " +
                                 "Verificar que exista un ScreenHost en la escena (instructivo §8.3).", this);
                return;
            }

            screens.PushByStringId(ClassSelectionScreenId);
        }

        // ================================================================
        // Continue (resume de run guardada)
        // ================================================================

        /// <summary>
        /// Prende/apaga el Continue según exista un save de run en curso. Se invoca
        /// en <c>OnEnable</c> — el save se borra en <c>EndRun(runCompleted: true)</c>,
        /// así que al volver al menú tras victoria/derrota el botón aparece apagado.
        /// </summary>
        private void RefreshContinueButton()
        {
            if (_continueButton == null) return;
            _continueButton.interactable = SaveSystem.HasSave();
        }

        private void OnContinueClicked()
        {
            if (!TryBuildResumeRequest())
            {
                // Save ilegible o build irrecuperable — el botón se apaga para no
                // reintentar en loop; el archivo queda para diagnóstico.
                if (_continueButton != null) _continueButton.interactable = false;
                return;
            }

            GameplaySceneFlow.LoadGameplay();
        }

        /// <summary>
        /// Lee el save, resuelve héroe + build + RunId y deja armada la
        /// <see cref="PendingRunRequest"/> con <c>isResume: true</c>. Aislado del
        /// scene-load para poder testearse en EditMode (patrón
        /// <c>BuildSelectionScreen.TryBuildAndStoreRequest</c>).
        /// </summary>
        private bool TryBuildResumeRequest()
        {
            // En el menú solo hay saveables globales registrados — el restore
            // temprano es inocuo; los run-scoped se rehidratan al Register durante
            // StartRun(resume: true).
            SaveSystem.LoadFromDisk();

            if (!SaveSystem.TryGetCached(RunUnlockState.SaveKeyConst, out var unlockObj) ||
                unlockObj is not RunUnlockSnapshot unlock ||
                string.IsNullOrEmpty(unlock.ClassId))
            {
                Debug.LogWarning(LogPrefix + "Save sin identidad de run (unlock_tracker) — no se puede resumir.", this);
                return false;
            }

            if (!ServiceLocator.TryGetService<HeroCatalogSO>(out var heroCatalog) || heroCatalog == null)
            {
                Debug.LogWarning(LogPrefix + "HeroCatalogSO no registrado — no se puede resolver el héroe del save.", this);
                return false;
            }

            var hero = heroCatalog.GetById(unlock.ClassId);
            if (hero == null)
            {
                Debug.LogWarning(LogPrefix + $"Héroe '{unlock.ClassId}' del save no existe en el catálogo.", this);
                return false;
            }

            // RunId guardado — de él deriva el seed del dungeon (RunController), así
            // el piso resumido se regenera idéntico. Fallback: Guid nuevo (piso nuevo).
            var runId = Guid.NewGuid();
            if (SaveSystem.TryGetCached(RunContext.SaveKeyConst, out var ctxObj) &&
                ctxObj is RunContextSnapshot ctxSnapshot &&
                Guid.TryParse(ctxSnapshot.RunId, out var savedRunId) &&
                savedRunId != Guid.Empty)
            {
                runId = savedRunId;
            }

            DiceBagSO bag = null;
            if (unlock.DiceBuild != null && unlock.DiceBuild.Count > 0)
            {
                bag = ScriptableObject.CreateInstance<DiceBagSO>();
                bag.name = $"ResumedBag.{hero.EntityId}";
                bag.Dice = new List<DiceType>(unlock.DiceBuild);
            }

            PendingRunRequest.Set(hero, runId, rulesetId: null, bag, isResume: true);
            Debug.Log(LogPrefix + $"Resuming run. hero={hero.EntityId}, runId={runId}, " +
                      $"bag={(bag != null ? bag.Dice.Count + " dados" : "null (StartingDiceBagRef)")}", this);
            return true;
        }

        /// <summary>
        /// Primera ejecución sin decisión de consentimiento → abre el popup
        /// opt-in (GDPR). Diferido un frame para que el ScreenHost termine de
        /// registrar screens (mismo patrón que <see cref="ConsumePostTutorialRouting"/>).
        /// </summary>
        private void TryShowAnalyticsConsent()
        {
            if (!ServiceLocator.TryGetService<Rollgeon.Analytics.IAnalyticsConsentService>(out var consent) || consent == null) return;
            if (consent.HasDecision) return;

            Rollgeon.Patterns.CoroutineHost.Run(PushAnalyticsConsentNextFrame());
        }

        private System.Collections.IEnumerator PushAnalyticsConsentNextFrame()
        {
            yield return null;

            // Re-chequeo: el jugador pudo decidir mientras tanto (o el popup ya abrió).
            if (ServiceLocator.TryGetService<Rollgeon.Analytics.IAnalyticsConsentService>(out var consent)
                && consent != null && !consent.HasDecision
                && ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                // Overlay no-destructivo: el menú queda vivo detrás (sin churn de
                // OnDisable/OnEnable que replaye la intro). El bloqueo de input lo
                // aporta el fondo full-screen raycast del propio overlay.
                screens.PushOverlay<AnalyticsConsentOverlay>();
            }
        }

        /// <summary>
        /// Los overlays (consentimiento, opciones) se cierran con pop — el menú
        /// recupera foco sin re-enable. El panel de opciones puede haber borrado
        /// el save, así que el Continue se refresca acá.
        /// </summary>
        protected override void OnGainFocus()
        {
            RefreshContinueButton();
        }

        /// <summary>
        /// Al volver del tutorial completado, saltar directo a la selección de clase
        /// para la primera run real (flag seteado por el teardown del tutorial).
        /// Diferido un frame para que el ScreenHost termine de registrar screens.
        /// </summary>
        private void ConsumePostTutorialRouting()
        {
            if (!Rollgeon.Tutorial.PostTutorialRouting.AutoOpenClassSelection) return;
            Rollgeon.Tutorial.PostTutorialRouting.AutoOpenClassSelection = false;

            Rollgeon.Patterns.CoroutineHost.Run(PushClassSelectionNextFrame());
        }

        private System.Collections.IEnumerator PushClassSelectionNextFrame()
        {
            yield return null;

            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                Debug.Log(LogPrefix + "Tutorial completado — abriendo selección de clase.");
                screens.PushByStringId(ClassSelectionScreenId);
            }
        }

        /// <summary>
        /// Handler del boton "Desbloqueos" (#164). Navega a <c>UnlocksScreen</c>.
        /// </summary>
        private void OnUnlocksClicked()
        {
            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no esta registrado — no se puede navegar.", this);
                return;
            }

            screens.PushByStringId("UnlocksScreen");
        }

        /// <summary>
        /// Abre el panel de opciones como overlay no-destructivo (el menú queda
        /// vivo detrás; al pop, <see cref="OnGainFocus"/> refresca el Continue).
        /// </summary>
        private void OnOptionsClicked()
        {
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PushOverlay<OptionsScreen>();
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no esta registrado — no se puede abrir opciones.", this);
            }
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
