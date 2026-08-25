using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Upgrades.Dice.UI
{
    /// <summary>
    /// El botón Confirmar de la máquina. En reposo muestra el sprite apagado
    /// (<c>SlotMachineButtons_2</c>) y no es clickeable. Cuando hay encantamiento
    /// + dado elegidos pasa al sprite prendido (<c>_0</c>) con un pulso de brillo
    /// infinito — y en el valle del pulso parpadea de vuelta al apagado, el
    /// "apretame" clásico de las slot. El estado presionado (<c>_1</c>) lo maneja
    /// el SpriteSwap del Button (overrideSprite pisa cualquier sprite del pulso).
    /// </summary>
    [AddComponentMenu("Rollgeon/Upgrades/Dice/UI/Altar Confirm Button View")]
    public sealed class AltarConfirmButtonView : MonoBehaviour
    {
        [Title("Widget refs")]
        [Required, SerializeField] private Button _button;
        [Required, SerializeField] private Image _image;

        [Title("Sprites (SlotMachineButtons)")]
        [Tooltip("_2 — apagado / deshabilitado.")]
        [Required, SerializeField] private Sprite _spriteIdle;

        [Tooltip("_0 — prendido, pulsando mientras espera el click.")]
        [Required, SerializeField] private Sprite _spriteReady;

        [SerializeField, Optional] private EnchantmentAltarUiSettingsSO _settings;

        /// <summary>Expuesto para que la view suscriba el click y el tutorial ancle.</summary>
        public Button Button => _button;

        private bool _ready;
        private Outline _glow;

        private void OnDisable()
        {
            Tween.StopAll(onTarget: this);
            ApplyIdleVisual();
        }

        /// <summary>Habilita (y arranca el pulso) o vuelve al reposo apagado.</summary>
        public void SetReady(bool ready)
        {
            if (_ready == ready) return;
            _ready = ready;

            Tween.StopAll(onTarget: this);
            if (_button != null) _button.interactable = ready;

            if (!ready)
            {
                ApplyIdleVisual();
                return;
            }

            if (!CanJuice())
            {
                if (_image != null)
                {
                    _image.sprite = _spriteReady;
                    _image.color = Color.white;
                }
                return;
            }

            // Pulso 1→0→1 infinito: brillo por multiplicación de color, glow
            // dorado (outline) que acompaña hasta el pico — el "más brilloso"
            // que una Image no puede dar sola —, y por debajo del threshold el
            // sprite parpadea al apagado.
            Tween.Custom(this, 1f, 0f, _settings.ConfirmPulseHalfDuration, (self, t) =>
            {
                if (self._image == null) return;
                self._image.sprite = t < self._settings.ConfirmPulseSwapThreshold
                    ? self._spriteIdle
                    : self._spriteReady;
                float brightness = Mathf.Lerp(self._settings.ConfirmPulseMinBrightness, 1f, t);
                self._image.color = new Color(brightness, brightness, brightness, 1f);
                self.SetGlow(t * self._settings.ConfirmPeakGlowAlpha);
            }, Ease.InOutSine, cycles: -1, CycleMode.Yoyo, useUnscaledTime: true);
        }

        private void SetGlow(float alpha)
        {
            if (_image == null || _settings == null) return;
            if (_glow == null)
            {
                if (alpha <= 0f) return;
                _glow = _image.GetComponent<Outline>();
                if (_glow == null) _glow = _image.gameObject.AddComponent<Outline>();
                _glow.effectDistance = _settings.ClickableOutlineDistance;
            }
            var c = _settings.ClickableOutlineColor;
            _glow.effectColor = new Color(c.r, c.g, c.b, alpha);
            _glow.enabled = alpha > 0.01f;
        }

        private void ApplyIdleVisual()
        {
            if (_image == null) return;
            _image.sprite = _spriteIdle;
            _image.color = Color.white;
            if (_glow != null) _glow.enabled = false;
        }

        private bool CanJuice()
        {
            return _settings != null && Application.isPlaying && !DiceUiMotionPrefs.ReducedMotion;
        }
    }
}
