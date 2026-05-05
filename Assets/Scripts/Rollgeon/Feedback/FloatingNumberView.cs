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

        // Camera resuelta en Initialize y reusada cada frame para billboard. Si es null,
        // el view degrada a comportamiento legacy (sin billboard, rise por world up).
        private Camera _camera;
        private float _born;

        public enum NumberType { Damage, Heal, Shield, Generic }

        public void Initialize(string text, NumberType type, Vector3 position)
        {
            transform.position = position;
            _born = Time.time;
            _camera = Camera.main;

            // Billboard inicial: orientamos el texto para que su forward coincida con el
            // forward de la cámara — el TextMesh se ve "derecho" en perspectiva iso. Sin
            // esto, el prefab quedaba con rotation identity y aparecía inclinado.
            ApplyBillboard();

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
            // Subimos en "arriba de pantalla" (cam.up), no en world up, para que el rise
            // se vea correcto desde cualquier ángulo de cámara iso.
            var up = _camera != null ? _camera.transform.up : Vector3.up;
            transform.position += up * (_riseSpeed * Time.deltaTime);

            // Re-billboard por si la cámara se mueve mientras el número está vivo.
            ApplyBillboard();

            if (Time.time - _born >= _lifeSeconds) Destroy(gameObject);
        }

        private void ApplyBillboard()
        {
            if (_camera == null) return;
            // LookAt orienta el +Z local hacia la cámara — el TextMesh renderiza el texto
            // en su cara +Z, así que con esto la cara con texto siempre mira al lente.
            // Usamos cam.up como referencia upright para que el rise sea consistente con
            // la perspectiva (en iso, world up ≠ screen up).
            transform.LookAt(_camera.transform.position, _camera.transform.up);
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
