using TMPro;
using UnityEngine;

namespace Rollgeon.UI
{
    /// <summary>
    /// Curva un TMP a lo largo de un arco parabólico (el centro sube
    /// <see cref="_curveHeight"/> px y los extremos quedan en la línea base),
    /// rotando cada carácter para seguir la tangente — el título "de marquesina"
    /// que acompaña el domo de la slot machine del altar. Re-warpa solo cuando
    /// el texto cambia.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Arc Text Warp")]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ArcTextWarp : MonoBehaviour
    {
        [Tooltip("Cuánto sube el centro del texto respecto a los extremos (px).")]
        [SerializeField] private float _curveHeight = 28f;

        private TMP_Text _text;
        private string _lastText;
        private float _lastCurve;

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            _lastText = null;
            Warp();
        }

        private void LateUpdate()
        {
            // El warp pisa la malla que TMP regenera en cada layout — re-aplicar
            // cuando el contenido (o el tuning) cambió.
            if (_text == null) return;
            if (_text.text == _lastText && Mathf.Approximately(_curveHeight, _lastCurve)) return;
            Warp();
        }

        private void Warp()
        {
            if (_text == null || string.IsNullOrEmpty(_text.text)) return;

            _text.ForceMeshUpdate();
            var textInfo = _text.textInfo;
            if (textInfo == null || textInfo.characterCount == 0) return;

            // Extremos reales del texto renderizado — el arco se normaliza a eso.
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;
                minX = Mathf.Min(minX, charInfo.bottomLeft.x);
                maxX = Mathf.Max(maxX, charInfo.topRight.x);
            }
            float width = maxX - minX;
            if (width <= 0f) return;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;
                var vertices = textInfo.meshInfo[materialIndex].vertices;

                var mid = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
                float nx = Mathf.Clamp(((mid.x - minX) / width) * 2f - 1f, -1f, 1f); // [-1..1]

                // Parábola: y = h(1-x²); tangente: dy/dx = -2hx / (width/2).
                float yOffset = _curveHeight * (1f - nx * nx);
                float slope = -2f * _curveHeight * nx / (width * 0.5f);
                float angle = Mathf.Atan(slope) * Mathf.Rad2Deg;

                var matrix = Matrix4x4.TRS(
                    new Vector3(0f, yOffset, 0f) + mid,
                    Quaternion.Euler(0f, 0f, angle),
                    Vector3.one) * Matrix4x4.Translate(-mid);

                for (int v = 0; v < 4; v++)
                    vertices[vertexIndex + v] = matrix.MultiplyPoint3x4(vertices[vertexIndex + v]);
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            _lastText = _text.text;
            _lastCurve = _curveHeight;
        }
    }
}
