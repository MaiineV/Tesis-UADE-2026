#if UNITY_EDITOR
using Rollgeon.Effects.Selection;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Drawers
{
    /// <summary>
    /// Dibuja un <c>bool[]</c> decorado con <see cref="BoolGridAttribute"/> como grilla de
    /// botones toggle (verde = activo, amarillo = celda centro). Auto-resizea el array
    /// cuando Rows/Cols cambian, preservando lo que entra. Port de Bot-Game.
    /// </summary>
    public sealed class BoolGridAttributeDrawer : OdinAttributeDrawer<BoolGridAttribute, bool[]>
    {
        private const float CellSize = 24f;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var attr = Attribute;
            int rows = GetSiblingInt(attr.RowsProperty, 1);
            int cols = GetSiblingInt(attr.ColsProperty, 1);
            var center = attr.CenterProperty != null ? GetSiblingVector2Int(attr.CenterProperty) : (Vector2Int?)null;

            var arr = ValueEntry.SmartValue;
            int needed = rows * cols;

            if (arr == null || arr.Length != needed)
            {
                var resized = new bool[needed];
                if (arr != null)
                {
                    for (int i = 0; i < Mathf.Min(arr.Length, needed); i++)
                        resized[i] = arr[i];
                }
                ValueEntry.SmartValue = resized;
                arr = resized;
            }

            if (label != null)
                EditorGUILayout.LabelField(label);

            float totalWidth = cols * CellSize;
            var gridRect = GUILayoutUtility.GetRect(totalWidth, rows * CellSize);
            gridRect.width = Mathf.Min(gridRect.width, totalWidth);

            bool changed = false;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    var cellRect = new Rect(gridRect.x + c * CellSize, gridRect.y + r * CellSize, CellSize, CellSize);

                    bool isCenter = center.HasValue && c == center.Value.x && r == center.Value.y;
                    bool value = idx < arr.Length && arr[idx];

                    if (isCenter)
                        GUI.color = value ? new Color(1f, 0.85f, 0f) : new Color(0.6f, 0.55f, 0.3f);
                    else
                        GUI.color = value ? Color.green : new Color(0.35f, 0.35f, 0.35f);

                    if (GUI.Button(cellRect, GUIContent.none))
                    {
                        arr[idx] = !arr[idx];
                        changed = true;
                    }

                    GUI.color = Color.white;
                }
            }

            if (changed)
            {
                ValueEntry.SmartValue = arr;
                ValueEntry.ApplyChanges();
            }
        }

        private int GetSiblingInt(string name, int fallback)
        {
            var sibling = Property.Parent?.Children[name];
            if (sibling != null)
            {
                var val = sibling.ValueEntry?.WeakSmartValue;
                if (val is int i) return i;
            }
            return fallback;
        }

        private Vector2Int? GetSiblingVector2Int(string name)
        {
            var sibling = Property.Parent?.Children[name];
            if (sibling != null)
            {
                var val = sibling.ValueEntry?.WeakSmartValue;
                if (val is Vector2Int v) return v;
            }
            return null;
        }
    }
}
#endif
