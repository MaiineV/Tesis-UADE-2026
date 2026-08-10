using Patterns;
using Rollgeon.Audio;
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

        private float _nextTickAt;

        public BreakdownCounterView CounterN => _counterN;
        public BreakdownCounterView CounterM => _counterM;
        public TextMeshProUGUI MultSign => _multSign;

        public bool IsShowing { get; private set; }

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
            }
        }

        private void PlayTick()
        {
            if (_previewTickClip == null || !Application.isPlaying) return;
            if (_settings != null && !_settings.EnableSfx) return;
            if (Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + 0.05f;
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx2D(_previewTickClip, 0.5f);
        }

        public void Hide() => SetVisible(false);

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
