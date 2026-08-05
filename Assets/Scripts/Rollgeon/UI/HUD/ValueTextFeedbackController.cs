using System.Globalization;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Da color + animación al value text del combate (el <c>_formulaLabel</c> de
    /// <see cref="DamageFormulaView"/>) según el <see cref="DiceBoardType"/> vigente. La animación
    /// es un tag de Febucci Text Animator (shake en ataque, wave/bounce en defensa) que sacude las
    /// LETRAS — se ve bien en UI, a diferencia de mover el transform — y su amplitud escala con el
    /// valor del combo: mejor combo ⇒ más fuerte. Autorado por tipo en <see cref="DiceBoardSkinCatalogSO"/>.
    /// </summary>
    /// <remarks>
    /// El texto lo produce <see cref="DamageFormulaView"/>; este controller lo tiñe y lo envuelve
    /// con <c>&lt;tag amplitude=N&gt;…&lt;/tag&gt;</c>. Si el tipo no tiene tag, el texto queda plano.
    /// El color base se respeta (no hay efecto de color de Text Animator activo).
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Value Text Feedback Controller")]
    public sealed class ValueTextFeedbackController : MonoBehaviour
    {
        private const float DefaultValueForMax = 20f;

        [SerializeField]
        [Tooltip("El value text a animar (el mismo _formulaLabel de DamageFormulaView).")]
        private TextMeshProUGUI _label;

        [SerializeField]
        [Tooltip("Catálogo con TextColor + Feedback por DiceBoardType. Sin catálogo, no hay feedback.")]
        private DiceBoardSkinCatalogSO _catalog;

        [SerializeField]
        [Tooltip("Duración de la transición de color al cambiar el board type. 0 = instantáneo.")]
        private float _colorTweenDuration = 0.15f;

        private TextAnimator_TMP _textAnimator;
        private DiceBoardType _boardType = DiceBoardType.Default;
        private string _rawText = string.Empty;
        private int _lastValue;
        private Tween _colorTween;

        private void Awake()
        {
            if (_label == null) _label = GetComponent<TextMeshProUGUI>();
            if (_label == null) return;

            // Text Animator engancha el mesh del TMP; lo agregamos si falta (mismo patrón que
            // JuicyMenuSetupTools). Sin él, los tags quedan como texto plano (TMP los ignora).
            _textAnimator = _label.GetComponent<TextAnimator_TMP>();
            if (_textAnimator == null) _textAnimator = _label.gameObject.AddComponent<TextAnimator_TMP>();
        }

        /// <summary>
        /// Fija el board type vigente: tiñe el value text con su <c>TextColor</c> y re-renderiza el
        /// texto actual con el tag/amplitud del nuevo tipo.
        /// </summary>
        public void SetBoardType(DiceBoardType type)
        {
            _boardType = type;
            if (_label == null) return;

            if (TryGetEntry(out var skin))
            {
                if (_colorTweenDuration > 0f && Application.isPlaying)
                {
                    if (_colorTween.isAlive) _colorTween.Stop();
                    _colorTween = Tween.Color(_label, skin.TextColor, _colorTweenDuration);
                }
                else
                {
                    _label.color = skin.TextColor;
                }
            }

            if (!string.IsNullOrEmpty(_rawText)) Render();
        }

        /// <summary>
        /// Setea el value text. El tag del tipo lo envuelve con amplitud proporcional a
        /// <paramref name="value"/> (el valor del combo): mayor combo ⇒ más intenso.
        /// </summary>
        public void Show(string rawText, int value)
        {
            _rawText = rawText ?? string.Empty;
            _lastValue = value;
            Render();
        }

        /// <summary>Limpia el value text.</summary>
        public void Clear()
        {
            _rawText = string.Empty;
            _lastValue = 0;
            if (_label != null) _label.text = string.Empty;
        }

        private void Render()
        {
            if (_label == null) return;

            string wrapped = _rawText;
            if (TryGetEntry(out var skin) && !string.IsNullOrEmpty(skin.Feedback.EffectTag))
            {
                var fb = skin.Feedback;
                // Baseline (AmplitudeMin) sin combo; escala hasta AmplitudeMax con el valor.
                float amp = _lastValue > 0
                    ? Intensity(_lastValue, fb.AmplitudeMin, fb.AmplitudeMax, fb.ValueForMax)
                    : fb.AmplitudeMin;
                // InvariantCulture: el parser de Febucci espera punto decimal, no coma.
                string ampStr = amp.ToString("0.##", CultureInfo.InvariantCulture);
                wrapped = $"<{fb.EffectTag} amplitude={ampStr}>{_rawText}</{fb.EffectTag}>";
            }

            if (_textAnimator != null) _textAnimator.SetText(wrapped);
            else _label.text = wrapped;
        }

        private bool TryGetEntry(out DiceBoardSkinEntry skin)
        {
            skin = default;
            return _catalog != null && _catalog.TryGet(_boardType, out skin);
        }

        /// <summary>
        /// Interpola la amplitud entre <paramref name="min"/> y <paramref name="max"/> según el
        /// valor del combo (min en value ≤ 0, max en value ≥ valueForMax). Pura y testeable.
        /// </summary>
        public static float Intensity(int value, float min, float max, float valueForMax)
        {
            if (value <= 0) return min;
            float vmax = valueForMax > 0f ? valueForMax : DefaultValueForMax;
            float t = Mathf.Clamp01(value / vmax);
            return Mathf.Lerp(min, max, t);
        }
    }
}
