using System.Linq;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.Inventory;
using Rollgeon.UI.Tooltips;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Installer del inventario nuevo (mock "new inventory drawer"): reconstruye el
    /// prefab de la celda rombo y rearma el panel dentro del <c>InventoryDrawer</c>
    /// existente de <c>Canvas_PlayerStatus</c> con el arte de NewUI. Idempotente —
    /// reejecutar actualiza sin duplicar.
    /// </summary>
    /// <remarks>
    /// Conserva el GameObject <c>InventoryDrawer</c> y su <see cref="SlidingDrawer"/>
    /// (el onClick del BackpackIcon apunta a ese componente) y no toca nada fuera de
    /// ese subtree. El grid no usa LayoutGroup: las celdas las posiciona
    /// <see cref="InventoryDrawerView.Rebuild"/> con <see cref="InventoryDiamondLayout"/>.
    /// </remarks>
    public static class NewInventorySetupTools
    {
        private const string PlayerStatusPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_PlayerStatus.prefab";
        private const string SlotPrefabPath = "Assets/Prefabs/UI/InventoryItemSlot.prefab";

        private const string NewUiSheetPath = "Assets/Art/UI/NewUI.png";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";

        private const string PanelSlice = "NewUI_2";        // fondo violeta, tiled
        private const string FilledSlice = "NewUI_5";       // rombo con item
        private const string EmptySlice = "NewUI_6";        // rombo vacío
        private const string FilledHoverSlice = "NewUI_8";  // con item + hover
        private const string EmptyHoverSlice = "NewUI_11";  // vacío + hover
        private const string TitlePlaqueSlice = "NewUI_18"; // placa del título

        private const float PanelTiledPpuMultiplier = 0.4f;
        private const float TitlePlaquePpuMultiplier = 0.3f;

        // Placa 131×26 a 3× (pixel art, mismo factor que los rombos).
        private static readonly Vector2 TitlePlaqueSize = new Vector2(393f, InventoryDiamondLayout.TitleHeight);
        // Ícono centrado en el rombo: 44 px deja aire contra los bordes del diamante.
        private static readonly Vector2 ItemIconSize = new Vector2(44f, 44f);

        private static readonly Vector2 PanelSize = new Vector2(
            InventoryDiamondLayout.PanelWidth,
            InventoryDiamondLayout.PanelHeight(InventoryDiamondLayout.MinCells / InventoryDiamondLayout.Cols));

        private const float PanelOpenX = 24f;
        private static readonly float PanelClosedX = -(PanelSize.x + 40f);
        // -168 y no -136: el top del panel pisaba los últimos ~16px del marco del
        // personaje (cluster en y-24 + marco de 128) — pedido de playtest del 03/09.
        private const float PanelTopY = -168f;

        [MenuItem("Rollgeon/Inventory Drawer/Setup New Inventory")]
        public static void SetupAll()
        {
            CreateSlotPrefab();
            SetupDrawer();
        }

        // ================================================================
        // 1 - Celda rombo
        // ================================================================

        public static void CreateSlotPrefab()
        {
            var filled = LoadSpriteOrError(NewUiSheetPath, FilledSlice);
            var empty = LoadSpriteOrError(NewUiSheetPath, EmptySlice);
            var filledHover = LoadSpriteOrError(NewUiSheetPath, FilledHoverSlice);
            var emptyHover = LoadSpriteOrError(NewUiSheetPath, EmptyHoverSlice);
            if (filled == null || empty == null || filledHover == null || emptyHover == null) return;

            RebuildPrefab(SlotPrefabPath, "InventoryItemSlot", root =>
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = Vector2.one * InventoryDiamondLayout.DiamondSize;

                // Restos del slot viejo (grilla cuadrada UI-sheet).
                DestroyChildIfPresent(rootRect, "Base");
                RemoveComponentIfPresent<LayoutElement>(root);

                // Único raycast target de la celda: le da el hover al tooltip trigger y
                // al swap de sprites de la view.
                var cellImage = Ensure<Image>(root);
                cellImage.sprite = empty;
                cellImage.type = Image.Type.Simple;
                cellImage.preserveAspect = true;
                cellImage.raycastTarget = true;

                var iconRect = EnsureChildRect(rootRect, "ItemIcon", Vector2.zero, ItemIconSize);
                Center(iconRect);
                var iconImage = Ensure<Image>(iconRect.gameObject);
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                iconImage.enabled = false;

                var tooltip = Ensure<UITooltipTrigger>(root);

                var view = Ensure<InventoryItemSlotView>(root);
                var so = new SerializedObject(view);
                so.FindProperty("_cellBg").objectReferenceValue = cellImage;
                so.FindProperty("_icon").objectReferenceValue = iconImage;
                so.FindProperty("_tooltip").objectReferenceValue = tooltip;
                so.FindProperty("_filledSprite").objectReferenceValue = filled;
                so.FindProperty("_emptySprite").objectReferenceValue = empty;
                so.FindProperty("_filledHoverSprite").objectReferenceValue = filledHover;
                so.FindProperty("_emptyHoverSprite").objectReferenceValue = emptyHover;
                so.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        // ================================================================
        // 2 - Panel dentro del InventoryDrawer existente
        // ================================================================

        public static void SetupDrawer()
        {
            var panelSprite = LoadSpriteOrError(NewUiSheetPath, PanelSlice);
            var plaqueSprite = LoadSpriteOrError(NewUiSheetPath, TitlePlaqueSlice);
            if (panelSprite == null || plaqueSprite == null) return;

            var slot = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath)?.GetComponent<InventoryItemSlotView>();
            if (slot == null)
            {
                Debug.LogError("[NewInventory] InventoryItemSlot.prefab sin InventoryItemSlotView — correr primero el paso 1.");
                return;
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = PrefabUtility.LoadPrefabContents(PlayerStatusPrefabPath);
            try
            {
                var drawerTransform = FindDeep(root.transform, "InventoryDrawer");
                if (drawerTransform == null)
                {
                    Debug.LogError("[NewInventory] InventoryDrawer no encontrado en Canvas_PlayerStatus — " +
                                   "el BackpackIcon quedaría sin destino. Abortando sin guardar.");
                    return;
                }
                var drawerRect = (RectTransform)drawerTransform;

                var panel = drawerRect.Find("Panel") as RectTransform;
                if (panel == null)
                {
                    Debug.LogError("[NewInventory] Panel no encontrado dentro de InventoryDrawer. Abortando sin guardar.");
                    return;
                }

                panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
                panel.pivot = new Vector2(0f, 1f);
                panel.anchoredPosition = new Vector2(PanelClosedX, PanelTopY);
                panel.sizeDelta = PanelSize;
                var panelImage = Ensure<Image>(panel.gameObject);
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Tiled;
                panelImage.pixelsPerUnitMultiplier = PanelTiledPpuMultiplier;
                panelImage.raycastTarget = true;

                // Secciones del layout viejo que el diseño nuevo no tiene.
                DestroyChildIfPresent(panel, "ItemsCaption");
                DestroyChildIfPresent(panel, "ItemsBox");
                DestroyChildIfPresent(panel, "Title");

                // -- Placa del título --
                var plaque = EnsureChildRect(panel, "TitlePlaque",
                    new Vector2(0f, -InventoryDiamondLayout.PanelPadding), TitlePlaqueSize);
                plaque.anchorMin = plaque.anchorMax = new Vector2(0.5f, 1f);
                plaque.pivot = new Vector2(0.5f, 1f);
                plaque.anchoredPosition = new Vector2(0f, -InventoryDiamondLayout.PanelPadding);
                plaque.sizeDelta = TitlePlaqueSize;
                var plaqueImage = Ensure<Image>(plaque.gameObject);
                plaqueImage.sprite = plaqueSprite;
                plaqueImage.type = Image.Type.Sliced;
                plaqueImage.pixelsPerUnitMultiplier = TitlePlaquePpuMultiplier;
                plaqueImage.raycastTarget = false;

                var title = EnsureChildRect(plaque, "Title", Vector2.zero, Vector2.zero);
                Stretch(title, 8f);
                var titleLabel = EnsureLabel(title.gameObject, font, 34f, TextAlignmentOptions.Center);
                titleLabel.text = "Inventario";

                // -- Grid (sin LayoutGroup: la view posiciona con InventoryDiamondLayout) --
                // Anclada al centro-arriba del panel: queda centrada horizontal sin
                // importar cuánto crezca el padding del panel.
                float gridTop = InventoryDiamondLayout.PanelPadding
                                + InventoryDiamondLayout.TitleHeight
                                + InventoryDiamondLayout.SectionGap;
                var grid = EnsureChildRect(panel, "Grid",
                    new Vector2(-InventoryDiamondLayout.GridWidth / 2f, -gridTop),
                    new Vector2(InventoryDiamondLayout.GridWidth, InventoryDiamondLayout.GridHeight(
                        InventoryDiamondLayout.MinCells / InventoryDiamondLayout.Cols)));
                grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 1f);
                grid.pivot = new Vector2(0f, 1f);
                grid.anchoredPosition = new Vector2(-InventoryDiamondLayout.GridWidth / 2f, -gridTop);
                RemoveComponentIfPresent<GridLayoutGroup>(grid.gameObject);

                // Celdas viejas instanciadas dentro del grid quedarían con el prefab
                // nuevo igual, pero limpiarlas evita arrastrar wiring viejo.
                for (int i = grid.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(grid.GetChild(i).gameObject);

                var slider = Ensure<SlidingDrawer>(drawerRect.gameObject);
                var sliderSo = new SerializedObject(slider);
                sliderSo.FindProperty("_panel").objectReferenceValue = panel;
                sliderSo.FindProperty("_closedX").floatValue = PanelClosedX;
                sliderSo.FindProperty("_openX").floatValue = PanelOpenX;
                sliderSo.ApplyModifiedPropertiesWithoutUndo();

                var view = Ensure<InventoryDrawerView>(drawerRect.gameObject);
                var so = new SerializedObject(view);
                so.FindProperty("_panel").objectReferenceValue = panel;
                so.FindProperty("_grid").objectReferenceValue = grid;
                so.FindProperty("_slotPrefab").objectReferenceValue = slot;
                so.FindProperty("_titleLabel").objectReferenceValue = titleLabel;
                so.ApplyModifiedPropertiesWithoutUndo();

                WireBackpackIcon(root, slider);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerStatusPrefabPath);
                Debug.Log("[NewInventory] Panel rombo armado en Canvas_PlayerStatus.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Reengancha el toggle por si el drawer viejo dejó el listener apuntando a un
        // componente destruido. No renombrar 'BackpackIcon': CharacterFrameController
        // lo busca por nombre.
        private static void WireBackpackIcon(GameObject root, SlidingDrawer drawer)
        {
            var icon = FindDeep(root.transform, "BackpackIcon");
            if (icon == null)
            {
                Debug.LogWarning("[NewInventory] BackpackIcon no encontrado — el panel queda sin disparador.");
                return;
            }

            var button = Ensure<Button>(icon.gameObject);
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddVoidPersistentListener(button.onClick, drawer.Toggle);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static void DestroyChildIfPresent(RectTransform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static void RemoveComponentIfPresent<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var comp)) Object.DestroyImmediate(comp);
        }

        private static void RebuildPrefab(string path, string rootName, System.Action<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    build(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                Debug.Log($"[NewInventory] Prefab actualizado: {path}");
                return;
            }

            var go = new GameObject(rootName, typeof(RectTransform));
            try
            {
                build(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Debug.Log($"[NewInventory] Prefab creado: {path}");
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static TextMeshProUGUI EnsureLabel(GameObject go, TMP_FontAsset font, float size,
            TextAlignmentOptions alignment)
        {
            var label = Ensure<TextMeshProUGUI>(go);
            if (font != null) label.font = font;
            label.fontSize = size;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static void Center(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static Sprite LoadSpriteOrError(string assetPath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogError($"[NewInventory] Slice '{spriteName}' no encontrado en {assetPath}.");
            return sprite;
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
    }
}
