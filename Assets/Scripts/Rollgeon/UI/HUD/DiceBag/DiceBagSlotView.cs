using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.DiceBag
{
    /// <summary>
    /// Un cupo de encantamiento del dado seleccionado: el círculo, y la runa encima cuando
    /// el cupo está ocupado.
    /// </summary>
    /// <remarks>
    /// El círculo cambia de sprite (vacío / ocupado) EN VEZ de solo prender la runa: así el
    /// cupo ocupado se distingue aunque el jugador no reconozca todavía qué significa la
    /// runa, que es siempre la misma para cualquier encantamiento.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Bag Slot View")]
    public class DiceBagSlotView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private Image _circle;
        [SerializeField, Required] private Image _rune;
        [SerializeField, Required] private Button _button;

        [Title("Círculos")]
        [SerializeField, Tooltip("Cupo libre (EnchanmentSlot_0).")]
        private Sprite _emptyCircle;

        [SerializeField, Tooltip("Cupo con encantamiento (EnchanmentSlot_1).")]
        private Sprite _filledCircle;

        private Action _onClick;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
            _onClick = null;
        }

        private void HandleClick() => _onClick?.Invoke();

        public void Bind(bool filled, Action onClick)
        {
            _onClick = onClick;

            if (_circle != null)
            {
                var sprite = filled ? _filledCircle : _emptyCircle;
                if (sprite != null) _circle.sprite = sprite;
            }

            if (_rune != null) _rune.enabled = filled && _rune.sprite != null;
        }

        /// <summary>Realce del cupo elegido — el detalle de abajo habla de este.</summary>
        public void SetSelected(bool selected)
        {
            if (_circle == null) return;
            var color = _circle.color;
            color.a = selected ? 1f : 0.75f;
            _circle.color = color;
        }
    }
}
