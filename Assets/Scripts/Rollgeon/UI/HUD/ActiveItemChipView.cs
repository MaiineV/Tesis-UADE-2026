using System;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Items;
using Rollgeon.Items.Active;
using Rollgeon.UI.HUD.DiceAnim;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LocalizedContent = Rollgeon.Localization.LocalizedContent;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// La ficha del <b>slot unico</b> de item activo. GDD "Ítems Activos" §18.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Solo existe en combate.</b> El GDD: "completamente oculta: fuera de combate".
    /// La ficha se apaga entera en exploracion, no se atenua.
    /// </para>
    /// <para>
    /// <b>El dado vive adentro de la ficha.</b> Es regla obligatoria del GDD: "el dado del
    /// ítem se ve dentro del slot, nunca junto a los 5 dados principales de combate".
    /// Al resolverse, la cara obtenida se muestra en la ficha y despues vuelve al reposo.
    /// </para>
    /// <para>
    /// El color de la ficha corresponde al pilar de combate del item. Los pilares todavia
    /// no estan definidos en el GDD ni en codigo, asi que <see cref="_placeholderTint"/>
    /// es un placeholder hasta que existan.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Active Item Chip View")]
    public class ActiveItemChipView : MonoBehaviour
    {
        private const string LogPrefix = "[ActiveItemChipView] ";

        [Title("Ficha — refs")]
        [Required("Arrastrar la Image de la ficha (el fondo redondo).")]
        [SerializeField]
        private Image _chip;

        [Required("Arrastrar la Image del icono del dado, centrada en la ficha.")]
        [SerializeField]
        private Image _dieIcon;

        [Tooltip("Label del numero obtenido en la tirada. Se muestra al resolver y se " +
                 "esconde al volver al reposo.")]
        [SerializeField]
        private TextMeshProUGUI _rollLabel;

        [SerializeField]
        private Button _button;

        [SerializeField]
        [Tooltip("Tooltip con el nombre del item y su tabla de bandas.")]
        private UITooltipTrigger _tooltip;

        [Title("Ficha — arte")]
        [InfoBox("La silueta del dado sale del DiceShapeCatalog, el mismo que usan la mesa " +
                 "de dados y los slots del modo classic. Vacio = se resuelve desde " +
                 "Resources/Dice/DiceShapeCatalog.")]
        [SerializeField]
        private Rollgeon.Dice.DiceShapeCatalogSO _shapeCatalog;

        [SerializeField]
        [Tooltip("PLACEHOLDER: color de la ficha. Va a salir del pilar de combate del " +
                 "item cuando los pilares esten definidos.")]
        private Color _placeholderTint = Color.white;

        [Title("Feel")]
        [InfoBox("Los tiempos de la tirada viven en ActiveItemRollFeelMath, que es logica " +
                 "pura y testeable. Aca solo van los colores por banda.")]
        [SerializeField]
        [Tooltip("Color del numero en banda negativa.")]
        private Color _negativeColor = new Color(0.85f, 0.35f, 0.30f);

        [SerializeField]
        [Tooltip("Color del numero en banda mixta.")]
        private Color _mixedColor = new Color(0.92f, 0.80f, 0.35f);

        [SerializeField]
        [Tooltip("Color del numero en banda positiva.")]
        private Color _positiveColor = new Color(0.45f, 0.90f, 0.50f);

        [SerializeField, MinValue(1f)]
        [Tooltip("Cuanto se aclara la ficha mientras espera que elijas el objetivo.")]
        private float _armedTintFactor = 1.45f;

        [ShowInInspector, ReadOnly]
        private ActiveItemBlock _block = ActiveItemBlock.NoItemEquipped;

        private IEquippedActiveItemService _equipped;
        private IActiveItemActivationService _activation;
        private bool _bound;
        // Tirada pendiente de decision (aceptar / re-tirar). _pendingStartedAt < 0 = no hay.
        private float _pendingStartedAt = -1f;
        private ActiveItemPendingRoll _pending;
        // Animacion de resolucion en curso. _resolveStartedAt < 0 = no hay.
        private float _resolveStartedAt = -1f;
        private ActiveItemActivationResult _lastResult;
        // Plan del giro, armado una vez por tirada con la coreografia compartida.
        private int _spinTickCount;
        private int _sideSeed;
        private int[] _previewFaces;
        private bool _showPreviewFaces;
        // Deteccion de transiciones para los hooks de juice.
        private ActiveItemRollPhase _lastShownPhase = ActiveItemRollPhase.Idle;
        private int _lastSpinTick;

        // ==================================================================
        // Hooks de juice (§19) — los consume ActiveItemChipJuice, que cuelga de
        // este mismo GameObject. La view marca los momentos; el juice pone SFX,
        // particulas, hit-stop y texto flotante.
        // ==================================================================

        /// <summary>Arranca el giro de una tirada pendiente (la primera o un reroll).</summary>
        public event Action<ActiveItemPendingRoll> RollSpinStarted;

        /// <summary>Un tick del giro (el dado "traquetea").</summary>
        public event Action SpinTicked;

        /// <summary>Se asento la cara cruda y la decision quedo abierta.</summary>
        public event Action<ActiveItemPendingRoll> RawFaceSettled;

        /// <summary>
        /// El jugador acepto y la activacion se resolvio — el payoff fuerte va aca.
        /// Si hubo encantamiento, el destello arranca en este mismo instante
        /// (mirar <see cref="ActiveItemActivationResult.WasEnchanted"/>).
        /// </summary>
        public event Action<ActiveItemActivationResult> ResultLanded;

        private const string AnimSettingsResourcePath = "Dice/DiceUiAnimationSettings";
        private DiceUiAnimationSettingsSO _animSettings;

        /// <summary>
        /// Tuning de animacion de dados del proyecto. Es el mismo asset que usan los dados
        /// de combate y el de movimiento: si alguien retoca el ritmo del giro alla, la ficha
        /// lo sigue sola en vez de quedar desincronizada con su propio juego de constantes.
        /// </summary>
        private DiceUiAnimationSettingsSO ResolveAnimSettings()
        {
            if (_animSettings != null) return _animSettings;
            _animSettings = Resources.Load<DiceUiAnimationSettingsSO>(AnimSettingsResourcePath);
            if (_animSettings == null)
                _animSettings = ScriptableObject.CreateInstance<DiceUiAnimationSettingsSO>();
            return _animSettings;
        }
        private Vector3 _chipRestScale = Vector3.one;

        // ==================================================================
        // Lifecycle
        // ==================================================================

        /// <summary>
        /// Captura la escala de reposo UNA vez, antes de que nada la anime.
        /// </summary>
        /// <remarks>
        /// No se puede re-muestrear del transform mas tarde: este mismo componente lo
        /// escala en <see cref="ApplyRollFrame"/>, asi que leerlo mientras hay una tirada
        /// en curso hornea el pop como nueva base. Como <see cref="EndRollAnimation"/>
        /// restaura a esa base, la ficha nunca vuelve a su tamaño y cada tirada
        /// interrumpida multiplica la anterior.
        /// </remarks>
        private void Awake()
        {
            if (_chip != null) _chipRestScale = _chip.transform.localScale;
        }

        private void OnEnable()
        {
            Bind();
            Refresh();
        }

        private void OnDisable() => Unbind();

        private void Bind()
        {
            if (_bound) return;

            ResolveServices();

            EventManager.Subscribe(EventName.OnCombatStart, HandleRefresh);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleRefresh);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleRefresh);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleRefresh);
            EventManager.Subscribe(EventName.OnPlayerRollsChanged, HandleRefresh);

            if (_button != null) _button.onClick.AddListener(HandleClick);
            if (_tooltip != null) _tooltip.TextProvider = BuildTooltip;

            _bound = true;
        }

        /// <summary>
        /// Resuelve los servicios de la run y engancha sus eventos. Idempotente y
        /// reintentable: el HUD despierta antes de que la run registre nada, asi que la
        /// primera pasada de <see cref="Bind"/> encuentra null y hay que volver a
        /// intentarlo en cada <see cref="Refresh"/>. Sin esto la ficha se pintaba bien
        /// pero nunca mostraba la cara obtenida, porque la suscripcion a
        /// <c>OnResolved</c> jamas llegaba a hacerse.
        /// </summary>
        private void ResolveServices()
        {
            if (_equipped == null
                && ServiceLocator.TryGetService<IEquippedActiveItemService>(out _equipped)
                && _equipped != null)
            {
                _equipped.OnEquippedChanged -= HandleEquippedChanged;
                _equipped.OnEquippedChanged += HandleEquippedChanged;
            }

            if (_activation == null
                && ServiceLocator.TryGetService<IActiveItemActivationService>(out _activation)
                && _activation != null)
            {
                _activation.OnRollPending -= HandleRollPending;
                _activation.OnRollPending += HandleRollPending;
                _activation.OnResolved -= HandleResolved;
                _activation.OnResolved += HandleResolved;
                _activation.OnSelectionStarted -= HandleRefreshNoArgs;
                _activation.OnSelectionStarted += HandleRefreshNoArgs;
                _activation.OnSelectionCancelled -= HandleRefreshNoArgs;
                _activation.OnSelectionCancelled += HandleRefreshNoArgs;
            }
        }

        private void Unbind()
        {
            if (!_bound) return;

            if (_equipped != null) _equipped.OnEquippedChanged -= HandleEquippedChanged;
            if (_activation != null)
            {
                _activation.OnRollPending -= HandleRollPending;
                _activation.OnResolved -= HandleResolved;
            }

            EventManager.UnSubscribe(EventName.OnCombatStart, HandleRefresh);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleRefresh);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleRefresh);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleRefresh);
            EventManager.UnSubscribe(EventName.OnPlayerRollsChanged, HandleRefresh);

            if (_button != null) _button.onClick.RemoveListener(HandleClick);

            _bound = false;
        }

        private void Update()
        {
            // La animacion avanza por tiempo, no por coroutine: si el HUD se desactiva a
            // mitad, la ficha no queda pegada mostrando la cara vieja.
            if (_resolveStartedAt >= 0f)
            {
                float elapsed = Time.unscaledTime - _resolveStartedAt;
                if (elapsed >= ActiveItemRollFeelMath.ResolveTotalSeconds(_lastResult.WasEnchanted))
                {
                    EndRollAnimation();
                    return;
                }

                ApplyResolveFrame(elapsed);
                return;
            }

            if (_pendingStartedAt < 0f) return;

            // El service puede descartar la pendiente sin resolverla (el combate se cerro
            // con la decision abierta) — la ficha no se queda pegada esperando un
            // OnResolved que no va a llegar.
            if (_activation == null || !_activation.IsAwaitingDecision)
            {
                EndRollAnimation();
                return;
            }

            ApplyPendingFrame(Time.unscaledTime - _pendingStartedAt);
        }

        /// <summary>
        /// Arma el giro con <see cref="DiceAnimChoreographer"/>: cuantos ticks entran, con
        /// que lateral arranca la rotacion y que caras preview cicla. Se precalcula una vez
        /// por tirada para que dos cuadros del mismo instante muestren lo mismo.
        /// </summary>
        private void BuildSpinPlan(ActiveItemPendingRoll pending)
        {
            var settings = ResolveAnimSettings();
            var t = settings.ToTimings();
            _showPreviewFaces = settings.ShowPreviewFacesDuringSpin;
            _spinTickCount = DiceAnimChoreographer.TickCount(t.SpinSeconds, t.SpinTickSeconds);
            _sideSeed = UnityEngine.Random.Range(0, 2);

            int faceMax = DiceAnimChoreographer.PreviewFaceRange(
                t.PreviewFaceMax, pending.Item != null ? pending.Item.ActiveDie.MaxFace() : 6);

            var rng = new System.Random(Environment.TickCount);
            _previewFaces = new int[_spinTickCount + 1];
            int previous = 0;
            for (int i = 1; i <= _spinTickCount; i++)
            {
                previous = DiceAnimChoreographer.NextPreviewFace(rng, faceMax, previous);
                _previewFaces[i] = previous;
            }
        }

        /// <summary>
        /// Pinta un cuadro del segmento pendiente: giro y asentado de la cara cruda, que
        /// queda sostenida mientras el jugador decide si acepta o re-tira.
        /// </summary>
        private void ApplyPendingFrame(float elapsed)
        {
            var phase = ActiveItemRollFeelMath.PendingPhaseAt(elapsed);
            var dieType = _pending.Item != null ? _pending.Item.ActiveDie : DiceType.D6;

            int face;
            bool showNumber = true;
            var role = Rollgeon.Dice.DiceShapeRole.Front;

            if (phase == ActiveItemRollPhase.Spinning)
            {
                // El giro lo coreografia DiceAnimChoreographer, el mismo que los dados de
                // combate: la silueta alterna frontal/laterales y el numero cicla caras
                // preview, desacelerando hacia el reveal.
                int tick = ActiveItemRollFeelMath.SpinTickAt(elapsed, _spinTickCount);
                if (tick != _lastSpinTick)
                {
                    _lastSpinTick = tick;
                    SpinTicked?.Invoke();
                }
                role = DiceAnimChoreographer.SpinRole(tick, _sideSeed);
                face = tick >= 1 && _previewFaces != null && tick < _previewFaces.Length
                    ? _previewFaces[tick]
                    : _pending.RawRoll;
                // Con el tuning shippeado el dado gira "en blanco" y el numero se revela al
                // asentarse. Mostrarlo durante el giro convierte la animacion en un contador
                // de numeros y tapa la rotacion de la silueta, que es lo que se tiene que leer.
                showNumber = _showPreviewFaces;
            }
            else
            {
                face = _pending.RawRoll;
                if (_lastShownPhase != ActiveItemRollPhase.Settled) RawFaceSettled?.Invoke(_pending);
            }
            _lastShownPhase = phase;

            if (_dieIcon != null)
            {
                var sprite = SpriteFor(dieType, role);
                if (sprite != null) _dieIcon.sprite = sprite;
            }

            if (_rollLabel != null)
            {
                _rollLabel.gameObject.SetActive(showNumber);
                if (showNumber)
                {
                    _rollLabel.text = face.ToString();
                    // Girando va neutro para no anticipar. Asentada, se pinta la banda de
                    // la cara cruda: es la informacion con la que el jugador decide si
                    // re-tira. La banda final (post encantamiento) recien se ve al aceptar.
                    _rollLabel.color = phase == ActiveItemRollPhase.Spinning
                        ? Color.white
                        : ColorFor(ActiveItemBands.Resolve(_pending.RawRoll, _pending.Item));
                }
            }

            if (_chip != null)
                _chip.transform.localScale = _chipRestScale * ActiveItemRollFeelMath.PendingScaleAt(elapsed);
        }

        /// <summary>
        /// Pinta un cuadro del segmento de resolucion: el destello del encantamiento si lo
        /// hubo y el hold del resultado final. Arranca con la cruda ya asentada.
        /// </summary>
        private void ApplyResolveFrame(float elapsed)
        {
            if (_dieIcon != null)
            {
                var dieType = _lastResult.Item != null ? _lastResult.Item.ActiveDie : DiceType.D6;
                var sprite = SpriteFor(dieType);
                if (sprite != null) _dieIcon.sprite = sprite;
            }

            if (_rollLabel != null)
            {
                _rollLabel.gameObject.SetActive(true);
                // La cara final desde el primer cuadro: el salto cruda → ajustada ES el
                // destello del encantamiento (la cruda ya se vio todo el segmento
                // pendiente, no hace falta repetirla).
                _rollLabel.text = _lastResult.Roll.ToString();
                _rollLabel.color = ColorFor(_lastResult.Band);
            }

            if (_chip != null)
            {
                float scale = ActiveItemRollFeelMath.ResolveScaleAt(
                    elapsed, _lastResult.WasEnchanted, _lastResult.Band);
                _chip.transform.localScale = _chipRestScale * scale;
            }
        }

        private void EndRollAnimation()
        {
            _pendingStartedAt = -1f;
            _resolveStartedAt = -1f;
            _lastShownPhase = ActiveItemRollPhase.Idle;
            _lastSpinTick = 0;
            if (_rollLabel != null) _rollLabel.gameObject.SetActive(false);
            if (_chip != null) _chip.transform.localScale = _chipRestScale;
            Refresh();
        }

        /// <summary>
        /// Color por banda, para este chip y su juice. Sale de la banda y no del numero:
        /// en Riesgo la negativa es un buen resultado y en Precision el maximo del dado
        /// puede ser el peor.
        /// </summary>
        public Color BandColor(ActiveItemBand band) => ColorFor(band);

        private Color ColorFor(ActiveItemBand band)
        {
            switch (band)
            {
                case ActiveItemBand.Negative: return _negativeColor;
                case ActiveItemBand.Mixed: return _mixedColor;
                default: return _positiveColor;
            }
        }

        private void HandleRefresh(params object[] args) => Refresh();
        private void HandleRefreshNoArgs() => Refresh();
        private void HandleEquippedChanged(ItemSO equipped, ItemSO discarded) => Refresh();

        // ==================================================================
        // Render
        // ==================================================================

        /// <summary>
        /// Repinta la ficha con el item equipado y el gate actual. Idempotente.
        /// </summary>
        public void Refresh()
        {
            ResolveServices();

            // Sin servicio resuelto todavia (el HUD despierta antes que la run) la ficha
            // se esconde: mostrarla vacia en exploracion seria justo lo que el GDD
            // prohibe.
            _block = _activation != null ? _activation.CanActivate() : ActiveItemBlock.NotInCombat;

            // Fuera de combate el slot no existe — se apaga entero, no se atenua.
            bool visible = _block != ActiveItemBlock.NotInCombat;
            if (_chip != null) _chip.enabled = visible;
            if (_dieIcon != null) _dieIcon.enabled = visible && HasItem;
            if (_button != null) _button.interactable = visible;
            if (!visible)
            {
                if (_rollLabel != null) _rollLabel.gameObject.SetActive(false);
                return;
            }

            // Armada = esperando que el jugador elija target. Todavia no costo nada y
            // re-clickear cancela, asi que el estado tiene que leerse distinto del reposo.
            // La ventana de decision (aceptar / re-tirar) usa el mismo tinte: tambien es
            // un estado intermedio, distinto del reposo y del bloqueado.
            bool arming = _activation != null
                && (_activation.IsSelecting || _activation.IsAwaitingDecision);
            if (_chip != null)
                _chip.color = arming ? _placeholderTint * _armedTintFactor : _placeholderTint;

            ApplyDieSprite();
            ApplyUnavailableTint();
        }

        private bool HasItem => _equipped != null && _equipped.HasItem;

        private void ApplyDieSprite()
        {
            if (_dieIcon == null) return;

            if (!HasItem)
            {
                // PRE-02: el slot vacio se muestra sin dado ni tabla de resultados.
                _dieIcon.enabled = false;
                return;
            }

            var sprite = SpriteFor(_equipped.Current.ActiveDie);
            _dieIcon.sprite = sprite;
            _dieIcon.enabled = sprite != null;
        }

        /// <summary>
        /// Silueta del dado desde el catalogo compartido. No se guarda un array propio en
        /// el prefab: el mapeo DiceType -> sprite ya vive en DiceShapeCatalogSO y
        /// duplicarlo lo dejaria desincronizado cuando el arte cambie.
        /// </summary>
        private Sprite SpriteFor(DiceType die,
            Rollgeon.Dice.DiceShapeRole role = Rollgeon.Dice.DiceShapeRole.Front)
        {
            if (_resolvedCatalog == null)
                _resolvedCatalog = Rollgeon.Dice.DiceShapeCatalogSO.Resolve(_shapeCatalog);
            return _resolvedCatalog != null ? _resolvedCatalog.GetShape(die, role) : null;
        }

        private Rollgeon.Dice.DiceShapeCatalogSO _resolvedCatalog;

        /// <summary>
        /// Rojo de "no lo podes usar ahora", el mismo que usan los chips de accion.
        /// Se aplica sobre el icono del dado para no pisar el color de la ficha.
        /// </summary>
        private void ApplyUnavailableTint()
        {
            if (_dieIcon == null) return;
            // AwaitingDecision no es "no usable": la ficha sigue viva (click = re-tirar)
            // y el Confirm del HUD acepta. Pintarla de rojo diria lo contrario.
            bool usable = _block == ActiveItemBlock.None
                          || _block == ActiveItemBlock.AwaitingDecision;
            if (usable) UnavailableTint.Remove(_dieIcon);
            else UnavailableTint.Apply(_dieIcon);
        }

        // ==================================================================
        // Click
        // ==================================================================

        private void HandleClick()
        {
            if (_activation == null)
            {
                Debug.LogWarning(LogPrefix + "IActiveItemActivationService no registrado.");
                return;
            }

            // Ventana de decision abierta: el click de la ficha re-tira, el mismo gesto
            // que el boton Reroll de los dados de combate. Aceptar vive en el boton
            // Confirm del HUD, como en ataque/defensa.
            if (_activation.IsAwaitingDecision)
            {
                if (!_activation.CanRequestReroll)
                {
                    ShowReject(LocalizedContent.Ui(UiTextKeys.RejectNoRolls, "Rolls insuficientes."));
                    return;
                }

                _activation.RequestReroll();
                Refresh();
                return;
            }

            var block = _activation.CanActivate();
            if (block != ActiveItemBlock.None)
            {
                ShowReject(DescribeBlock(block));
                return;
            }

            // Gratis: si el item pide target abre la seleccion y espera; si activa
            // directo, resuelve en el acto. El cobro nunca ocurre aca.
            _activation.BeginActivation();
            Refresh();
        }

        private void HandleRollPending(ActiveItemPendingRoll pending)
        {
            // El GDD: el dado emerge del item equipado. La tirada queda pendiente: el
            // giro asienta la cara cruda y la sostiene hasta que el jugador decida.
            _pending = pending;
            _pendingStartedAt = Time.unscaledTime;
            _resolveStartedAt = -1f;
            _lastShownPhase = ActiveItemRollPhase.Idle;
            _lastSpinTick = 0;
            BuildSpinPlan(pending);

            // Una tirada nueva (o un reroll) cancela la anterior: se vuelve al reposo
            // antes de animar. El reposo es el capturado en Awake, nunca el tamaño que
            // dejo el pop previo.
            if (_chip != null) _chip.transform.localScale = _chipRestScale;

            RollSpinStarted?.Invoke(pending);
            ApplyPendingFrame(0f);
            Refresh();
        }

        private void HandleResolved(ActiveItemActivationResult result)
        {
            // El jugador acepto: la resolucion corre sobre la cara ya asentada y el slot
            // vuelve al reposo apenas termina de mostrar el resultado.
            _lastResult = result;
            _resolveStartedAt = Time.unscaledTime;
            _pendingStartedAt = -1f;

            if (_chip != null) _chip.transform.localScale = _chipRestScale;

            ResultLanded?.Invoke(result);
            ApplyResolveFrame(0f);
            Refresh();
        }

        /// <summary>Motivo localizado del bloqueo (§7).</summary>
        private static string DescribeBlock(ActiveItemBlock block)
        {
            switch (block)
            {
                case ActiveItemBlock.NotEnoughRolls:
                    return LocalizedContent.Ui(UiTextKeys.RejectNoRolls, "Rolls insuficientes.");
                case ActiveItemBlock.NoValidTarget:
                    return LocalizedContent.Ui(UiTextKeys.RejectNoValidTarget, "Sin objetivo válido.");
                case ActiveItemBlock.NoItemEquipped:
                    return LocalizedContent.Ui(UiTextKeys.RejectNoActiveItem, "Sin ítem equipado.");
                case ActiveItemBlock.NotYourTurn:
                    return LocalizedContent.Ui(UiTextKeys.RejectNotYourTurn, "No es tu turno.");
                default:
                    return LocalizedContent.Ui(UiTextKeys.RejectItemUnavailable,
                                               "No podés usar este objeto ahora.");
            }
        }

        private void ShowReject(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return;

            string title = LocalizedContent.Ui(UiTextKeys.RejectTitle,
                "Esta acción no puede ser realizada");
            ActionRejectToast.Show(transform as RectTransform,
                title + "\n" + reason,
                _rollLabel != null ? _rollLabel.font : null);
        }

        /// <summary>
        /// Nombre del item y su tabla de bandas. El GDD pide mostrar el reparto del dado
        /// <b>antes</b> de activar, para que el jugador sepa a que se expone.
        /// </summary>
        private string BuildTooltip()
        {
            if (!HasItem)
                return LocalizedContent.Ui(UiTextKeys.RejectNoActiveItem, "Sin ítem equipado.");

            var item = _equipped.Current;
            int faces = item.ActiveDie.MaxFace();

            string name = LocalizedContent.Name(item.ItemId,
                string.IsNullOrEmpty(item.DisplayName) ? item.ItemId : item.DisplayName);

            // Se listan las caras y no un rango: en Precision y Control las bandas no son
            // contiguas (Control con paridad par sobre D6 da mixta en 2 y en 5), y en
            // Binary/Gradient/Hierarchy directamente no hay 3 bandas fijas — DescribeStructure
            // devuelve las filas reales del item (2 en Binary, 1 en Gradient/Hierarchy).
            var text = new System.Text.StringBuilder();
            text.AppendLine($"<b>{name}</b>  ·  d{faces}");
            var rows = ActiveItemBands.DescribeStructure(item);
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) text.AppendLine();
                text.Append($"{rows[i].Faces} {rows[i].Label}");
            }

            var ench = _equipped.Enchantment;
            if (ench != null)
            {
                // La tabla de arriba es la del dado crudo. El encantamiento corre el
                // resultado antes de la banda, asi que el jugador tiene que verlo junto a
                // ella para entender a que se expone de verdad.
                text.AppendLine();
                string uses = ench.IsLimited ? $"  [{_equipped.EnchantmentUsesLeft} usos]" : string.Empty;
                text.Append($"<i>{ench.DisplayName}: {ench.DescribeEffect()}</i>{uses}");
            }

            if (_activation != null && _activation.IsAwaitingDecision)
            {
                text.AppendLine();
                text.Append("<i>" + LocalizedContent.Ui(UiTextKeys.ActiveItemDecideHint,
                    "Click: re-tirar (-1 Roll) · Confirmar: aceptar") + "</i>");
            }

            return text.ToString();
        }
    }
}
