using System;
using System.Collections.Generic;
using PrimeTween;
using Rollgeon.Localization;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.Upgrades.Dice.UI
{
    /// <summary>
    /// Un slot de la slot machine del altar. El nombre vive dentro de un
    /// viewport enmascarado (<c>RectMask2D</c>): durante el spin la columna de
    /// labels desfila de arriba hacia abajo como un reel real — el nombre actual
    /// sale por abajo mientras el siguiente entra desde arriba — y el
    /// encantamiento final aterriza centrado con un settle. Hover con opción
    /// aterrizada prende un outline + notifica a la view para la descripción;
    /// click = encantar. El icono es un ref reservado — los encantamientos
    /// todavía no tienen arte propio.
    /// </summary>
    [AddComponentMenu("Rollgeon/Upgrades/Dice/UI/Enchantment Option Slot View")]
    public sealed class EnchantmentOptionSlotView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private const float EmptyAlpha = 0.45f;
        private const float FallbackRowHeight = 86f;

        [Title("Widget refs")]
        [Required, SerializeField] private Button _button;
        [Required, SerializeField] private Image _background;

        [Tooltip("Label central — el nombre visible en reposo y el 'actual' durante el spin.")]
        [Required, SerializeField] private TextMeshProUGUI _nameLabel;

        [Title("Reel (spin vertical)")]
        [Tooltip("Columna dentro del viewport enmascarado; se desplaza hacia abajo durante el spin.")]
        [SerializeField, Optional] private RectTransform _reelColumn;

        [Tooltip("Segundo label, una fila ARRIBA del central — el nombre entrante.")]
        [SerializeField, Optional] private TextMeshProUGUI _spinLabel;

        [Tooltip("Reservado — cuando los encantamientos tengan icono, va acá.")]
        [SerializeField, Optional] private Image _icon;

        [SerializeField, Optional] private EnchantmentAltarUiSettingsSO _settings;

        private int _index;
        private Action<int> _onClick;
        private Action<int, bool> _onHoverChanged;
        private Outline _outline;
        private Vector3 _baseScale = Vector3.one;
        private bool _baseScaleCaptured;
        private int _lastSpinCell = -1;
        private bool _selected;

        /// <summary>Encantamiento aterrizado en este slot, o null (vacío / girando).</summary>
        public EnchantmentSO Option { get; private set; }

        private void Awake()
        {
            CaptureBaseScale();
            if (_button != null) _button.onClick.AddListener(HandleClicked);
            if (_icon != null) _icon.enabled = false;
            if (_spinLabel != null) _spinLabel.text = string.Empty;
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClicked);
        }

        private void OnDisable()
        {
            Tween.StopAll(onTarget: this);
            Tween.StopAll(onTarget: transform);
            if (_baseScaleCaptured) transform.localScale = _baseScale;
            ResetReelPose();
            SetOutline(false);
        }

        public void Configure(int index, Action<int> onClick, Action<int, bool> onHoverChanged)
        {
            _index = index;
            _onClick = onClick;
            _onHoverChanged = onHoverChanged;
        }

        /// <summary>Estado de reposo — sin opción, no clickeable.</summary>
        public void SetEmpty()
        {
            Option = null;
            _selected = false;
            Tween.StopAll(onTarget: this);
            ResetReelPose();
            SetInteractable(false);
            SetOutline(false);
            if (_nameLabel != null)
            {
                _nameLabel.text = "?";
                SetLabelAlpha(EmptyAlpha);
            }
        }

        /// <summary>Aterriza la opción final del roll en este slot (sin animación).</summary>
        public void SetOption(EnchantmentSO ench)
        {
            Option = ench;
            ResetReelPose();
            if (_nameLabel != null)
            {
                _nameLabel.text = ench != null ? FormatName(ench) : "—";
                SetLabelAlpha(ench != null ? 1f : EmptyAlpha);
            }
        }

        /// <summary>
        /// Corre el reel completo: <paramref name="cycles"/> nombres desfilan de
        /// arriba hacia abajo con la desaceleración OutQuart (frenéticos al
        /// principio, espaciados al final) y <paramref name="final"/> aterriza
        /// centrado con settle + punch. Sin juice (settings/refs ausentes,
        /// reduced motion) aterriza directo.
        /// </summary>
        public void PlaySpin(float duration, int cycles, IReadOnlyList<string> names, int nameOffset,
            EnchantmentSO final, Action onLanded)
        {
            if (_settings == null || !Application.isPlaying || DiceUiMotionPrefs.ReducedMotion
                || _reelColumn == null || _spinLabel == null || names == null || names.Count == 0
                || cycles <= 0 || duration <= 0f)
            {
                SetOption(final);
                onLanded?.Invoke();
                return;
            }

            Option = null;
            _selected = false;
            SetOutline(false);
            SetInteractable(false);
            SetLabelAlpha(1f);
            _lastSpinCell = -1;

            Tween.StopAll(onTarget: this);
            Tween.Custom(this, 0f, 1f, duration, (self, t) =>
            {
                self.TickSpin(t, cycles, names, nameOffset, final);
            }, Ease.Linear, useUnscaledTime: true)
            .OnComplete(this, self => self.LandSpin(final, onLanded));
        }

        private void TickSpin(float t, int cycles, IReadOnlyList<string> names, int nameOffset,
            EnchantmentSO final)
        {
            // Misma curva base que ChestReelMath: OutQuart sobre el avance en
            // celdas — la velocidad de desfile decae hacia el click final.
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 4f);
            float cells = eased * cycles;
            int cell = Mathf.FloorToInt(cells);
            float frac = cells - cell;

            if (cell != _lastSpinCell)
            {
                _lastSpinCell = cell;
                if (_nameLabel != null) _nameLabel.text = NameAt(names, cell, nameOffset, cycles, final);
                if (_spinLabel != null) _spinLabel.text = NameAt(names, cell + 1, nameOffset, cycles, final);
            }

            // La columna baja una fila por celda: el actual sale por abajo del
            // viewport y el entrante (una fila arriba) cae hacia el centro.
            _reelColumn.anchoredPosition = new Vector2(0f, -frac * RowHeight());
        }

        private static string NameAt(IReadOnlyList<string> names, int cell, int nameOffset, int cycles,
            EnchantmentSO final)
        {
            // La última celda del desfile ES la opción final — así el nombre que
            // queda centrado al frenar es el real, no uno decorativo.
            if (final != null && cell >= cycles) return FormatName(final);
            return names[(cell + nameOffset) % names.Count];
        }

        private void LandSpin(EnchantmentSO final, Action onLanded)
        {
            SetOption(final);

            // Settle: la columna rebota apenas hacia arriba, como el reel que
            // clava la posición, + punch de escala de la card.
            if (_reelColumn != null)
            {
                Tween.PunchLocalPosition(_reelColumn, new Vector3(0f, RowHeight() * 0.18f, 0f),
                    _settings.ReelLandPunchDuration, frequency: 2f, useUnscaledTime: true);
            }
            PlayLandPunch();
            onLanded?.Invoke();
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null) _button.interactable = interactable;
            if (!interactable && !_selected) SetOutline(false);
        }

        /// <summary>
        /// Marca este slot como el encantamiento elegido de la oferta — el
        /// outline queda fijo hasta deseleccionar (flow: encantamiento → dado).
        /// </summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            SetOutline(selected);
            if (selected) PlayLandPunch();
        }

        public void PlayLandPunch()
        {
            if (!CanJuice()) return;
            CaptureBaseScale();
            Tween.StopAll(onTarget: transform);
            transform.localScale = _baseScale;
            Tween.PunchScale(transform, Vector3.one * _settings.ReelLandPunchScale,
                _settings.ReelLandPunchDuration, useUnscaledTime: true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button == null || !_button.interactable || Option == null) return;
            SetOutline(true);
            _onHoverChanged?.Invoke(_index, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_selected) SetOutline(false);
            _onHoverChanged?.Invoke(_index, false);
        }

        public static string FormatName(EnchantmentSO ench)
        {
            if (ench == null) return string.Empty;
            string display = !string.IsNullOrEmpty(ench.DisplayName) ? ench.DisplayName : ench.UpgradeId;
            return $"<color=#{EnchantmentPalette.TitleHex(ench)}>{LocalizedContent.Name(ench.UpgradeId, display)}</color>";
        }

        private float RowHeight()
        {
            // Una fila = el alto del viewport (el padre enmascarado de la columna).
            var viewport = _reelColumn != null ? _reelColumn.parent as RectTransform : null;
            float h = viewport != null ? viewport.rect.height : 0f;
            return h > 1f ? h : FallbackRowHeight;
        }

        private void ResetReelPose()
        {
            if (_reelColumn != null)
            {
                Tween.StopAll(onTarget: _reelColumn);
                _reelColumn.anchoredPosition = Vector2.zero;
            }
            if (_spinLabel != null) _spinLabel.text = string.Empty;
        }

        private void SetLabelAlpha(float alpha)
        {
            if (_nameLabel == null) return;
            var c = _nameLabel.color;
            _nameLabel.color = new Color(c.r, c.g, c.b, alpha);
            if (_spinLabel != null)
            {
                var s = _spinLabel.color;
                _spinLabel.color = new Color(s.r, s.g, s.b, alpha);
            }
        }

        private void SetOutline(bool on)
        {
            if (_background == null) return;
            if (_outline == null)
            {
                if (!on) return;
                _outline = _background.GetComponent<Outline>();
                if (_outline == null) _outline = _background.gameObject.AddComponent<Outline>();
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

        private void CaptureBaseScale()
        {
            if (_baseScaleCaptured) return;
            _baseScale = transform.localScale;
            _baseScaleCaptured = true;
        }

        private void HandleClicked() => _onClick?.Invoke(_index);
    }
}
