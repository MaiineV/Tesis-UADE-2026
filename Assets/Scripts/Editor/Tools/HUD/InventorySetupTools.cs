using System.Linq;
using Rollgeon.Items;
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
    /// Installer del drawer de inventario: crea el prefab de la celda, arma el panel
    /// deslizante en <c>Canvas_PlayerStatus</c>, engancha el ícono de la mochila y asigna
    /// el sprite base a los items sin ícono. Idempotente — reejecutar actualiza sin duplicar.
    /// </summary>
    /// <remarks>
    /// Mismo gesto que el contrato y la bolsa de dados (<see cref="SlidingDrawer"/>): sin
    /// pausa, cierra por ícono / Esc / click afuera, exclusión mutua gratis. El alto
    /// inicial es de una fila; en runtime <see cref="InventoryDrawerView.Rebuild"/> crece
    /// el panel según las filas, así que acá solo se deja el estado de reposo.
    /// </remarks>
    public static class InventorySetupTools
    {
        private const string PlayerStatusPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_PlayerStatus.prefab";
        private const string SlotPrefabPath = "Assets/Prefabs/UI/InventoryItemSlot.prefab";

        private const string UiSheetPath = "Assets/Art/UI/UI-sheet.png";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";

        private const string PanelSlice = "UI-sheet_7";
        private const string BoxSlice = "UI-sheet_1";
        private const string CellSlice = "UI-sheet_2";
        private const string BaseSlice = "UI-sheet_3";

        // Layout en px de referencia (1920x1080) — los números compartidos con el runtime
        // viven en InventoryDrawerView; acá solo lo que es exclusivo del armado.
        // Base del item: UI-sheet_3 es 6x7 → x6 entero (pixel art).
        private static readonly Vector2 BaseIconSize = new Vector2(36f, 42f);

        private static readonly Vector2 PanelSize =
            new Vector2(InventoryDrawerView.PanelWidth, InventoryDrawerView.PanelHeight(1));
        private const float PanelOpenX = 24f;
        private static readonly float PanelClosedX = -(PanelSize.x + 40f);
        private const float PanelTopY = -136f;

        private static readonly float ContentWidth = InventoryDrawerView.BoxWidth;

        [MenuItem("Rollgeon/Inventory Drawer/Setup All")]
        public static void SetupAll()
        {
            CreateSlotPrefab();
            SetupDrawer();
            AssignMissingItemIcons();
        }

        // ================================================================
        // 1 - Celda del grid
        // ================================================================

        [MenuItem("Rollgeon/Inventory Drawer/1 - Create Slot Prefab")]
        public static void CreateSlotPrefab()
        {
            var cell = LoadSpriteOrError(UiSheetPath, CellSlice);
            var baseSprite = LoadSpriteOrError(UiSheetPath, BaseSlice);
            if (cell == null || baseSprite == null) return;

            RebuildPrefab(SlotPrefabPath, "InventoryItemSlot", root =>
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(InventoryDrawerView.CellSize, InventoryDrawerView.CellSize);

                // Único raycast target de la celda: le da el hover al tooltip trigger.
                var cellImage = Ensure<Image>(root);
                cellImage.sprite = cell;
                cellImage.type = Image.Type.Sliced;
                cellImage.raycastTarget = true;

                var baseRect = EnsureChildRect(rootRect, "Base", Vector2.zero, BaseIconSize);
                Center(baseRect);
                var baseImage = Ensure<Image>(baseRect.gameObject);
                baseImage.sprite = baseSprite;
                baseImage.preserveAspect = true;
                baseImage.raycastTarget = false;
                // Arranca apagada: solo la prende Bind() para un item sin sprite propio.
                baseImage.enabled = false;

                var iconRect = EnsureChildRect(rootRect, "ItemIcon", Vector2.zero, BaseIconSize);
                Center(iconRect);
                var iconImage = Ensure<Image>(iconRect.gameObject);
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                iconImage.enabled = false;

                var tooltip = Ensure<UITooltipTrigger>(root);

                var view = Ensure<InventoryItemSlotView>(root);
                var so = new SerializedObject(view);
                so.FindProperty("_cellBg").objectReferenceValue = cellImage;
                so.FindProperty("_base").objectReferenceValue = baseImage;
                so.FindProperty("_icon").objectReferenceValue = iconImage;
                so.FindProperty("_tooltip").objectReferenceValue = tooltip;
                so.ApplyModifiedPropertiesWithoutUndo();

                var layout = Ensure<LayoutElement>(root);
                layout.preferredWidth = InventoryDrawerView.CellSize;
                layout.preferredHeight = InventoryDrawerView.CellSize;
            });
        }

        // ================================================================
        // 2 - Panel en Canvas_PlayerStatus
        // ================================================================

        [MenuItem("Rollgeon/Inventory Drawer/2 - Setup Drawer")]
        public static void SetupDrawer()
        {
            var panelSprite = LoadSpriteOrError(UiSheetPath, PanelSlice);
            var boxSprite = LoadSpriteOrError(UiSheetPath, BoxSlice);
            if (panelSprite == null || boxSprite == null) return;

            var slot = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath)?.GetComponent<InventoryItemSlotView>();
            if (slot == null)
            {
                Debug.LogError("[Inventory] Correr primero el paso 1.");
                return;
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = PrefabUtility.LoadPrefabContents(PlayerStatusPrefabPath);
            try
            {
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas == null)
                {
                    Debug.LogError("[Inventory] Canvas no encontrado en Canvas_PlayerStatus.");
                    return;
                }
                var canvasRect = (RectTransform)canvas.transform;

                var drawerRect = EnsureChildRect(canvasRect, "InventoryDrawer", Vector2.zero, Vector2.zero);
                Stretch(drawerRect, 0f);
                drawerRect.SetAsLastSibling();

                var backdropRect = EnsureChildRect(drawerRect, "Backdrop", Vector2.zero, Vector2.zero);
                Stretch(backdropRect, 0f);
                var backdropImage = Ensure<Image>(backdropRect.gameObject);
                backdropImage.color = new Color(0f, 0f, 0f, 0.01f);
                backdropImage.raycastTarget = true;
                var backdropButton = Ensure<Button>(backdropRect.gameObject);
                backdropButton.transition = Selectable.Transition.None;
                backdropRect.gameObject.SetActive(false);

                var panel = EnsureChildRect(drawerRect, "Panel", new Vector2(PanelClosedX, PanelTopY), PanelSize);
                panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
                panel.pivot = new Vector2(0f, 1f);
                panel.anchoredPosition = new Vector2(PanelClosedX, PanelTopY);
                panel.sizeDelta = PanelSize;
                var panelImage = Ensure<Image>(panel.gameObject);
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
                panelImage.raycastTarget = true;

                float y = -InventoryDrawerView.PanelPadding;

                // -- Título --
                var title = EnsureSectionRect(panel, "Title", ref y, InventoryDrawerView.TitleHeight);
                var titleLabel = EnsureLabel(title.gameObject, font, 30f, TextAlignmentOptions.Center);
                titleLabel.text = "Inventario";
                y -= InventoryDrawerView.SectionGap;

                // -- Caption --
                var caption = EnsureSectionRect(panel, "ItemsCaption", ref y, InventoryDrawerView.CaptionHeight);
                var captionLabel = EnsureLabel(caption.gameObject, font, 20f, TextAlignmentOptions.Left);
                captionLabel.text = "Objetos";
                y -= InventoryDrawerView.SectionGap;

                // -- Caja de items (crece en runtime; acá queda el alto de una fila) --
                var itemsBox = EnsureSectionRect(panel, "ItemsBox", ref y, InventoryDrawerView.BoxHeight(1));
                var itemsBoxImage = Ensure<Image>(itemsBox.gameObject);
                itemsBoxImage.sprite = boxSprite;
                itemsBoxImage.type = Image.Type.Sliced;
                itemsBoxImage.raycastTarget = false;

                var grid = EnsureChildRect(itemsBox, "Grid", Vector2.zero, Vector2.zero);
                Stretch(grid, InventoryDrawerView.BoxPadding);
                var gridLayout = Ensure<GridLayoutGroup>(grid.gameObject);
                gridLayout.cellSize = new Vector2(InventoryDrawerView.CellSize, InventoryDrawerView.CellSize);
                gridLayout.spacing = new Vector2(InventoryDrawerView.CellSpacing, InventoryDrawerView.CellSpacing);
                gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
                gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
                gridLayout.childAlignment = TextAnchor.UpperLeft;
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = InventoryDrawerView.Columns;

                var slider = Ensure<SlidingDrawer>(drawerRect.gameObject);
                var sliderSo = new SerializedObject(slider);
                sliderSo.FindProperty("_panel").objectReferenceValue = panel;
                sliderSo.FindProperty("_backdrop").objectReferenceValue = backdropButton;
                sliderSo.FindProperty("_closedX").floatValue = PanelClosedX;
                sliderSo.FindProperty("_openX").floatValue = PanelOpenX;
                sliderSo.ApplyModifiedPropertiesWithoutUndo();

                var view = Ensure<InventoryDrawerView>(drawerRect.gameObject);
                var so = new SerializedObject(view);
                so.FindProperty("_panel").objectReferenceValue = panel;
                so.FindProperty("_itemsBox").objectReferenceValue = itemsBox;
                so.FindProperty("_grid").objectReferenceValue = grid;
                so.FindProperty("_slotPrefab").objectReferenceValue = slot;
                so.FindProperty("_titleLabel").objectReferenceValue = titleLabel;
                so.FindProperty("_captionLabel").objectReferenceValue = captionLabel;
                so.ApplyModifiedPropertiesWithoutUndo();

                WireBackpackIcon(root, slider);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerStatusPrefabPath);
                Debug.Log("[Inventory] Panel armado en Canvas_PlayerStatus.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void WireBackpackIcon(GameObject root, SlidingDrawer drawer)
        {
            var icon = FindDeep(root.transform, "BackpackIcon");
            if (icon == null)
            {
                Debug.LogWarning("[Inventory] BackpackIcon no encontrado — correr antes " +
                                 "'Rollgeon/Player Icons/Setup All'. El panel queda sin disparador.");
                return;
            }

            if (icon.TryGetComponent<Image>(out var image)) image.raycastTarget = true;

            var button = Ensure<Button>(icon.gameObject);
            button.transition = Selectable.Transition.None;

            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddVoidPersistentListener(button.onClick, drawer.Toggle);
        }

        // ================================================================
        // 3 - Sprite base para items sin ícono
        // ================================================================

        /// <summary>
        /// Asigna UI-sheet_3 como <c>Icon</c> a todo <see cref="ItemSO"/> que no tenga uno,
        /// para que los items tengan representación 2D en cualquier UI. Cuando haya arte
        /// propio por item, se reemplaza a mano y reejecutar esto no lo pisa.
        /// </summary>
        [MenuItem("Rollgeon/Inventory Drawer/3 - Assign Missing Item Icons")]
        public static void AssignMissingItemIcons()
        {
            var baseSprite = LoadSpriteOrError(UiSheetPath, BaseSlice);
            if (baseSprite == null) return;

            int assigned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:ItemSO", new[] { "Assets/Rollgeon" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
                if (item == null || item.Icon != null) continue;

                // Asignación por campo + SetDirty: el asset es de Odin y se reserializa
                // entero — nunca editar el YAML a mano.
                item.Icon = baseSprite;
                EditorUtility.SetDirty(item);
                assigned++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Inventory] {assigned} item(s) sin ícono ahora usan {BaseSlice}.");
        }

        // ================================================================
        // Helpers
        // ================================================================

        // Apila secciones de arriba hacia abajo llevando la Y en una sola variable — así
        // agregar o mover una sección no obliga a recalcular las de abajo a mano.
        private static RectTransform EnsureSectionRect(RectTransform panel, string name, ref float y, float height)
        {
            var rect = EnsureChildRect(panel, name, new Vector2(0f, y), new Vector2(ContentWidth, height));
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(ContentWidth, height);
            y -= height;
            return rect;
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
                Debug.Log($"[Inventory] Prefab actualizado: {path}");
                return;
            }

            var go = new GameObject(rootName, typeof(RectTransform));
            try
            {
                build(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Debug.Log($"[Inventory] Prefab creado: {path}");
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
                Debug.LogError($"[Inventory] Slice '{spriteName}' no encontrado en {assetPath}.");
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
