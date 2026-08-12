using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Look de chip para <see cref="Button"/>s planos (los de exploración, que no
    /// tienen el state machine de <see cref="ActionButton"/>): base en reposo,
    /// sprite de highlight al hover, y el mismo highlight atenuado cuando el botón
    /// está deshabilitado — el look normalizado de "no disponible" que comparte
    /// con los chips de combate.
    /// </summary>
    /// <remarks>
    /// Observa <see cref="Button.interactable"/> por polling en <c>LateUpdate</c>:
    /// <see cref="ExplorationActionButtonsView"/> lo togglea desde varios caminos
    /// (refresh, hotkeys, tutorial) y no emite ningún evento al hacerlo. Cuatro
    /// comparaciones de bool por frame — más barato que enganchar cada caller.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Chip Button Visual")]
    [RequireComponent(typeof(Button))]
    public class ChipButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [InfoBox("Sprite al hover; los estados deshabilitados lo muestran con el alpha " +
                 "de abajo. Null = sin swap.")]
        [SerializeField]
        private Sprite _highlightSprite;

        [SerializeField, Range(0f, 1f), Tooltip("Alpha del chip cuando el botón no es interactable.")]
        private float _disabledAlpha = 0.5f;

        private Button _button;
        private Image _image;
        private Sprite _normalSprite;
        private Color _baseColor = Color.white;
        private bool _hovered;
        private bool _lastInteractable;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _image = _button.targetGraphic as Image;
            if (_image != null)
            {
                _normalSprite = _image.sprite;
                _baseColor = _image.color;
            }
            _lastInteractable = _button.interactable;
            Apply();
        }

        private void OnDisable()
        {
            // uGUI no dispara PointerExit sobre un GO desactivado.
            _hovered = false;
        }

        private void LateUpdate()
        {
            if (_button.interactable == _lastInteractable) return;
            _lastInteractable = _button.interactable;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Apply();
        }

        private void Apply()
        {
            if (_image == null) return;

            bool disabled = !_button.interactable;
            if (_highlightSprite != null)
                _image.sprite = disabled || _hovered ? _highlightSprite : _normalSprite;

            var color = _baseColor;
            if (disabled) color.a *= _disabledAlpha;
            _image.color = color;
        }
    }
}
