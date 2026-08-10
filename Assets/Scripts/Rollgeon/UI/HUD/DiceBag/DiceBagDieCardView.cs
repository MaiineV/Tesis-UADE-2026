using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.DiceBag
{
    /// <summary>
    /// Un dado de la bolsa: la casilla, el ícono del dado con su número de caras (igual que
    /// en la mesa de encantamientos) y, POR FUERA del cuadrado, cuántos cupos de
    /// encantamiento tiene usados sobre el total.
    /// </summary>
    /// <remarks>
    /// El contador va afuera del marco a pedido de diseño: adentro competía con el número
    /// de caras y la casilla quedaba cargada.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Bag Die Card View")]
    public class DiceBagDieCardView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private Image _frame;
        [SerializeField, Required] private Image _diceIcon;
        [SerializeField, Required] private TextMeshProUGUI _faceCountLabel;
        [SerializeField, Required] private TextMeshProUGUI _slotsLabel;
        [SerializeField, Required] private Button _button;

        [Title("Marcos")]
        [SerializeField, Tooltip("Casilla en reposo (UI-sheet_7).")]
        private Sprite _idleFrame;

        [SerializeField, Tooltip("Casilla del dado seleccionado (UI-sheet_5).")]
        private Sprite _selectedFrame;

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

        public void Bind(Sprite diceSprite, int maxFace, int usedSlots, int totalSlots,
                         string slotsSuffix, Action onClick)
        {
            _onClick = onClick;

            if (_diceIcon != null)
            {
                _diceIcon.sprite = diceSprite;
                _diceIcon.enabled = diceSprite != null;
            }

            if (_faceCountLabel != null) _faceCountLabel.text = maxFace > 0 ? maxFace.ToString() : string.Empty;

            // "2/3 cupos" — el sufijo viene ya localizado del panel, que es quien sabe de
            // la tabla; la card solo dibuja.
            if (_slotsLabel != null)
                _slotsLabel.text = totalSlots > 0
                    ? $"{usedSlots}/{totalSlots} {slotsSuffix}".TrimEnd()
                    : string.Empty;
        }

        public void SetSelected(bool selected)
        {
            if (_frame == null) return;
            var sprite = selected ? _selectedFrame : _idleFrame;
            if (sprite != null) _frame.sprite = sprite;
        }
    }
}
