using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Set de sprites de un botón del HUD: normal + hover y opcionalmente disabled.
    /// Los sheets del artista vienen con la variante hover como <c>_0</c> y la normal
    /// como <c>_1</c> (Roll2, CircleButton3, Confirm2; FinishTurnButton suma _2).
    /// </summary>
    [Serializable]
    public struct ButtonSpriteSet
    {
        [SerializeField]
        [Tooltip("Sprite en reposo.")]
        private Sprite _normal;

        [SerializeField]
        [Tooltip("Sprite con el puntero encima. Null = queda el normal.")]
        private Sprite _hover;

        [SerializeField]
        [Tooltip("Sprite con el botón no interactuable. Null = normal + tint del Button.")]
        private Sprite _disabled;

        public ButtonSpriteSet(Sprite normal, Sprite hover, Sprite disabled = null)
        {
            _normal = normal;
            _hover = hover;
            _disabled = disabled;
        }

        public Sprite Normal => _normal;
        public Sprite Hover => _hover;
        public Sprite Disabled => _disabled;

        /// <summary>Un set sin normal es un set no configurado — se ignora entero.</summary>
        public bool HasSprites => _normal != null;
    }

    /// <summary>
    /// Pinta la Image de un botón según puntero e interactable, con el
    /// <see cref="ButtonSpriteSet"/> intercambiable en runtime — la pieza que permite
    /// que un mismo botón cambie de arte según contexto (reroll gratis vs pago,
    /// End Turn vs Confirm) además de por estado.
    /// </summary>
    /// <remarks>
    /// El hover va por sprite (no por el SpriteSwap del <see cref="Button"/>) para poder
    /// conservar la transición ColorTint, que es la única que atenúa el disabled cuando
    /// el set no trae arte propio de disabled.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Hud Button Sprite Swap")]
    public class HudButtonSpriteSwap : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        [Tooltip("Botón dueño del estado interactable. Auto-resolve en el mismo GO si null.")]
        private Button _button;

        [SerializeField]
        [Tooltip("Image a repintar. Auto-resolve al target graphic del botón si null.")]
        private Image _image;

        [SerializeField]
        [Tooltip("Set aplicado en Awake, antes de que la vista dueña empuje contexto.")]
        private ButtonSpriteSet _initialSet;

        private ButtonSpriteSet _set;
        private bool _hovered;
        private bool _lastInteractable = true;

        private void Awake()
        {
            if (!_set.HasSprites && _initialSet.HasSprites) Apply(_initialSet);
        }

        private void OnDisable() => _hovered = false;

        private void Update()
        {
            // Selectable no avisa cuando le cambian interactable (y acá lo fuerzan
            // varios gates externos, ej. ActionRollExplorationVisibility) — poll de
            // un bool por frame para que el sprite de disabled entre y salga solo.
            if (IsInteractable() != _lastInteractable) Repaint();
        }

        /// <summary>Cambia el set vigente y repinta. Sets sin normal se ignoran.</summary>
        public void Apply(ButtonSpriteSet set)
        {
            if (!set.HasSprites) return;
            _set = set;
            Repaint();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Repaint();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Repaint();
        }

        /// <summary>Re-evalúa estado y pisa el sprite de la Image. Idempotente.</summary>
        public void Repaint()
        {
            if (!_set.HasSprites) return;
            ResolveRefs();
            if (_image == null) return;

            bool interactable = IsInteractable();
            _lastInteractable = interactable;
            _image.sprite = !interactable && _set.Disabled != null ? _set.Disabled
                : _hovered && interactable && _set.Hover != null ? _set.Hover
                : _set.Normal;
        }

        private bool IsInteractable() => _button == null || _button.IsInteractable();

        private void ResolveRefs()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_image == null && _button != null) _image = _button.targetGraphic as Image;
            if (_image == null) _image = GetComponent<Image>();
        }
    }
}
