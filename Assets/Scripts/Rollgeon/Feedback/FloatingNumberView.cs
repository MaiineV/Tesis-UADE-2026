using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// View runtime del prefab <c>FloatingNumber</c> (en <c>Resources/FloatingNumber</c>).
    /// Spawnea con <see cref="Initialize"/>, hace bob + fade, y se autodestruye al terminar.
    /// TECHNICAL.md §10.7.
    /// </summary>
    /// <remarks>
    /// Si no hay <c>TextMesh</c> / <c>UnityEngine.UI.Text</c> / <c>TMP_Text</c> asignado,
    /// solo se mueve y logea — el prefab autoral decide qué renderer usar. Esto desacopla
    /// el código de TextMeshPro como hard dependency.
    /// </remarks>
    public sealed class FloatingNumberView : MonoBehaviour
    {
        [SerializeField] private TextMesh _textMesh;
        [SerializeField] private UnityEngine.UI.Text _uguiText;
        [SerializeField] private float _lifeSeconds = 1.2f;
        [SerializeField] private float _riseSpeed = 1.5f;

        private float _born;

        public enum NumberType { Damage, Heal, Shield, Generic }

        public void Initialize(string text, NumberType type, Vector3 position)
        {
            transform.position = position;
            _born = Time.time;

            var color = TypeToColor(type);
            if (_textMesh != null)
            {
                _textMesh.text = text;
                _textMesh.color = color;
            }
            if (_uguiText != null)
            {
                _uguiText.text = text;
                _uguiText.color = color;
            }
        }

        private void Update()
        {
            transform.position += Vector3.up * (_riseSpeed * Time.deltaTime);
            if (Time.time - _born >= _lifeSeconds) Destroy(gameObject);
        }

        private static Color TypeToColor(NumberType t) => t switch
        {
            NumberType.Damage => Color.red,
            NumberType.Heal => Color.green,
            NumberType.Shield => new Color(0.6f, 0.8f, 1f),
            _ => Color.white,
        };
    }
}
