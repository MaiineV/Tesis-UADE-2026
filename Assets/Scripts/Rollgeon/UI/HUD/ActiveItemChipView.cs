using System;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Items;
using Rollgeon.Items.Active;
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
        [InfoBox("Un sprite por DiceType, en el orden del enum: D4, D6, D8, D10, D12, " +
                 "D20, D3. Si falta el del dado del item, el icono se apaga.")]
        [SerializeField]
        private Sprite[] _dieSprites = new Sprite[7];

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
        // Animacion de tirada en curso. _rollStartedAt < 0 = en reposo.
        private float _rollStartedAt = -1f;
        private ActiveItemActivationResult _lastResult;
        private int _spinSeed;
        private Vector3 _chipRestScale = Vector3.one;

        // ==================================================================
        // Lifecycle
        // ==================================================================

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
            if (_activation != null) _activation.OnResolved -= HandleResolved;

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
            if (_rollStartedAt < 0f) return;

            float elapsed = Time.unscaledTime - _rollStartedAt;
            if (elapsed >= ActiveItemRollFeelMath.TotalSeconds(_lastResult.WasEnchanted))
            {
                EndRollAnimation();
                return;
            }

            ApplyRollFrame(elapsed);
        }

        /// <summary>Pinta un cuadro de la animacion de tirada.</summary>
        private void ApplyRollFrame(float elapsed)
        {
            bool enchanted = _lastResult.WasEnchanted;
            int faces = _lastResult.Item != null ? _lastResult.Item.ActiveDie.MaxFace() : 6;

            int face = ActiveItemRollFeelMath.FaceAt(
                elapsed, enchanted, _lastResult.RawRoll, _lastResult.Roll, faces, _spinSeed);

            var phase = ActiveItemRollFeelMath.PhaseAt(elapsed, enchanted);

            if (_rollLabel != null)
            {
                _rollLabel.gameObject.SetActive(true);
                _rollLabel.text = face.ToString();

                _rollLabel.color = ColorForPhase(phase, face);
            }

            if (_chip != null)
            {
                float scale = ActiveItemRollFeelMath.ScaleAt(elapsed, enchanted, _lastResult.Band);
                _chip.transform.localScale = _chipRestScale * scale;
            }
        }

        private void EndRollAnimation()
        {
            _rollStartedAt = -1f;
            if (_rollLabel != null) _rollLabel.gameObject.SetActive(false);
            if (_chip != null) _chip.transform.localScale = _chipRestScale;
            Refresh();
        }

        /// <summary>
        /// Color del numero en un cuadro dado.
        /// </summary>
        /// <remarks>
        /// Mientras gira el numero es adorno y va neutro, para no anticipar la banda antes
        /// de que el dado frene.
        /// <para>
        /// En la pausa sobre la cara cruda se pinta la banda <b>de esa cara</b>, no la
        /// final: si el encantamiento va a subir un 2 a un 3, el jugador tiene que ver
        /// primero el rojo del 2 y despues el salto al amarillo. Pintar el color final
        /// desde el principio spoilea la intervencion del encantamiento, que es
        /// justamente lo que el GDD pide comunicar.
        /// </para>
        /// </remarks>
        private Color ColorForPhase(ActiveItemRollPhase phase, int shownFace)
        {
            if (phase == ActiveItemRollPhase.Spinning) return Color.white;

            if (phase == ActiveItemRollPhase.Settled && _lastResult.WasEnchanted)
                return ColorFor(ActiveItemBands.Resolve(shownFace, _lastResult.Item));

            return ColorFor(_lastResult.Band);
        }

        /// <summary>
        /// Color por banda. Sale de la banda y no del numero: en Riesgo la negativa es un
        /// buen resultado y en Precision el maximo del dado puede ser el peor.
        /// </summary>
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
            bool arming = _activation != null && _activation.IsSelecting;
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

        private Sprite SpriteFor(DiceType die)
        {
            int index = (int)die;
            if (_dieSprites == null || index < 0 || index >= _dieSprites.Length) return null;
            return _dieSprites[index];
        }

        /// <summary>
        /// Rojo de "no lo podes usar ahora", el mismo que usan los chips de accion.
        /// Se aplica sobre el icono del dado para no pisar el color de la ficha.
        /// </summary>
        private void ApplyUnavailableTint()
        {
            if (_dieIcon == null) return;
            if (_block == ActiveItemBlock.None) UnavailableTint.Remove(_dieIcon);
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

        private void HandleResolved(ActiveItemActivationResult result)
        {
            // El GDD: el dado emerge del item equipado, y el slot vuelve al reposo apenas
            // termina de mostrar el resultado.
            _lastResult = result;
            _rollStartedAt = Time.unscaledTime;
            _spinSeed = Environment.TickCount;

            if (_chip != null) _chipRestScale = _chip.transform.localScale;

            ApplyRollFrame(0f);
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
            // contiguas (Control con paridad par sobre D6 da mixta en 2 y en 5).
            var text = new System.Text.StringBuilder();
            text.AppendLine($"<b>{name}</b>  ·  d{faces}");
            text.AppendLine($"{ActiveItemBands.DescribeFaces(ActiveItemBand.Negative, item)} riesgo");
            text.AppendLine($"{ActiveItemBands.DescribeFaces(ActiveItemBand.Mixed, item)} mixto");
            text.Append($"{ActiveItemBands.DescribeFaces(ActiveItemBand.Positive, item)} fuerte");

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

            return text.ToString();
        }
    }
}
