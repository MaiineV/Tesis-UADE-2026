using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Fija el RectTransform del <see cref="TMP_SubMeshUI"/> (el mesh que TMP crea como
    /// hijo del label para dibujar sprites inline) a los offsets calibrados a ojo para el
    /// DmgIndicator del badge de daño. Los valores son los del inspector de un rect
    /// stretch (Left/Top/Right/Bottom).
    /// </summary>
    /// <remarks>
    /// No alcanza con setearlo una vez en el editor: TMP crea el submesh lazy en el
    /// primer render del sprite y puede resetearle el rect en un rebuild — este
    /// componente lo re-afirma por frame, solo cuando difiere.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Tooltips/TMP SubMesh Rect Offset")]
    public sealed class TMPSubMeshRectOffset : MonoBehaviour
    {
        [Tooltip("Left del rect del submesh, en px de inspector.")]
        [SerializeField] private float _left = -9f;

        [Tooltip("Top del rect del submesh.")]
        [SerializeField] private float _top = 2.5f;

        [Tooltip("Right del rect del submesh.")]
        [SerializeField] private float _right = 4f;

        [Tooltip("Bottom del rect del submesh.")]
        [SerializeField] private float _bottom = 5.5f;

        private void LateUpdate()
        {
            // Inspector → offsets: Left = offsetMin.x, Bottom = offsetMin.y,
            // Right = -offsetMax.x, Top = -offsetMax.y.
            var min = new Vector2(_left, _bottom);
            var max = new Vector2(-_right, -_top);

            for (int i = 0; i < transform.childCount; i++)
            {
                if (!transform.GetChild(i).TryGetComponent<TMP_SubMeshUI>(out var sub))
                    continue;

                var rect = (RectTransform)sub.transform;
                if (rect.offsetMin != min) rect.offsetMin = min;
                if (rect.offsetMax != max) rect.offsetMax = max;
            }
        }
    }
}
