using System;
using Rollgeon.Upgrades.Dice;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.DiceBag
{
    /// <summary>
    /// Un dado de la bolsa: la casilla, el ícono del dado con su número de caras (igual que
    /// en la mesa de encantamientos) y, POR FUERA del cuadrado, cuántos encantamientos
    /// acumuló (stack sin techo — GDD).
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

        [Title("Encantamiento")]
        [SerializeField, Tooltip("Material del dado encantado (EnchantHoloUI) — el mismo que usan " +
                                 "los slots de la zona de dados en combate.")]
        private Material _enchantMaterial;

        [SerializeField, Tooltip("Material del dado maldito (EnchantCurseUI) — el mismo que usan " +
                                 "los slots. Si falta, cae al material holo.")]
        private Material _cursedMaterial;

        private Action _onClick;
        private EnchantmentSO _enchantment;

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

        public void Bind(Sprite diceSprite, int maxFace, int enchantCount,
                         string enchantSuffix, Action onClick)
        {
            _onClick = onClick;

            if (_diceIcon != null)
            {
                _diceIcon.sprite = diceSprite;
                _diceIcon.enabled = diceSprite != null;
            }

            if (_faceCountLabel != null) _faceCountLabel.text = maxFace > 0 ? maxFace.ToString() : string.Empty;

            // "2 encantamientos" — el sufijo viene ya localizado del panel, que es quien
            // sabe de la tabla; la card solo dibuja. Sin encantamientos, sin contador.
            if (_slotsLabel != null)
                _slotsLabel.text = enchantCount > 0
                    ? $"{enchantCount} {enchantSuffix}".TrimEnd()
                    : string.Empty;
        }

        /// <summary>
        /// Muestra el visual de dado encantado sobre el ícono: holo para bendiciones,
        /// maldito (<c>CapCursed</c>) para curses. <c>null</c> = sin encantamiento
        /// (vuelve al material default de uGUI). Mismo contrato que
        /// <c>DiceSlotView.SetEnchantVisual</c>: el material es compartido y la variación por
        /// dado sale de la posición canvas-space dentro del shader.
        /// </summary>
        public void SetEnchantVisual(EnchantmentSO enchantment)
        {
            // Idempotente: sin esto, escribir el material en cada rebuild dispara SetMaterialDirty.
            // Válido con la elección holo/maldito: es función pura de la identidad del SO.
            if (_enchantment == enchantment) return;
            _enchantment = enchantment;

            if (_diceIcon == null) return;
            _diceIcon.material = ResolveMaterial(enchantment);
        }

        private Material ResolveMaterial(EnchantmentSO enchantment)
        {
            if (enchantment == null) return null;
            if (enchantment.IsCursed() && _cursedMaterial != null) return _cursedMaterial;
            return _enchantMaterial;
        }

        public void SetSelected(bool selected)
        {
            if (_frame == null) return;
            var sprite = selected ? _selectedFrame : _idleFrame;
            if (sprite != null) _frame.sprite = sprite;
        }
    }
}
