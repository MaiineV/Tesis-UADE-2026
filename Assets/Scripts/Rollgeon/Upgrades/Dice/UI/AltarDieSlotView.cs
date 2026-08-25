using System;
using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Upgrades.Dice.UI
{
    /// <summary>
    /// Un dado apoyado en la repisa de la máquina: ícono del dado con su número
    /// de caras encima y la sombra (<c>SlotMachineShadow</c>) debajo. Cuando la
    /// oferta está lista y hay un encantamiento elegido, los dados VÁLIDOS
    /// muestran el outline dorado de "clickeable"; al elegir uno sube un poco y
    /// su contorno cambia al outline arcano de seleccionado — SOLO el borde,
    /// nunca un shader sobre el dado completo (feedback de playtest).
    /// </summary>
    [AddComponentMenu("Rollgeon/Upgrades/Dice/UI/Altar Die Slot View")]
    public sealed class AltarDieSlotView : MonoBehaviour
    {
        [Title("Widget refs")]
        [Required, SerializeField] private Button _button;
        [Required, SerializeField] private Image _icon;
        [SerializeField, Optional] private TextMeshProUGUI _numberLabel;
        [SerializeField, Optional] private Image _shadow;

        [Title("Settings")]
        [SerializeField, Optional] private EnchantmentAltarUiSettingsSO _settings;

        private int _index;
        private Action<int> _onClick;
        private Outline _outline;
        private UITooltipTrigger _tooltipTrigger;
        private RectTransform _iconRect;
        private Vector2 _iconRestPos;
        private bool _restCaptured;
        private bool _selected;

        private void Awake()
        {
            _iconRect = _icon != null ? _icon.rectTransform : null;
            CaptureRestPose();
            if (_button != null) _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClicked);
        }

        private void OnDisable()
        {
            if (_iconRect != null)
            {
                Tween.StopAll(onTarget: _iconRect);
                if (_restCaptured) _iconRect.anchoredPosition = _iconRestPos;
            }
            _selected = false;
            SetOutline(false);
        }

        public void Configure(int index, Action<int> onClick)
        {
            _index = index;
            _onClick = onClick;
        }

        /// <summary>Vincula el dado del bag a esta posición de la repisa.</summary>
        public void Bind(Sprite diceSprite, string facesNumber, Func<string> tooltipProvider)
        {
            if (_icon != null)
            {
                _icon.sprite = diceSprite;
                _icon.enabled = diceSprite != null;
                _icon.preserveAspect = true;
            }
            if (_numberLabel != null) _numberLabel.text = facesNumber ?? string.Empty;
            ConfigureTooltip(tooltipProvider);
            SetSelectable(false);
            SetSelected(false, animate: false);
        }

        /// <summary>Oculta la posición (bags con menos dados que la repisa).</summary>
        public void SetOccupied(bool occupied)
        {
            if (_icon != null) _icon.enabled = occupied && _icon.sprite != null;
            if (_numberLabel != null) _numberLabel.gameObject.SetActive(occupied);
            if (_shadow != null) _shadow.enabled = occupied;
            if (_button != null) _button.interactable = false;
        }

        /// <summary>
        /// Outline de "podés elegir este" — se prende en los dados válidos para
        /// el encantamiento seleccionado, cuando los reels ya frenaron.
        /// </summary>
        public void SetSelectable(bool selectable)
        {
            if (_button != null) _button.interactable = selectable;
            if (!_selected) SetOutline(selectable);
        }

        /// <summary>
        /// El dado elegido sube y su contorno pasa al outline arcano de
        /// seleccionado (solo el borde — nada de shaders sobre el dado);
        /// deseleccionar lo devuelve a la repisa.
        /// </summary>
        public void SetSelected(bool selected, bool animate = true)
        {
            if (_selected == selected) return;
            _selected = selected;
            SetOutline(selected || (_button != null && _button.interactable));

            if (_iconRect == null) return;
            CaptureRestPose();
            Tween.StopAll(onTarget: _iconRect);
            var target = selected && _settings != null
                ? _iconRestPos + new Vector2(0f, _settings.DieSelectRise)
                : _iconRestPos;

            if (animate && CanJuice())
            {
                Tween.UIAnchoredPosition(_iconRect, target, _settings.DieSelectRiseDuration,
                    _settings.DieSelectRiseEase, useUnscaledTime: true);
            }
            else
            {
                _iconRect.anchoredPosition = target;
            }
        }

        private void SetOutline(bool on)
        {
            if (_icon == null) return;
            if (_outline == null)
            {
                if (!on) return;
                _outline = _icon.GetComponent<Outline>();
                if (_outline == null) _outline = _icon.gameObject.AddComponent<Outline>();
            }
            if (on && _settings != null)
            {
                // Seleccionado = tono arcano; solo-clickeable = dorado.
                _outline.effectColor = _selected
                    ? _settings.DieSelectedOutlineColor
                    : _settings.ClickableOutlineColor;
                _outline.effectDistance = _selected
                    ? _settings.DieSelectedOutlineDistance
                    : _settings.ClickableOutlineDistance;
            }
            _outline.enabled = on;
        }

        private void ConfigureTooltip(Func<string> tooltipProvider)
        {
            if (_tooltipTrigger == null) _tooltipTrigger = GetComponent<UITooltipTrigger>();
            if (tooltipProvider != null)
            {
                if (_tooltipTrigger == null) _tooltipTrigger = gameObject.AddComponent<UITooltipTrigger>();
                _tooltipTrigger.TextProvider = tooltipProvider;
                return;
            }
            // Nunca dejar TextProvider null — caería en TooltipResolver.AutoResolve
            // (mismo gotcha que EnchantmentItemButtonView).
            if (_tooltipTrigger != null) _tooltipTrigger.TextProvider = () => string.Empty;
        }

        private void CaptureRestPose()
        {
            if (_restCaptured || _iconRect == null) return;
            _iconRestPos = _iconRect.anchoredPosition;
            _restCaptured = true;
        }

        private bool CanJuice()
        {
            return _settings != null && Application.isPlaying && !DiceUiMotionPrefs.ReducedMotion;
        }

        private void HandleClicked() => _onClick?.Invoke(_index);
    }
}
