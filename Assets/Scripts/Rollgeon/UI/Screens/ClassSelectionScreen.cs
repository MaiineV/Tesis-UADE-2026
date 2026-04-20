using System;
using Patterns;
using Rollgeon.Heroes;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Pantalla de seleccion de clase (Sprint03 #98). Muestra tres botones de clase
    /// (<b>Guerrero</b> disponible, <b>Mago</b>/<b>Picaro</b> bloqueados) y un panel
    /// derecho con el contrato del heroe + pasiva + portrait. Al confirmar crea un
    /// <see cref="BuildSelectionPayload"/> y navega a <c>BuildSelectionScreen</c>
    /// (UI#0013a). OnRunStart se dispara downstream via RunBootstrapper.
    /// Plan §4.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ScreenStringId.</b> Literal <c>"ClassSelectionScreen"</c> — debe matchear el
    /// string que <c>MainMenuScreen.OnPlayClicked</c> pushea (ver
    /// <c>MainMenuScreen.cs</c> ClassSelectionScreenId const y plan §4.1 / brief §5).
    /// </para>
    /// <para>
    /// [SETUP] El GameObject vive como hijo del Canvas de <c>01_MainMenu.unity</c>
    /// (Opcion A del plan §8.3). Buttons, TMP labels, Image portrait y el
    /// <see cref="ContractDisplayView"/> se cablean en engine via Inspector — ver
    /// <c>docs/setup/UI#0098_ClassSelectionScreen.md §8.4</c>.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Class Selection Screen")]
    public class ClassSelectionScreen : BaseScreen
    {
        private const string LogPrefix = "[ClassSelectionScreen] ";
        private const string ClassSelectionScreenId = "ClassSelectionScreen";

        // ---- Hero data -------------------------------------------------------

        [Title("Screen — Class Selection")]
        [Required("Arrastrar el asset ClassHero_Warrior.asset (ver instructivo §8.2).")]
        [SerializeField]
        [Tooltip("ClassHeroSO del Guerrero. Unico heroe disponible en Sprint03 (plan §4.1).")]
        private ClassHeroSO _warriorHero;

        [SerializeField]
        [Tooltip("String-id de la siguiente screen a pushear al confirmar. Default " +
                 "'BuildSelectionScreen' — stub graceful hasta que la tarea T-build mergee.")]
        private string _nextScreenStringId = "BuildSelectionScreen";

        [SerializeField]
        [Tooltip("Ruleset id que viaja con OnRunStart (schema §1.2). Default 'default'.")]
        private string _rulesetId = "default";

        // ---- Class buttons ---------------------------------------------------

        [Title("Class Buttons")]
        [Required("Arrastrar el Button del Guerrero (ver instructivo §8.4).")]
        [SerializeField]
        private Button _warriorButton;

        [Required("Arrastrar el Button del Mago (bloqueado, non-interactable).")]
        [SerializeField]
        private Button _magoButton;

        [Required("Arrastrar el Button del Picaro (bloqueado, non-interactable).")]
        [SerializeField]
        private Button _picaroButton;

        [Required("Arrastrar el Button Confirmar.")]
        [SerializeField]
        private Button _confirmButton;

        // ---- Right panel -----------------------------------------------------

        [Title("Right Panel")]
        [Required("Arrastrar el ContractDisplayView del panel derecho.")]
        [SerializeField]
        private ContractDisplayView _contractDisplay;

        [Required("Arrastrar el Image del portrait.")]
        [SerializeField]
        private Image _portraitDisplay;

        [Required("Arrastrar el TMP de la pasiva.")]
        [SerializeField]
        private TextMeshProUGUI _passiveDisplay;

        [Required("Arrastrar el GameObject del highlight/indicador de seleccion del Guerrero.")]
        [SerializeField]
        [Tooltip("GameObject que se activa cuando el Guerrero esta seleccionado (outline, " +
                 "check icon, etc.). Lo define la UX en engine.")]
        private GameObject _warriorSelectionIndicator;

        // ---- State -----------------------------------------------------------

        private ClassHeroSO _selectedHero;

        /// <inheritdoc/>
        public override string ScreenStringId => ClassSelectionScreenId;

        // ---- Lifecycle -------------------------------------------------------

        /// <summary>
        /// Wirea listeners, deshabilita Mago/Picaro, deja Confirm deshabilitado y el panel
        /// derecho limpio. No auto-selecciona: el usuario debe clickear el Guerrero
        /// explicitamente (brief §7 — "On Warrior button click: set selected, populate panel").
        /// </summary>
        protected override void OnPushed(IScreenPayload payload)
        {
            if (_warriorButton != null)
            {
                _warriorButton.onClick.AddListener(OnWarriorClicked);
                _warriorButton.interactable = true;
            }
            else
            {
                Debug.LogWarning(LogPrefix + "_warriorButton no esta cableado.", this);
            }

            if (_magoButton != null)
            {
                _magoButton.interactable = false;
            }
            else
            {
                Debug.LogWarning(LogPrefix + "_magoButton no esta cableado.", this);
            }

            if (_picaroButton != null)
            {
                _picaroButton.interactable = false;
            }
            else
            {
                Debug.LogWarning(LogPrefix + "_picaroButton no esta cableado.", this);
            }

            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(OnConfirmClicked);
                _confirmButton.interactable = false;
            }
            else
            {
                Debug.LogWarning(LogPrefix + "_confirmButton no esta cableado.", this);
            }

            if (_warriorSelectionIndicator != null)
            {
                _warriorSelectionIndicator.SetActive(false);
            }

            _selectedHero = null;
        }

        /// <summary>
        /// Desuscribe listeners y limpia el estado visual.
        /// </summary>
        protected override void OnPopped()
        {
            if (_warriorButton != null) _warriorButton.onClick.RemoveListener(OnWarriorClicked);
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            _selectedHero = null;
        }

        // ---- Handlers --------------------------------------------------------

        private void OnWarriorClicked()
        {
            SelectWarrior();
        }

        /// <summary>
        /// Marca al Guerrero como seleccionado y puebla el panel derecho con su
        /// <see cref="ClassHeroSO.Portrait"/>, <see cref="ContractSheet"/> y la pasiva
        /// (literal <c>"Pasiva: TBD"</c> hasta que mergee Hero Template).
        /// Idempotente — reclickear no rompe nada.
        /// </summary>
        private void SelectWarrior()
        {
            if (_warriorHero == null)
            {
                Debug.LogWarning(LogPrefix + "_warriorHero no esta cableado — no se puede seleccionar.", this);
                return;
            }

            _selectedHero = _warriorHero;

            if (_portraitDisplay != null && _warriorHero.Portrait != null)
            {
                _portraitDisplay.sprite = _warriorHero.Portrait;
            }

            if (_contractDisplay != null)
            {
                _contractDisplay.Bind(_warriorHero.Sheet);
            }

            if (_passiveDisplay != null)
            {
                _passiveDisplay.text = "Pasiva: TBD";
            }

            if (_warriorSelectionIndicator != null)
            {
                _warriorSelectionIndicator.SetActive(true);
            }

            if (_confirmButton != null)
            {
                _confirmButton.interactable = true;
            }
        }

        /// <summary>
        /// Creates a <see cref="BuildSelectionPayload"/> with the selected hero and a new
        /// run id, then navigates to <c>_nextScreenStringId</c> (BuildSelectionScreen).
        /// OnRunStart is now fired downstream by <see cref="Rollgeon.Run.RunBootstrapper.StartRun"/>
        /// inside <see cref="BuildSelectionScreen.OnConfirmClicked"/> (UI#0013a).
        /// </summary>
        // [STUB] IRngService §17.O — cuando mergee, reemplazar Guid.NewGuid() por
        //        ServiceLocator.GetService<IRngService>().NewRunId(). Convencion
        //        igual a T100/T102.
        private void OnConfirmClicked()
        {
            if (_selectedHero == null)
            {
                Debug.LogWarning(LogPrefix + "Confirm con _selectedHero == null — early return.", this);
                return;
            }

            var runId = Guid.NewGuid();
            var heroId = _selectedHero.EntityId ?? "<null>";
            // OnRunStart is now fired by BuildSelectionScreen via RunBootstrapper.StartRun
            // (removed from here to avoid double-firing)

            Debug.Log(LogPrefix + $"Navigating to build selection. heroId={heroId}, runId={runId}, next={_nextScreenStringId}", this);

            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no esta registrado — no se puede navegar.", this);
                return;
            }

            if (!string.IsNullOrEmpty(_nextScreenStringId))
            {
                var payload = new BuildSelectionPayload
                {
                    SelectedHero = _selectedHero,
                    RunId = runId,
                    RulesetId = _rulesetId
                };
                screens.PushByStringId(_nextScreenStringId, payload);
            }
        }
    }
}
