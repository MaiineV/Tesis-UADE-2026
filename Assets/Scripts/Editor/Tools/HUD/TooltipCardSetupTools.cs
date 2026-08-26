using System.IO;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Autora el prefab de una tarjeta del tooltip y le cuelga la columna al panel.
    /// </summary>
    /// <remarks>
    /// Idempotente: correrlo dos veces deja el mismo resultado. El ancho de la tarjeta es el mismo
    /// que el del texto del panel para que el tooltip no cambie de ancho entre uno de texto y uno
    /// de tarjetas.
    /// </remarks>
    public static class TooltipCardSetupTools
    {
        private const string CardPrefabPath = "Assets/Prefabs/UI/TooltipCard.prefab";
        private const string TooltipPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_Tooltip.prefab";
        private const string PanelSpriteGuid = "cca52ed63b2fdae4ca26627a5c6beed8";

        private const float CardWidth = 300f;
        private const float IconSize = 40f;
        private const float BadgeSize = 18f;

        [MenuItem("Rollgeon/Tooltips/1 - Author Tooltip Card Prefab")]
        public static void AuthorCardPrefab()
        {
            var root = new GameObject("TooltipCard", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(CardWidth, IconSize + 16f);

            var background = Ensure<Image>(root);
            background.sprite = LoadPanelSprite();
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;

            var layout = Ensure<HorizontalLayoutGroup>(root);
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var rootLayout = Ensure<LayoutElement>(root);
            rootLayout.preferredWidth = CardWidth;

            // Ícono + su badge. El badge queda fuera del layout para poder pisar la esquina.
            var iconRect = EnsureChildRect(rootRect, "Icon", Vector2.zero, new Vector2(IconSize, IconSize));
            var iconLayout = Ensure<LayoutElement>(iconRect.gameObject);
            iconLayout.preferredWidth = IconSize;
            iconLayout.preferredHeight = IconSize;
            var iconImage = Ensure<Image>(iconRect.gameObject);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var badgeRect = EnsureChildRect(iconRect, "Badge", Vector2.zero, new Vector2(BadgeSize, BadgeSize));
            badgeRect.anchorMin = new Vector2(1f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            Ensure<LayoutElement>(badgeRect.gameObject).ignoreLayout = true;
            var badgeImage = Ensure<Image>(badgeRect.gameObject);
            badgeImage.raycastTarget = false;
            var badgeLabel = EnsureLabel(badgeRect, "Value", 12f, TextAlignmentOptions.Center);

            // Cuerpo: título y regla, uno debajo del otro.
            var bodyRect = EnsureChildRect(rootRect, "Body", Vector2.zero, Vector2.zero);
            var bodyLayout = Ensure<VerticalLayoutGroup>(bodyRect.gameObject);
            bodyLayout.spacing = 2;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;
            Ensure<LayoutElement>(bodyRect.gameObject).flexibleWidth = 1f;

            var titleLabel = EnsureLabel(bodyRect, "Title", 16f, TextAlignmentOptions.Left);
            titleLabel.fontStyle = FontStyles.Bold;
            var ruleLabel = EnsureLabel(bodyRect, "Rule", 13f, TextAlignmentOptions.Left);
            ruleLabel.enableWordWrapping = true;

            var view = Ensure<TooltipCardView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("_titleLabel").objectReferenceValue = titleLabel;
            so.FindProperty("_ruleLabel").objectReferenceValue = ruleLabel;
            so.FindProperty("_iconRoot").objectReferenceValue = iconRect.gameObject;
            so.FindProperty("_icon").objectReferenceValue = iconImage;
            so.FindProperty("_badge").objectReferenceValue = badgeRect.gameObject;
            so.FindProperty("_badgeLabel").objectReferenceValue = badgeLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(CardPrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TooltipCardSetupTools] Prefab de tarjeta autorado en {CardPrefabPath}.");
        }

        [MenuItem("Rollgeon/Tooltips/2 - Wire Card Column Into Tooltip Panel")]
        public static void WireCardColumn()
        {
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (cardPrefab == null)
            {
                Debug.LogError($"[TooltipCardSetupTools] Falta {CardPrefabPath}. Corré el paso 1 primero.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(TooltipPrefabPath);
            try
            {
                var controller = contents.GetComponentInChildren<TooltipController>(includeInactive: true);
                if (controller == null)
                {
                    Debug.LogError($"[TooltipCardSetupTools] {TooltipPrefabPath} no tiene TooltipController.");
                    return;
                }

                var so = new SerializedObject(controller);
                var panel = (RectTransform)so.FindProperty("_root").objectReferenceValue;
                if (panel == null)
                {
                    Debug.LogError("[TooltipCardSetupTools] El TooltipController no tiene _root cableado.");
                    return;
                }

                var cards = EnsureChildRect(panel, "Cards", Vector2.zero, Vector2.zero);
                // Después del texto: el encabezado va arriba y la columna crece abajo.
                cards.SetSiblingIndex(panel.childCount - 1);

                var layout = Ensure<VerticalLayoutGroup>(cards.gameObject);
                layout.spacing = 6;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                Ensure<LayoutElement>(cards.gameObject).preferredWidth = CardWidth;

                so.FindProperty("_cardsContainer").objectReferenceValue = cards;
                so.FindProperty("_cardPrefab").objectReferenceValue =
                    cardPrefab.GetComponent<TooltipCardView>();
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, TooltipPrefabPath);
                Debug.Log("[TooltipCardSetupTools] Columna de tarjetas cableada en el panel del tooltip.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Sprite LoadPanelSprite()
        {
            var path = AssetDatabase.GUIDToAssetPath(PanelSpriteGuid);
            if (string.IsNullOrEmpty(path)) return null;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) return sprite;
            return null;
        }

        private static TextMeshProUGUI EnsureLabel(RectTransform parent, string name, float size,
                                                   TextAlignmentOptions alignment)
        {
            var rect = EnsureChildRect(parent, name, Vector2.zero, Vector2.zero);
            var label = Ensure<TextMeshProUGUI>(rect.gameObject);
            label.fontSize = size;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform EnsureChildRect(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                rect = (RectTransform)go.transform;
                rect.SetParent(parent, worldPositionStays: false);
            }
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static T Ensure<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();

        private static T Ensure<T>(RectTransform rect) where T : Component => Ensure<T>(rect.gameObject);
    }
}
