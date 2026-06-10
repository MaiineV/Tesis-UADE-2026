using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Heroes;
using Rollgeon.Meta;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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

        [Title("Unlockable Classes (#164)")]
        [InfoBox("Clases adicionales gateadas por meta-progresión (ej. Berserker, Gambler). " +
                 "El botón queda interactable solo si la clase está desbloqueada según " +
                 "IMetaProgressionService. Lista vacía = solo Guerrero (comportamiento legacy).")]
        [SerializeField]
        private List<SelectableClassEntry> _unlockableClasses = new List<SelectableClassEntry>();

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
        private readonly List<(Button button, UnityAction handler)> _classButtonHandlers
            = new List<(Button, UnityAction)>();

        /// <summary>Entry de clase desbloqueable cableada en el Inspector (#164).</summary>
        [Serializable]
        public class SelectableClassEntry
        {
            [Tooltip("ClassHeroSO de la clase (ej. CH_Berserker).")]
            public ClassHeroSO Hero;

            [Tooltip("Button de la clase en el panel izquierdo.")]
            public Button Button;

            [Tooltip("Indicador de selección (outline/check). Opcional.")]
            public GameObject SelectionIndicator;

            [Tooltip("Candado visual mostrado mientras la clase está bloqueada. Opcional.")]
            public GameObject LockIndicator;
        }

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
                // El Guerrero pertenece al pool base (#164) — sin definición de
                // unlock que lo gatee, IsAvailable devuelve true.
                _warriorButton.interactable = _warriorHero == null ||
                    MetaUnlockGate.IsAvailable(UnlockableCategory.HeroClass, _warriorHero.EntityId);
            }
            else
            {
                Debug.LogWarning(LogPrefix + "_warriorButton no esta cableado.", this);
            }

            WireUnlockableClasses();

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

            foreach (var (button, handler) in _classButtonHandlers)
            {
                if (button != null) button.onClick.RemoveListener(handler);
            }
            _classButtonHandlers.Clear();

            _selectedHero = null;
        }

        // ---- Handlers --------------------------------------------------------

        private void OnWarriorClicked()
        {
            SelectWarrior();
        }

        /// <summary>
        /// Cablea las clases desbloqueables (#164): cada entry queda interactable
        /// solo si su clase está disponible según <see cref="MetaUnlockGate"/>, con
        /// candado visible mientras está bloqueada.
        /// </summary>
        private void WireUnlockableClasses()
        {
            foreach (var entry in _unlockableClasses)
            {
                if (entry?.Button == null) continue;

                bool available = entry.Hero != null &&
                    MetaUnlockGate.IsAvailable(UnlockableCategory.HeroClass, entry.Hero.EntityId);

                entry.Button.interactable = available;
                if (entry.LockIndicator != null) entry.LockIndicator.SetActive(!available);
                if (entry.SelectionIndicator != null) entry.SelectionIndicator.SetActive(false);

                if (!available) continue;

                var captured = entry;
                UnityAction handler = () => SelectHero(captured.Hero, captured.SelectionIndicator);
                entry.Button.onClick.AddListener(handler);
                _classButtonHandlers.Add((entry.Button, handler));
            }
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

            SelectHero(_warriorHero, _warriorSelectionIndicator);
        }

        /// <summary>
        /// Selección generalizada (#164): setea el héroe, puebla el panel derecho
        /// y deja un único indicador de selección activo.
        /// </summary>
        private void SelectHero(ClassHeroSO hero, GameObject selectionIndicator)
        {
            if (hero == null) return;

            _selectedHero = hero;

            if (_portraitDisplay != null && hero.Portrait != null)
            {
                _portraitDisplay.sprite = hero.Portrait;
            }

            if (_contractDisplay != null)
            {
                _contractDisplay.Bind(hero.Sheet);
            }

            if (_passiveDisplay != null)
            {
                _passiveDisplay.text = hero.Passive != null ? hero.Passive.Description : "Pasiva: TBD";
            }

            if (_warriorSelectionIndicator != null)
            {
                _warriorSelectionIndicator.SetActive(ReferenceEquals(selectionIndicator, _warriorSelectionIndicator));
            }
            foreach (var entry in _unlockableClasses)
            {
                if (entry?.SelectionIndicator != null)
                {
                    entry.SelectionIndicator.SetActive(ReferenceEquals(selectionIndicator, entry.SelectionIndicator));
                }
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
