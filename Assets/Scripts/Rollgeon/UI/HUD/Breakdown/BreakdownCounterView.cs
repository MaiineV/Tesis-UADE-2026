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

        /// <summary>Ancla para los vuelos de valores (destino).</summary>
        public RectTransform Anchor => (RectTransform)transform;

        public float Value => _value;

        public void SetValue(float value, bool isMultiplier)
        {
            _value = value;
            _isMultiplier = isMultiplier;
            Render();
        }

        /// <summary>Aplica un aporte (suma si es N, multiplica si es M) con punch.</summary>
        public void AddAndPunch(float amount)
        {
            _value = _isMultiplier ? _value * amount : _value + amount;
            Render();
            Punch();
        }

        public void Punch()
        {
            if (_punch.isAlive) _punch.Stop();
            transform.localScale = Vector3.one;
            _punch = Tween.PunchScale(transform, Vector3.one * (_punchScale - 1f), _punchSeconds, frequency: 2);
        }

        /// <summary>N entero; M con un decimal ("1.0", "2.5") para que se lea como multi.</summary>
        public static string Format(float value, bool isMultiplier)
            => isMultiplier ? value.ToString("0.0#") : Mathf.RoundToInt(value).ToString();

        private void Render()
        {
            if (_label != null) _label.text = Format(_value, _isMultiplier);
        }

        private void OnDisable()
        {
            if (_punch.isAlive) _punch.Stop();
            transform.localScale = Vector3.one;
        }
    }
}
