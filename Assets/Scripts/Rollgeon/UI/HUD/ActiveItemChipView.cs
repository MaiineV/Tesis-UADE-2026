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
        [SerializeField, MinValue(0f)]
        [Tooltip("Segundos que el numero obtenido queda visible antes de volver al reposo.")]
        private float _rollDisplaySeconds = 1.2f;

        [ShowInInspector, ReadOnly]
        private ActiveItemBlock _block = ActiveItemBlock.NoItemEquipped;

        private IEquippedActiveItemService _equipped;
        private IActiveItemActivationService _activation;
        private bool _bound;
        private float _hideRollAt;

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
            // El numero obtenido vuelve al reposo solo. Update en vez de coroutine para
            // que un OnDisable a mitad no deje la ficha pegada mostrando la cara vieja.
            if (_hideRollAt > 0f && Time.unscaledTime >= _hideRollAt)
            {
                _hideRollAt = 0f;
                if (_rollLabel != null) _rollLabel.gameObject.SetActive(false);
            }
        }

        private void HandleRefresh(params object[] args) => Refresh();
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

            if (_chip != null) _chip.color = _placeholderTint;
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

            // Fase 1: solo los items que activan directo, sin paso de seleccion. Los que
            // piden target abren el selector — eso entra en la fase siguiente.
            _activation.Confirm(selection: null);
        }

        private void HandleResolved(ActiveItemActivationResult result)
        {
            // El GDD: "el dado dentro del slot refleja el resultado obtenido brevemente
            // antes de volver a su estado de reposo".
            if (_rollLabel != null)
            {
                _rollLabel.gameObject.SetActive(true);
                _rollLabel.text = result.Roll.ToString();
            }
            _hideRollAt = Time.unscaledTime + _rollDisplaySeconds;

            Refresh();
        }

        // ==================================================================
        // Texto
        // ==================================================================

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
            var neg = ActiveItemBands.RangeOf(ActiveItemBand.Negative, faces);
            var mix = ActiveItemBands.RangeOf(ActiveItemBand.Mixed, faces);
            var pos = ActiveItemBands.RangeOf(ActiveItemBand.Positive, faces);

            string name = LocalizedContent.Name(item.ItemId,
                string.IsNullOrEmpty(item.DisplayName) ? item.ItemId : item.DisplayName);

            return $"<b>{name}</b>  ·  d{faces}\n" +
                   $"{Range(neg)} riesgo\n" +
                   $"{Range(mix)} mixto\n" +
                   $"{Range(pos)} fuerte";
        }

        private static string Range((int Min, int Max) r)
            => r.Min == r.Max ? r.Min.ToString() : $"{r.Min}-{r.Max}";
    }
}
