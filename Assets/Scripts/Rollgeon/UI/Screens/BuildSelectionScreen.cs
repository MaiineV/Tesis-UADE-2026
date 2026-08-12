using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Rollgeon.Meta;
using Rollgeon.Run;
using Rollgeon.Tutorial.UI;
using Rollgeon.UI;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Help;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CoroutineHost = Rollgeon.Patterns.CoroutineHost;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Build selection screen (UI#0013a). Shows the selected hero's info,
    /// dice bag preview, and confirm/back buttons. On confirm, stores the
    /// selected hero in <see cref="PendingRunRequest"/> and loads
    /// <c>02_Gameplay</c>. <see cref="RunBootstrapper.StartRun"/> is fired
    /// downstream by <c>GameplayBootstrapper</c> in the new scene.
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

        [Title("Dice Bag Builder (Fase 2)")]
        [Tooltip("Container donde se instancia un PoolOfferingRow por cada DicePoolEntry del hero. " +
                 "Si es null o el hero no trae DiceBagPool, la screen cae al modo legacy.")]
        [SerializeField] private Transform _poolOfferingsContainer;

        [Tooltip("Prefab de la fila +/- del pool. Tener en cuenta que se instancia uno por tipo de dado.")]
        [SerializeField] private PoolOfferingRow _poolOfferingPrefab;

        [Tooltip("Label que muestra 'X / RequiredBagSize'. Opcional.")]
        [SerializeField, Optional] private TextMeshProUGUI _bagCounterLabel;

        [Tooltip("Boton para vaciar la bolsa actual. Opcional.")]
        [SerializeField, Optional] private Button _clearBagButton;

        [Title("Mock rework (opcional)")]
        [Tooltip("Tira de la bolsa ordenada menor→mayor con juice. Con esto cableado " +
                 "reemplaza a _diceContainer/_diceSlotPrefab (que quedan como legacy).")]
        [SerializeField, Optional] private DiceStripView _diceStrip;

        [Tooltip("Sprites por tipo + tunables de la tira. Requerido si _diceStrip está cableado.")]
        [SerializeField, Optional] private DiceBuildUiSettingsSO _diceUiSettings;

        [Title("Ayuda (coach-marks)")]
        [Tooltip("Botón '?' de la esquina. Repite la guía cuantas veces el jugador quiera. Opcional.")]
        [SerializeField, Optional] private Button _helpButton;

        [Tooltip("Colchón extra sobre la entrada juicy antes de arrancar la guía automática. " +
                 "Sin esto los pasos anclarían a botones todavía invisibles y en movimiento.")]
        [SerializeField] private float _helpStartExtraDelay = 0.15f;

        // ---- State ----
        private ClassHeroSO _selectedHero;
        private Guid _runId;
        private string _rulesetId;

        // Builder mode (Fase 2). _builderMode == true cuando el hero trae un DiceBagPool
        // y la screen tiene container/prefab cableados; si no, mantiene el flujo legacy.
        private bool _builderMode;
        private BuildHelpFlow _helpFlow;
        private IBuildHelpSeenStore _helpSeenStore;
        private Coroutine _helpAutoStart;

        private readonly List<DiceType> _currentBag = new();
        private readonly List<PoolOfferingRow> _poolRows = new();
        private int _lastShownBagCount = -1;

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
                _heroNameLabel.text = LocalizedContent.Name(_selectedHero.EntityId, _selectedHero.DisplayName ?? "");
            if (_heroDescriptionLabel != null && _selectedHero != null)
                _heroDescriptionLabel.text = LocalizedContent.Description(_selectedHero.EntityId, _selectedHero.Description ?? "");
            if (_heroPortrait != null && _selectedHero != null && _selectedHero.Portrait != null)
                _heroPortrait.sprite = _selectedHero.Portrait;

            // Populate dice bag (builder o legacy)
            PopulateDiceBag();

            // Wire buttons
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_backButton != null) _backButton.onClick.AddListener(OnBackClicked);
            if (_clearBagButton != null) _clearBagButton.onClick.AddListener(OnClearBagClicked);

            if (_helpButton != null) _helpButton.onClick.AddListener(OnHelpClicked);

            if (_diceStrip != null)
            {
                _diceStrip.Configure(_diceUiSettings);
                // Click en un dado de la tira = quitarlo de la bolsa (mock).
                _diceStrip.OnDieClicked += OnRemoveDice;
            }

            TryAutoStartHelp();
        }

        protected override void OnPopped()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);
            if (_clearBagButton != null) _clearBagButton.onClick.RemoveListener(OnClearBagClicked);
            if (_helpButton != null) _helpButton.onClick.RemoveListener(OnHelpClicked);
            if (_diceStrip != null) _diceStrip.OnDieClicked -= OnRemoveDice;

            StopHelp();
            ClearPoolRows();
            ClearDiceSlots();
            _currentBag.Clear();
            _builderMode = false;
            _selectedHero = null;
        }

        private void PopulateDiceBag()
        {
            ClearPoolRows();
            ClearDiceSlots();
            _currentBag.Clear();
            _builderMode = false;
            _lastShownBagCount = -1;

            // Modo builder (Fase 2): el hero trae un pool valido y la screen esta cableada.
            var pool = _selectedHero != null ? _selectedHero.DiceBagPool : null;
            if (pool != null && _poolOfferingsContainer != null && _poolOfferingPrefab != null)
            {
                BuildPoolUI(pool);
                _builderMode = true;
                if (_diceBagFallbackLabel != null) _diceBagFallbackLabel.gameObject.SetActive(false);
                RefreshUI();
                return;
            }

            // Modo legacy (Fase 1 fallback): mostrar nombre del SO opaco si existe.
            bool hasBag = false;
            if (_selectedHero != null && _selectedHero.StartingDiceBagRef != null)
            {
                if (_diceSlotPrefab != null && _diceContainer != null)
                {
                    var slot = Instantiate(_diceSlotPrefab, _diceContainer);
                    slot.Bind(_selectedHero.StartingDiceBagRef.name);
                    hasBag = true;
                }
            }

            if (_diceBagFallbackLabel != null)
                _diceBagFallbackLabel.gameObject.SetActive(!hasBag);

            // Confirm en legacy queda habilitado (no hay bolsa que armar).
            if (_confirmButton != null) _confirmButton.interactable = true;
        }

        private void BuildPoolUI(DiceBagPoolSO pool)
        {
            foreach (var entry in pool.Offerings)
            {
                // Meta-progresión (#164): dados gateados no se ofrecen hasta desbloquearse.
                if (!MetaUnlockGate.IsAvailable(UnlockableCategory.Dice, entry.Type.ToString())) continue;

                var row = Instantiate(_poolOfferingPrefab, _poolOfferingsContainer);
                var sprite = _diceUiSettings != null ? _diceUiSettings.GetSprite(entry.Type) : null;
                row.Bind(entry.Type, entry.MaxInBag, sprite);
                row.OnAddRequested += OnAddDice;
                row.OnRemoveRequested += OnRemoveDice;
                _poolRows.Add(row);
            }

            // Separadores entre filas como en el mock: todas menos la última.
            for (int i = 0; i < _poolRows.Count; i++)
            {
                _poolRows[i].SetDividerVisible(i < _poolRows.Count - 1);
            }
        }

        private void OnAddDice(DiceType type)
        {
            var pool = _selectedHero?.DiceBagPool;
            if (pool == null) return;
            if (_currentBag.Count >= pool.RequiredBagSize) return;

            int currentOfType = _currentBag.Count(d => d == type);
            int maxOfType = pool.MaxFor(type);
            if (currentOfType >= maxOfType) return;

            _currentBag.Add(type);
            RefreshUI();
        }

        private void OnRemoveDice(DiceType type)
        {
            // Saca la ultima ocurrencia (LIFO se siente natural en UI).
            int lastIndex = _currentBag.LastIndexOf(type);
            if (lastIndex < 0) return;
            _currentBag.RemoveAt(lastIndex);
            RefreshUI();
        }

        private void OnClearBagClicked()
        {
            _currentBag.Clear();
            RefreshUI();
        }

        // ======================================================================
        // Ayuda (coach-marks)
        // ======================================================================

        // Pedida a mano: la pantalla ya está quieta hace rato, así que no hay nada que
        // esperar. Hacerla esperar la entrada juicy se sentía como que el botón no respondía.
        private void OnHelpClicked() => StartHelp(waitForEntrance: false);

        /// <summary>
        /// Primera visita: la guía se muestra sola. No corre en modo legacy (sin pool no
        /// hay nada que explicar y el texto mentiría) ni si el jugador apagó el tutorial
        /// en opciones — el botón '?' nunca se gatea, ahí la pide él.
        /// </summary>
        private void TryAutoStartHelp()
        {
            if (!_builderMode) return;
            if (SeenStore.HasSeen) return;
            if (!IsTutorialEnabled()) return;

            // Acá sí esperamos: la screen se acaba de mostrar y todavía está armándose.
            StartHelp(waitForEntrance: true);
        }

        private void StartHelp(bool waitForEntrance)
        {
            if (!_builderMode) return;

            // La guía necesita una corrutina para esperar el layout, y una corrutina
            // necesita el GameObject activo. En runtime el ScreenManager activa la screen
            // antes del OnPushed, pero un caller que la pushee sin mostrarla (los tests
            // del builder) no debería comerse un error por pedir ayuda que nadie ve.
            if (!isActiveAndEnabled) return;

            if (_helpAutoStart != null) StopCoroutine(_helpAutoStart);
            _helpAutoStart = StartCoroutine(StartHelpDeferred(waitForEntrance));
        }

        /// <summary>
        /// Espera un frame + rebuild de layout antes de anclar nada: las filas del pool se
        /// instancian en <see cref="OnPushed"/> y su layout group no resolvió todavía
        /// (medirían 0). Con <paramref name="waitForEntrance"/> espera además la entrada
        /// staggered de los botones, que arrancan en alpha 0 y moviéndose — anclar ahí
        /// deja el recorte persiguiendo un target invisible.
        /// </summary>
        private IEnumerator StartHelpDeferred(bool waitForEntrance)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (waitForEntrance)
            {
                float entrance = _helpStartExtraDelay;
                if (TryGetComponent<Rollgeon.UI.Menu.JuicyMenuGroup>(out var juicyGroup))
                    entrance += juicyGroup.EntranceTotalSeconds;
                if (entrance > 0f) yield return new WaitForSecondsRealtime(entrance);
            }

            _helpAutoStart = null;

            _helpFlow ??= new BuildHelpFlow(
                ServiceLocator.TryGetService<ITutorialOverlayService>(out var overlay) ? overlay : null);

            // Solo marcamos "visto" si de verdad se mostró: sin overlay registrado (abrir
            // la escena suelta en el editor) no queremos quemarle el auto-disparo al jugador.
            if (_helpFlow.Start(BuildHelpSteps())) SeenStore.MarkSeen();
        }

        private void StopHelp()
        {
            if (_helpAutoStart != null)
            {
                StopCoroutine(_helpAutoStart);
                _helpAutoStart = null;
            }
            _helpFlow?.Stop();
        }

        private IEnumerable<BuildHelpFlow.Step> BuildHelpSteps()
        {
            yield return new BuildHelpFlow.Step(
                _poolOfferingsContainer as RectTransform,
                BuildHelpTextKeys.Pool,
                "Estos son los dados de tu clase. Haz clic en uno para sumarlo a la bolsa; " +
                "el número de cada fila dice cuántos puedes llevar de ese tipo.");

            // Solo la tira. Sumar el contador al recorte (que está en la esquina opuesta)
            // hacía que el spotlight abarcara el bounding box de ambos: un círculo enorme
            // centrado entre los dos, o sea sobre nada.
            yield return new BuildHelpFlow.Step(
                _diceStrip != null ? _diceStrip.transform as RectTransform : null,
                BuildHelpTextKeys.Strip,
                "Tu bolsa se arma aquí, siempre ordenada de menor a mayor. Haz clic en un " +
                "dado de la tira para devolverlo al pool.");

            yield return new BuildHelpFlow.Step(
                _clearBagButton != null ? _clearBagButton.transform as RectTransform : null,
                BuildHelpTextKeys.Clear,
                "Limpiar vacía la bolsa entera y te deja empezar de cero.");

            yield return new BuildHelpFlow.Step(
                _confirmButton != null ? _confirmButton.transform as RectTransform : null,
                BuildHelpTextKeys.Confirm,
                "Cuando completes la bolsa, Confirmar se habilita y arranca la run.");
        }

        private IBuildHelpSeenStore SeenStore => _helpSeenStore ??= new BuildHelpPrefs();

        /// <summary>Inyección para tests — evita que el auto-disparo dependa de PlayerPrefs.</summary>
        public void SetHelpSeenStore(IBuildHelpSeenStore store) => _helpSeenStore = store;

        private static bool IsTutorialEnabled()
        {
            // Sin servicio (escena suelta en el editor) asumimos habilitado, igual que
            // hace OptionsScreen al pintar el toggle.
            return !ServiceLocator.TryGetService<IMetaProgressionService>(out var meta)
                   || meta == null
                   || meta.IsTutorialEnabled;
        }

        private void RefreshUI()
        {
            if (!_builderMode) return;

            var pool = _selectedHero.DiceBagPool;
            int required = pool.RequiredBagSize;
            bool bagHasRoom = _currentBag.Count < required;

            // Refrescar contadores de cada fila.
            foreach (var row in _poolRows)
            {
                int currentOfType = _currentBag.Count(d => d == row.Type);
                row.Refresh(currentOfType, bagHasRoom);
            }

            // Reconstruir la preview de la bolsa armada.
            RebuildSelectedSlots();

            // Counter "X / Y" con punch al cambiar.
            if (_bagCounterLabel != null)
            {
                _bagCounterLabel.text = $"{_currentBag.Count} / {required}";

                bool countChanged = _lastShownBagCount >= 0 && _lastShownBagCount != _currentBag.Count;
                if (countChanged && Application.isPlaying
                    && !Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion)
                {
                    var t = _bagCounterLabel.transform;
                    PrimeTween.Tween.StopAll(onTarget: t);
                    t.localScale = Vector3.one;
                    PrimeTween.Tween.PunchScale(t, Vector3.one * 0.15f, 0.22f,
                        frequency: 3, useUnscaledTime: true);
                }
            }

            // Bolsa completa: ola de celebración en la tira (una sola vez por llegada).
            if (_diceStrip != null && _currentBag.Count == required && _lastShownBagCount != required)
            {
                _diceStrip.PlayCompleteWave();
            }
            _lastShownBagCount = _currentBag.Count;

            // Confirm habilitado solo cuando esta en target.
            if (_confirmButton != null)
                _confirmButton.interactable = _currentBag.Count == required;
        }

        private void RebuildSelectedSlots()
        {
            // Path del mock: tira ordenada menor→mayor con diff + juice.
            if (_diceStrip != null)
            {
                _diceStrip.SetDice(DiceStripMath.SortAscending(_currentBag), animate: true);
                return;
            }

            // Path legacy (tests / prefabs viejos): destruir y re-instanciar.
            ClearDiceSlots();
            if (_diceContainer == null || _diceSlotPrefab == null) return;
            foreach (var dice in _currentBag)
            {
                var slot = Instantiate(_diceSlotPrefab, _diceContainer);
                slot.Bind(dice);
            }
        }

        private void ClearPoolRows()
        {
            foreach (var row in _poolRows)
            {
                if (row == null) continue;
                row.OnAddRequested -= OnAddDice;
                row.OnRemoveRequested -= OnRemoveDice;
                row.Unbind();
                DestroyGO(row.gameObject);
            }
            _poolRows.Clear();
        }

        private void ClearDiceSlots()
        {
            if (_diceContainer == null) return;
            for (int i = _diceContainer.childCount - 1; i >= 0; i--)
                DestroyGO(_diceContainer.GetChild(i).gameObject);
        }

        private static void DestroyGO(GameObject go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        private void OnConfirmClicked()
        {
            if (!TryBuildAndStoreRequest()) return;

            GameplaySceneFlow.LoadGameplay();
        }

        /// <summary>
        /// Valida estado, construye el <see cref="DiceBagSO"/> runtime si esta en
        /// builder mode, y lo persiste en <see cref="PendingRunRequest"/>. Devuelve
        /// <c>false</c> sin tocar nada si el estado no permite navegar (hero null o
        /// bolsa incompleta). Aislado de <see cref="SceneManager.LoadScene"/> para
        /// poder testearlo en EditMode.
        /// </summary>
        private bool TryBuildAndStoreRequest()
        {
            if (_selectedHero == null)
            {
                Debug.LogWarning(LogPrefix + "Confirm with null hero — ignoring.", this);
                return false;
            }

            DiceBagSO builtBag = null;
            if (_builderMode)
            {
                var pool = _selectedHero.DiceBagPool;
                if (_currentBag.Count != pool.RequiredBagSize)
                {
                    Debug.LogWarning(LogPrefix + $"Confirm con bolsa incompleta ({_currentBag.Count}/{pool.RequiredBagSize}) — ignoring.", this);
                    return false;
                }

                builtBag = ScriptableObject.CreateInstance<DiceBagSO>();
                builtBag.name = $"BuiltBag.{_selectedHero.EntityId}";
                builtBag.Dice = new List<DiceType>(_currentBag);
            }

            PendingRunRequest.Set(_selectedHero, _runId, _rulesetId, builtBag);
            Debug.Log(LogPrefix + $"Navigating to gameplay. hero={_selectedHero.EntityId}, runId={_runId}, builtBag={(builtBag != null ? builtBag.Dice.Count + " dice" : "null")}", this);
            return true;
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
