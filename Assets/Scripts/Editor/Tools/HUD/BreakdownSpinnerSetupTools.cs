using System.Linq;
using Rollgeon.UI.HUD.Breakdown;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Installer del spinner de modificadores globales: reconstruye el subárbol
    /// del GO <c>GlobalModifierCascade</c> en <c>Canvas_ActionRoll.prefab</c>
    /// (recuadro FrameAnim + tambor de dos slots con RectMask2D) y recablea
    /// <see cref="GlobalModifierSpinnerView"/>. Idempotente — reejecutar
    /// reconstruye sin duplicar. La ref del director sobrevive (mismo GO/componente).
    /// </summary>
    public static class BreakdownSpinnerSetupTools
    {
        private const string LogPrefix = "[BreakdownSpinnerSetup] ";
        private const string PrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_ActionRoll.prefab";
        private const string FrameSheetPath = "Assets/Art/UI/Frame/FrameAnim.png";
        private const string FallbackIconPath = "Assets/Art/UI/Inventory/ItemSlot.png";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";
        private const string HostName = "GlobalModifierCascade";

        // Frame fuente 128x72 / interior 98x42, escala x2 entera (pixel-art crisp).
        private static readonly Vector2 FrameSize = new Vector2(256f, 144f);
        private static readonly Vector2 InteriorSize = new Vector2(196f, 84f);
        private static readonly Vector2 SlotSize = new Vector2(190f, 56f);
        private static readonly Vector2 IconSize = new Vector2(44f, 44f);
        private static readonly Vector2 IconPos = new Vector2(-68f, 0f);
        private static readonly Vector2 LabelSize = new Vector2(132f, 48f);
        private static readonly Vector2 LabelPos = new Vector2(22f, 0f);

        [MenuItem("Rollgeon/Breakdown/Setup Spinner (All)")]
        public static void SetupAll()
        {
            RebuildSpinner();
        }

        [MenuItem("Rollgeon/Breakdown/1 - Rebuild Spinner In Canvas_ActionRoll")]
        public static void RebuildSpinner()
        {
            var frameSprite = LoadSpriteOrError(FrameSheetPath, "FrameAnim_0");
            var interiorSprite = LoadSpriteOrError(FrameSheetPath, "FrameAnim_1");
            if (frameSprite == null || interiorSprite == null) return;

            var fallbackIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FallbackIconPath);
            if (fallbackIcon == null)
                Debug.LogWarning(LogPrefix + $"Fallback icon no encontrado en {FallbackIconPath} — queda sin asignar.");

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var host = root.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(t => t.name == HostName);
                if (host == null)
                {
                    Debug.LogError(LogPrefix + $"GO '{HostName}' no encontrado en {PrefabPath}.");
                    return;
                }

                var view = host.GetComponent<GlobalModifierSpinnerView>();
                if (view == null)
                {
                    Debug.LogError(LogPrefix + "GlobalModifierSpinnerView no está en el host — " +
                                   "¿compiló el rename de la view?");
                    return;
                }

                // -- Subárbol viejo (cascade) y corridas previas: afuera --
                DestroyChildIfExists(host, "Backplate");
                DestroyChildIfExists(host, "EntriesRoot");
                DestroyChildIfExists(host, "Interior");
                DestroyChildIfExists(host, "SlotsRoot");
                DestroyChildIfExists(host, "Frame");

                // El host conserva anchor bottom-right / pivot (1,0) / pos (-40,260).
                host.sizeDelta = FrameSize;

                // -- Orden de hijos = orden de dibujo: Interior, SlotsRoot, Frame --
                var interior = CreateChild(host, "Interior", Vector2.zero, InteriorSize);
                var interiorImage = interior.gameObject.AddComponent<Image>();
                interiorImage.sprite = interiorSprite;
                interiorImage.raycastTarget = false;

                var slotsRoot = CreateChild(host, "SlotsRoot", Vector2.zero, InteriorSize);
                slotsRoot.gameObject.AddComponent<RectMask2D>();

                var slotA = CreateSlot(slotsRoot, "SlotA", active: true);
                var slotB = CreateSlot(slotsRoot, "SlotB", active: false);

                var frame = CreateChild(host, "Frame", Vector2.zero, FrameSize);
                var frameImage = frame.gameObject.AddComponent<Image>();
                frameImage.sprite = frameSprite;
                frameImage.raycastTarget = false;

                // -- Wiring de la view --
                var so = new SerializedObject(view);
                so.FindProperty("_slotA").objectReferenceValue = slotA;
                so.FindProperty("_slotB").objectReferenceValue = slotB;
                so.FindProperty("_slotsRoot").objectReferenceValue = slotsRoot;
                so.FindProperty("_fallbackIcon").objectReferenceValue = fallbackIcon;
                so.FindProperty("_group").objectReferenceValue = host.GetComponent<CanvasGroup>();
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log(LogPrefix + "Spinner reconstruido en Canvas_ActionRoll (frame 256x144, " +
                          "tambor 2 slots). Si FrameAnim_1 no calza centrado dentro del borde, " +
                          "ajustar el offset de 'Interior' a mano una única vez.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ModifierEntryView CreateSlot(RectTransform parent, string name, bool active)
        {
            var slot = CreateChild(parent, name, Vector2.zero, SlotSize);

            var icon = CreateChild(slot, "Icon", IconPos, IconSize);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            var labelRect = CreateChild(slot, "Label", LabelPos, LabelSize);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 28f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 28f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.richText = true;
            label.raycastTarget = false;
            label.text = "+0";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            else Debug.LogWarning(LogPrefix + $"Fuente no encontrada en {FontPath} — Label con default TMP.");

            var view = slot.gameObject.AddComponent<ModifierEntryView>();
            var so = new SerializedObject(view);
            so.FindProperty("_icon").objectReferenceValue = iconImage;
            so.FindProperty("_label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            slot.gameObject.SetActive(active);
            return view;
        }

        private static RectTransform CreateChild(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        private static void DestroyChildIfExists(RectTransform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static Sprite LoadSpriteOrError(string assetPath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogError(LogPrefix + $"Slice '{spriteName}' no encontrado en {assetPath}. Abortando.");
            return sprite;
        }
    }
}
