using System;
using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.Upgrades.Dice.UI
{
    /// <summary>
    /// La palanca de la slot machine del altar. Dos frames del sheet
    /// <c>slot-machine-sheet</c>: _0 = palanca arriba (reposo), _1 = palanca
    /// abajo. El click NO es un swap seco: el frame de arriba se comprime en
    /// altura hacia la base (el rect tiene pivot abajo) hasta la altura del
    /// frame bajado, ahí swapea, dispara el roll, y la vuelta es más lenta y
    /// suave (ease configurable) como una palanca con resorte. Hover con
    /// palanca habilitada prende un outline dorado ("esto se clickea").
    /// </summary>
    [AddComponentMenu("Rollgeon/Upgrades/Dice/UI/Altar Lever View")]
    public sealed class AltarLeverView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private const float DisabledAlpha = 0.55f;

        [Title("Widget refs")]
        [Required, SerializeField] private Button _button;
        [Required, SerializeField] private Image _leverImage;

        [Title("Sprites (slot-machine-sheet)")]
        [Required, SerializeField] private Sprite _spriteUp;
        [Required, SerializeField] private Sprite _spriteDown;

        [SerializeField, Optional] private EnchantmentAltarUiSettingsSO _settings;

        /// <summary>Se dispara con la palanca abajo — la view paga el roll y arranca el spin.</summary>
        public event Action OnPulled;

        private Outline _outline;
        private RectTransform _leverRect;
        private Vector2 _upSize;
        private bool _pulling;

        private void Awake()
        {
            _leverRect = _leverImage != null ? _leverImage.rectTransform : null;
            if (_leverRect != null) _upSize = _leverRect.sizeDelta;
            if (_button != null) _button.onClick.AddListener(HandleClicked);
            ShowUp();
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClicked);
        }

        private void OnDisable()
        {
            Tween.StopAll(onTarget: this);
            _pulling = false;
            ShowUp();
            SetOutline(false);
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null) _button.interactable = interactable;
            if (_leverImage != null)
            {
                var c = _leverImage.color;
                _leverImage.color = new Color(c.r, c.g, c.b, interactable ? 1f : DisabledAlpha);
            }
            if (!interactable) SetOutline(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button == null || !_button.interactable || _pulling) return;
            SetOutline(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetOutline(false);
        }

        private void HandleClicked()
        {
            if (_pulling) return;

            if (!CanJuice() || _leverRect == null)
            {
                OnPulled?.Invoke();
                return;
            }

            _pulling = true;
            SetOutline(false);

            // Bajada: comprimir el frame de arriba hasta la altura del bajado —
            // con el pivot en la base, la manija "recorre" el arco hacia abajo.
            float downHeight = DownHeight();
            Tween.Custom(this, _upSize.y, downHeight, _settings.LeverPressDuration,
                    (self, h) => self.SetHeight(h), Ease.InQuad, useUnscaledTime: true)
                .OnComplete(this, self =>
                {
                    self.ApplySprite(self._spriteDown, new Vector2(self._upSize.x, self.DownHeight()));
                    // El roll dispara con la palanca abajo — los reels arrancan
                    // mientras vuelve.
                    self.OnPulled?.Invoke();
                    Tween.Delay(self, self._settings.LeverHoldDuration,
                        s => s.PlayReturn(), useUnscaledTime: true);
                });
        }

        private void PlayReturn()
        {
            // La vuelta arranca desde el frame de arriba comprimido y se estira
            // hasta el reposo, más lenta que la bajada.
            ApplySprite(_spriteUp, new Vector2(_upSize.x, DownHeight()));
            Tween.Custom(this, DownHeight(), _upSize.y, _settings.LeverReturnDuration,
                    (self, h) => self.SetHeight(h), _settings.LeverReturnEase, useUnscaledTime: true)
                .OnComplete(this, self =>
                {
                    self._pulling = false;
                    self.ShowUp();
                });
        }

        private float DownHeight()
        {
            // Alto nativo del frame bajado, escalado al ancho de la palanca.
            return _spriteDown != null
                ? _spriteDown.rect.height * (_upSize.x / Mathf.Max(1f, _spriteDown.rect.width))
                : _upSize.y * 0.4f;
        }

        private void SetHeight(float height)
        {
            if (_leverRect != null) _leverRect.sizeDelta = new Vector2(_upSize.x, height);
        }

        private void ShowUp() => ApplySprite(_spriteUp, _upSize);

        private void ApplySprite(Sprite sprite, Vector2 size)
        {
            if (_leverImage == null || sprite == null) return;
            _leverImage.sprite = sprite;
            if (_leverRect != null && size != Vector2.zero) _leverRect.sizeDelta = size;
        }

        private void SetOutline(bool on)
        {
            if (_leverImage == null) return;
            if (_outline == null)
            {
                if (!on) return;
                _outline = _leverImage.GetComponent<Outline>();
                if (_outline == null) _outline = _leverImage.gameObject.AddComponent<Outline>();
            }
            if (on && _settings != null)
            {
                _outline.effectColor = _settings.ClickableOutlineColor;
                _outline.effectDistance = _settings.ClickableOutlineDistance;
            }
            _outline.enabled = on;
        }

        private bool CanJuice()
        {
            return _settings != null && Application.isPlaying && !DiceUiMotionPrefs.ReducedMotion;
        }
    }
}
