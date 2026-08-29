using System;
using Rollgeon.Upgrades.Dice;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.DiceBag
{
    /// <summary>
    /// Un dado de la bolsa: el sprite del dado con su número de caras encima, sin
    /// marco ni fondo (mock "new dice bag drawer"). El seleccionado se marca con
    /// alpha pleno + escala; el resto queda atenuado.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Bag Die Card View")]
    public class DiceBagDieCardView : MonoBehaviour
    {
        private const float SelectedScale = 1.1f;
        private const float DimmedAlpha = 0.7f;

        [Title("Refs")]
        [SerializeField, Required] private Image _diceIcon;
        [SerializeField, Required] private TMPro.TextMeshProUGUI _faceCountLabel;
        [SerializeField, Required] private Button _button;

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

        public void Bind(Sprite diceSprite, int maxFace, Action onClick)
        {
            _onClick = onClick;

            if (_diceIcon != null)
            {
                _diceIcon.sprite = diceSprite;
                _diceIcon.enabled = diceSprite != null;
            }

            if (_faceCountLabel != null)
                _faceCountLabel.text = maxFace > 0 ? maxFace.ToString() : string.Empty;
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

        /// <summary>Sin marco, la selección se lee por presencia: alpha pleno y un toque de escala.</summary>
        public void SetSelected(bool selected)
        {
            if (_diceIcon != null)
            {
                var c = _diceIcon.color;
                c.a = selected ? 1f : DimmedAlpha;
                _diceIcon.color = c;
            }

            transform.localScale = Vector3.one * (selected ? SelectedScale : 1f);
        }
    }
}
