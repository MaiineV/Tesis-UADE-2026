using PrimeTween;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Un contador del breakdown (N o M). Formatea entero (N) o con un decimal (M),
    /// y hace punch de escala cuando un aporte le llega volando.
    /// </summary>
    public sealed class BreakdownCounterView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;

        [SerializeField]
        [Tooltip("Cuánto se infla el contador al recibir un aporte (1.18 = +18%).")]
        private float _punchScale = 1.18f;

        [SerializeField] private float _punchSeconds = 0.14f;

        private bool _isMultiplier;
        private float _value;
        private Tween _punch;
        private Tween _rotJiggle;

        // Identidad de color: N usa color fijo; M usa "heat" por valor (apagado en 1.0,
        // calienta hacia rojo con el multiplicador). Sin configurar, el label queda como
        // esté autorado en el prefab.
        private bool _hasStaticColor;
        private Color _staticColor;
        private bool _hasHeat;
        private Color _heatNeutral, _heatWarm, _heatHot;

        /// <summary>Ancla para los vuelos de valores (destino).</summary>
        public RectTransform Anchor => (RectTransform)transform;

        public float Value => _value;

        /// <summary>Color fijo del contador (modo N). Anula el modo heat.</summary>
        public void SetStaticColor(Color color)
        {
            _hasStaticColor = true;
            _staticColor = color;
            _hasHeat = false;
            Render();
        }

        /// <summary>Color por valor (modo M): apagado en 1.0 → warm → hot.</summary>
        public void SetHeatColors(Color neutral, Color warm, Color hot)
        {
            _hasHeat = true;
            _heatNeutral = neutral;
            _heatWarm = warm;
            _heatHot = hot;
            _hasStaticColor = false;
            Render();
        }

        public void SetValue(float value, bool isMultiplier)
        {
            _value = value;
            _isMultiplier = isMultiplier;
            Render();
        }

        /// <summary>Aplica un aporte (suma si es N, multiplica si es M) con punch.</summary>
        public void AddAndPunch(float amount, float punchIntensity = 1f, float rotationDegrees = 0f)
        {
            _value = _isMultiplier ? _value * amount : _value + amount;
            Render();
            Punch(punchIntensity, rotationDegrees);
        }

        public void Punch(float intensity = 1f, float rotationDegrees = 0f)
        {
            if (_punch.isAlive) _punch.Stop();
            transform.localScale = Vector3.one;
            _punch = Tween.PunchScale(transform,
                Vector3.one * ((_punchScale - 1f) * Mathf.Max(0.1f, intensity)),
                _punchSeconds, frequency: 2);

            if (rotationDegrees <= 0f) return;
            if (_rotJiggle.isAlive) _rotJiggle.Stop();
            transform.localRotation = Quaternion.identity;
            _rotJiggle = Tween.ShakeLocalRotation(transform,
                new Vector3(0f, 0f, rotationDegrees), _punchSeconds, frequency: 2);
        }

        /// <summary>N entero; M con un decimal ("1.0", "2.5") para que se lea como multi.</summary>
        public static string Format(float value, bool isMultiplier)
            => isMultiplier ? value.ToString("0.0#") : Mathf.RoundToInt(value).ToString();

        private void Render()
        {
            if (_label == null) return;
            _label.text = Format(_value, _isMultiplier);
            if (_hasHeat)
                _label.color = BreakdownFeelMath.HeatColor(_value, _heatNeutral, _heatWarm, _heatHot);
            else if (_hasStaticColor)
                _label.color = _staticColor;
        }

        private void OnDisable()
        {
            if (_punch.isAlive) _punch.Stop();
            if (_rotJiggle.isAlive) _rotJiggle.Stop();
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }
    }
}
