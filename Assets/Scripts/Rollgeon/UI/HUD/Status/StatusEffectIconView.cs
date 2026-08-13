using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Un ícono de estado del player: marco de fondo + ícono del efecto, con arte de marco
    /// distinta según el estado esté activo o latente. Es el prefab que
    /// <see cref="PlayerStatusIconsView"/> instancia una vez por estado.
    /// </summary>
    /// <remarks>
    /// El marco vive acá y no en el estado porque es del SISTEMA: todos los estados usan el
    /// mismo par (activo/inactivo), así que un estado nuevo solo aporta su ícono. Los dos
    /// sprites los inyecta el installer por SerializedObject.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Status Effect Icon View")]
    public class StatusEffectIconView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private Image _background;
        [SerializeField, Required] private Image _icon;

        [SerializeField]
        [Tooltip("Turnos restantes, bajo el borde del marco. Se apaga en lo que no tiene " +
                 "duración (las pasivas).")]
        private TextMeshProUGUI _durationLabel;

        [SerializeField]
        [Tooltip("Trigger del tooltip de hover. Opcional: sin él el ícono no explica nada.")]
        private UITooltipTrigger _tooltip;

        [Title("Marcos")]
        [SerializeField, Tooltip("Marco cuando el estado está surtiendo efecto (UI-sheet_5).")]
        private Sprite _activeBackground;

        [SerializeField, Tooltip("Marco cuando el estado está latente (UI-sheet_7).")]
        private Sprite _inactiveBackground;

        [ShowInInspector, ReadOnly] private string _stateId;

        // El texto se arma al hacer hover, no al pintar: el estado que muestra este slot
        // cambia varias veces por turno y formatear en cada refresh sería trabajo tirado.
        private StatusIconState _state;

        /// <summary>Id del estado que este slot está mostrando — la fila lo usa para reusarlo.</summary>
        public string StateId => _stateId;

        private void Awake()
        {
            if (_tooltip != null) _tooltip.TextProvider = BuildTooltip;
        }

        private string BuildTooltip() => StatusTooltipText.Build(_state);

        public void Show(in StatusIconState state)
        {
            _stateId = state.Id;
            _state = state;
            name = string.IsNullOrEmpty(state.Id) ? "StatusIcon" : $"StatusIcon_{state.Id}";

            // Awake no corre en un GameObject instanciado inactivo; re-asignar acá es barato
            // y evita un slot mudo cuando la fila crece con la pantalla ya andando.
            if (_tooltip != null) _tooltip.TextProvider = BuildTooltip;

            if (_durationLabel != null)
            {
                string badge = StatusTooltipText.ResolveDurationBadge(state);
                _durationLabel.text = badge;
                _durationLabel.gameObject.SetActive(badge.Length > 0);
            }

            if (_icon != null)
            {
                _icon.sprite = state.Icon;
                // Un estado sin arte todavía no debe dibujar el cuadrado blanco del Image.
                _icon.enabled = state.Icon != null;
            }

            if (_background == null) return;
            var frame = state.Active ? _activeBackground : _inactiveBackground;
            if (frame != null) _background.sprite = frame;
            _background.enabled = _background.sprite != null;
        }
    }
}
