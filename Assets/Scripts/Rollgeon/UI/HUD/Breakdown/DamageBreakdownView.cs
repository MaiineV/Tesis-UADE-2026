using System;
using Patterns;
using PrimeTween;
using Rollgeon.Audio;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// El "N × M" que reemplaza al texto de fórmula en modo daño-por-combo. En preview
    /// (pre-confirm) muestra N = base del combo y M = perilla de la habilidad (1.0 usual);
    /// el resto de los valores llega volando durante la secuencia del director.
    /// Lo muestra/oculta <c>DamageFormulaView</c>, que ya es dueño de la detección de modo
    /// (daño vs. escudo vs. action roll) — esta view no duplica esa lógica.
    /// </summary>
    public sealed class DamageBreakdownView : MonoBehaviour
    {
        [SerializeField] private BreakdownCounterView _counterN;
        [SerializeField] private BreakdownCounterView _counterM;
        [SerializeField] private TextMeshProUGUI _multSign;

        [SerializeField]
        [Tooltip("Opcional — visibilidad por alpha. Sin CanvasGroup se togglea el GameObject.")]
        private CanvasGroup _group;

        [SerializeField, Optional]
        [Tooltip("Colores semánticos N/M. Necesario acá (y no solo en el director) porque " +
                 "el preview lo muestra DamageFormulaView sin pasar por la secuencia.")]
        private BreakdownAnimSettingsSO _settings;

        [SerializeField, Optional]
        [Tooltip("Tick al cambiar el preview por toggle de hold (sugerido: sfx_dice_preview_tick).")]
        private AudioClip _previewTickClip;

        [SerializeField, Optional]
        [Tooltip("Nombre del combo activo, arriba del N×M — el jugador siempre sabe qué armó.")]
        private TextMeshProUGUI _comboNameLabel;

        private float _nextTickAt;
        private float _wobbleAmplitude;
        private Tween _popIn;
        private Tween _wobble;

        public BreakdownCounterView CounterN => _counterN;
        public BreakdownCounterView CounterM => _counterM;
        public TextMeshProUGUI MultSign => _multSign;

        public bool IsShowing { get; private set; }

        /// <summary>
        /// Alguien pidió un preview (nuevo o refrescado). El director lo usa para saber
        /// que el total del choque que dejó en pantalla ya no corresponde y apagarlo.
        /// </summary>
        public event Action PreviewShown;

        /// <summary>Alguien ocultó el N×M (Clear de la fórmula, otro modo). Ídem arriba.</summary>
        public event Action Hidden;

        private void Awake()
        {
            if (_settings == null) return;
            if (_counterN != null) _counterN.SetStaticColor(_settings.CounterNColor);
            if (_counterM != null)
                _counterM.SetHeatColors(_settings.CounterMNeutralColor,
                    _settings.CounterMWarmColor, _settings.CounterMHotColor);
            if (_multSign != null) _multSign.color = _settings.CounterMNeutralColor;
        }

        public void ShowPreview(int comboBase, float abilityMultiplier)
        {
            bool wasShowing = IsShowing;
            // El valor real del contador es la fuente de verdad (la secuencia lo pudo
            // haber dejado en los finales) — no un cache paralelo.
            bool changed = _counterN == null || _counterM == null
                || !Mathf.Approximately(_counterN.Value, comboBase)
                || !Mathf.Approximately(_counterM.Value, abilityMultiplier);
            SetVisible(true);
            PreviewShown?.Invoke();

            // Re-match sin cambio de valores (spam de toggles de hold) ⇒ no-op total.
            if (wasShowing && !changed) return;

            if (wasShowing && Application.isPlaying)
            {
                // Cambio en caliente: roll-up + tick en vez de snap.
                float roll = _settings != null ? _settings.PreviewRollupSeconds : 0.15f;
                _counterN?.TweenToValue(comboBase, isMultiplier: false, roll);
                _counterM?.TweenToValue(abilityMultiplier, isMultiplier: true, roll);
                PlayTick();
            }
            else
            {
                if (_counterN != null) _counterN.SetValue(comboBase, isMultiplier: false);
                if (_counterM != null) _counterM.SetValue(abilityMultiplier, isMultiplier: true);
                PlayPopIn();
            }

            RefreshWobble(abilityMultiplier);
        }

        // Pop-in OutBack al pasar de oculto a visible (Balatro: nada aparece seco).
        private void PlayPopIn()
        {
            float pop = _settings != null ? _settings.PreviewPopSeconds : 0.18f;
            if (!Application.isPlaying || DiceUiMotionPrefs.ReducedMotion || pop <= 0f) return;
            if (_popIn.isAlive) _popIn.Stop();
            transform.localScale = Vector3.one * 0.6f;
            _popIn = Tween.Scale(transform, 1f, pop, Ease.OutBack);
        }

        // Idle wobble de M: "late" cuando hay multiplicador (amplitud escala con M−1).
        private void RefreshWobble(float m)
        {
            float target = (_settings != null ? _settings.WobbleMaxDegrees : 3f) * Mathf.Clamp01(m - 1f);
            if (!Application.isPlaying || DiceUiMotionPrefs.ReducedMotion) target = 0f;
            if (Mathf.Approximately(target, _wobbleAmplitude)) return;
            _wobbleAmplitude = target;
            if (_wobble.isAlive) _wobble.Stop();
            if (_counterM == null) return;
            _counterM.transform.localRotation = Quaternion.identity;
            if (target <= 0f) return;
            float halfPeriod = 0.5f / Mathf.Max(0.1f, _settings != null ? _settings.WobbleHz : 1.4f);
            _wobble = Tween.LocalEulerAngles(_counterM.transform,
                new Vector3(0f, 0f, -target), new Vector3(0f, 0f, target),
                halfPeriod, Ease.InOutSine, cycles: -1, CycleMode.Yoyo);
        }

        private void StopMotion()
        {
            if (_popIn.isAlive) _popIn.Stop();
            if (_wobble.isAlive) _wobble.Stop();
            _wobbleAmplitude = 0f;
            transform.localScale = Vector3.one;
            if (_counterM != null) _counterM.transform.localRotation = Quaternion.identity;
        }

        private void OnDisable() => StopMotion();

        private void PlayTick()
        {
            if (_previewTickClip == null || !Application.isPlaying) return;
            if (_settings != null && !_settings.EnableSfx) return;
            if (Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + 0.05f;
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx2D(_previewTickClip, 0.5f);
        }

        /// <summary>Nombre del combo activo (vacío = sin texto). Persiste durante la secuencia.</summary>
        public void SetComboName(string comboName)
        {
            if (_comboNameLabel == null) return;
            _comboNameLabel.text = comboName ?? string.Empty;
        }

        public void Hide()
        {
            StopMotion();
            SetVisible(false);
            Hidden?.Invoke();
        }

        private void SetVisible(bool visible)
        {
            IsShowing = visible;
            if (_group != null)
            {
                _group.alpha = visible ? 1f : 0f;
                _group.blocksRaycasts = false;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
