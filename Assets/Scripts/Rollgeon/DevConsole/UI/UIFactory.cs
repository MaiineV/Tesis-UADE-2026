using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.DevConsole.UI
{
    /// <summary>Builders mínimos para construir la UI de la consola por código (sin prefab).</summary>
    internal static class UIFactory
    {
        public static RectTransform Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string content, float size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            return t;
        }

        public static Button Button(Transform parent, string label, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bg;

            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);

            var label_t = Text(go.transform, "Label", label, 15, Color.white, TextAlignmentOptions.Center);
            Stretch(label_t.gameObject);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 26;
            le.preferredHeight = 28;
            le.flexibleWidth = 1;
            return btn;
        }

        public static VerticalLayoutGroup VLayout(GameObject go, int padding, int spacing)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.spacing = spacing;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HLayout(GameObject go, int padding, int spacing)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.spacing = spacing;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return h;
        }
    }
}
