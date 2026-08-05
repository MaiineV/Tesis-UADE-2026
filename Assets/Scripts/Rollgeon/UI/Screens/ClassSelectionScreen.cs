using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Rollgeon.Meta;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Tooltips;
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

        [Title("Navigation")]
        [SerializeField, Optional]
        [Tooltip("Button Atrás — popea la screen y vuelve al menú. Lo cablea el installer.")]
        private Button _backButton;

        [Title("Juice (opcional)")]
        [SerializeField, Optional, Tooltip("CanvasGroup del root para el fade de entrada.")]
        private CanvasGroup _rootCanvasGroup;

        [SerializeField, Optional, Tooltip("Marco del retrato — pop de escala en la entrada.")]
        private RectTransform _portraitFrame;

        [SerializeField, Optional, Tooltip("Panel del contrato — pop de escala en la entrada.")]
        private RectTransform _contractPanel;

        [SerializeField] private float _entranceFadeDuration = 0.2f;
        [SerializeField] private float _entrancePopDuration = 0.28f;

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

            [Tooltip("TargetId del unlock cuando la clase aún no tiene ClassHeroSO " +
                     "(ej. \"Mage\"). Ignorado si Hero está cableado.")]
            public string ClassId;

            [Tooltip("Button de la clase en el panel izquierdo.")]
            public Button Button;

            [Tooltip("Indicador de selección (outline/check). Opcional.")]
            public GameObject SelectionIndicator;

            [Tooltip("Candado visual mostrado mientras la clase está bloqueada. Opcional.")]
            public GameObject LockIndicator;

            /// <summary>TargetId efectivo contra el sistema de unlocks.</summary>
            public string ResolvedTargetId =>
                Hero != null && !string.IsNullOrEmpty(Hero.EntityId) ? Hero.EntityId : ClassId;
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

            ApplyLegacyLock(_magoButton, "_magoButton");
            ApplyLegacyLock(_picaroButton, "_picaroButton");

            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(OnConfirmClicked);
                _confirmButton.interactable = false;
            }
            else
            {
                Debug.LogWarning(LogPrefix + "_confirmButton no esta cableado.", this);
            }

            if (_backButton != null)
            {
                _backButton.onClick.AddListener(OnBackClicked);
            }

            if (_warriorSelectionIndicator != null)
            {
                _warriorSelectionIndicator.SetActive(false);
            }

            _selectedHero = null;

            // Default del mock: el Guerrero arranca seleccionado (panel poblado
            // y Confirm habilitado desde el primer frame).
            SelectWarrior();

            PlayEntrance();
        }

        /// <summary>
        /// Desuscribe listeners y limpia el estado visual.
        /// </summary>
        protected override void OnPopped()
        {
            if (_warriorButton != null) _warriorButton.onClick.RemoveListener(OnWarriorClicked);
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);

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

        private void OnBackClicked()
        {
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PopCurrent();
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no esta registrado — no se puede volver.", this);
            }
        }

        /// <summary>
        /// Entrada juicy (receta OptionsScreen): fade del root + pop del marco del
        /// retrato y del panel de contrato. Gated por isPlaying — los tests
        /// EditMode invocan OnPushed y PrimeTween no corre en edit mode.
        /// </summary>
        private void PlayEntrance()
        {
            if (!Application.isPlaying) return;

            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 0f;
                PrimeTween.Tween.Alpha(_rootCanvasGroup, 1f, _entranceFadeDuration,
                    PrimeTween.Ease.OutQuad, useUnscaledTime: true);
            }

            PopIn(_portraitFrame);
            PopIn(_contractPanel);
        }

        private void PopIn(RectTransform target)
        {
            if (target == null) return;
            target.localScale = Vector3.one * 0.92f;
            PrimeTween.Tween.Scale(target, 1f, _entrancePopDuration,
                PrimeTween.Ease.OutBack, useUnscaledTime: true);
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

                string targetId = entry.ResolvedTargetId;
                bool gateOpen = !string.IsNullOrEmpty(targetId) &&
                    MetaUnlockGate.IsAvailable(UnlockableCategory.HeroClass, targetId);
                // Sin ClassHeroSO no hay nada que seleccionar, aunque el gate degrade
                // a "disponible" (ej. servicio meta sin registrar en tests/escenas sueltas).
                bool available = gateOpen && entry.Hero != null;

                entry.Button.interactable = available;
                if (entry.LockIndicator != null) entry.LockIndicator.SetActive(!available);
                if (entry.SelectionIndicator != null) entry.SelectionIndicator.SetActive(false);

                ConfigureLockTooltip(entry.Button, available, targetId);

                if (!available) continue;

                var captured = entry;
                UnityAction handler = () => SelectHero(captured.Hero, captured.SelectionIndicator);
                entry.Button.onClick.AddListener(handler);
                _classButtonHandlers.Add((entry.Button, handler));
            }
        }

        /// <summary>
        /// Fuerza el lock legacy de Mago/Pícaro solo cuando el botón no está
        /// gestionado por <see cref="_unlockableClasses"/> — el gating real vive en
        /// <see cref="WireUnlockableClasses"/>; esto cubre escenas/tests sin wiring #164.
        /// </summary>
        private void ApplyLegacyLock(Button button, string fieldName)
        {
            if (button == null)
            {
                Debug.LogWarning(LogPrefix + fieldName + " no esta cableado.", this);
                return;
            }

            foreach (var entry in _unlockableClasses)
            {
                if (entry?.Button == button) return;
            }

            button.interactable = false;
        }

        /// <summary>
        /// Deja el tooltip de "cómo se desbloquea" en el botón de la clase. En
        /// available se anula el provider (string vacío) para que el trigger no caiga
        /// al AutoResolve de otro componente. El provider se evalúa en cada hover,
        /// así que sigue el idioma activo sin LocalizationRefresh.
        /// </summary>
        private static void ConfigureLockTooltip(Button button, bool available, string targetId)
        {
            var trigger = button.GetComponent<UITooltipTrigger>();
            if (available)
            {
                if (trigger != null) trigger.TextProvider = () => string.Empty;
                return;
            }

            if (trigger == null) trigger = button.gameObject.AddComponent<UITooltipTrigger>();
            trigger.TextProvider = () => ResolveLockedTooltip(targetId);
        }

        /// <summary>
        /// Pista de desbloqueo para una clase bloqueada: el hint localizado de su
        /// <see cref="UnlockDefinitionSO"/> si existe, sino el fallback genérico de
        /// la tabla UI.
        /// </summary>
        public static string ResolveLockedTooltip(string targetId)
        {
            if (!string.IsNullOrEmpty(targetId) &&
                ServiceLocator.TryGetService<IMetaProgressionService>(out var meta) &&
                meta != null)
            {
                var defs = meta.Definitions;
                for (int i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    if (def != null &&
                        def.Category == UnlockableCategory.HeroClass &&
                        string.Equals(def.TargetId, targetId, StringComparison.Ordinal))
                    {
                        return LocalizedContent.Hint(def.UnlockId, def.HintText);
                    }
                }
            }

            return LocalizedContent.Ui("class_select.locked_tooltip", "Próximamente");
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
                bool changed = _portraitDisplay.sprite != hero.Portrait;
                _portraitDisplay.sprite = hero.Portrait;

                // Punch sutil solo cuando el retrato realmente cambia.
                if (changed && Application.isPlaying)
                {
                    PrimeTween.Tween.PunchScale(_portraitDisplay.transform,
                        Vector3.one * 0.05f, 0.25f, frequency: 4, useUnscaledTime: true);
                }
            }

            if (_contractDisplay != null)
            {
                _contractDisplay.Bind(hero.Sheet);
            }

            if (_passiveDisplay != null)
            {
                _passiveDisplay.text = hero.Passive != null
                    ? LocalizedContent.Description(hero.Passive.PassiveId, hero.Passive.Description)
                    : "Pasiva: TBD";
            }

            if (_warriorSelectionIndicator != null)
            {
                SetIndicator(_warriorSelectionIndicator,
                    ReferenceEquals(selectionIndicator, _warriorSelectionIndicator));
            }
            foreach (var entry in _unlockableClasses)
            {
                if (entry?.SelectionIndicator != null)
                {
                    SetIndicator(entry.SelectionIndicator,
                        ReferenceEquals(selectionIndicator, entry.SelectionIndicator));
                }
            }

            if (_confirmButton != null)
            {
                _confirmButton.interactable = true;
            }
        }

        /// <summary>
        /// Activa/desactiva un indicador de selección (el underline dorado) con un
        /// grow horizontal al prenderse. Instantáneo en edit mode.
        /// </summary>
        private static void SetIndicator(GameObject indicator, bool active)
        {
            bool wasActive = indicator.activeSelf;
            indicator.SetActive(active);

            // El marco del ícono acompaña la selección (sheet_7 / sheet_8) — el
            // componente vive en el botón, ancestro del underline. Opcional.
            // includeInactive: el underline recién apagado no debe cortar la búsqueda.
            var frame = indicator.GetComponentInParent<ClassSelectionFrameView>(true);
            if (frame != null) frame.SetSelected(active);

            if (active && !wasActive && Application.isPlaying)
            {
                var t = indicator.transform;
                t.localScale = new Vector3(0f, 1f, 1f);
                PrimeTween.Tween.ScaleX(t, 1f, 0.18f, PrimeTween.Ease.OutBack, useUnscaledTime: true);
            }
            else if (!active)
            {
                indicator.transform.localScale = Vector3.one;
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
