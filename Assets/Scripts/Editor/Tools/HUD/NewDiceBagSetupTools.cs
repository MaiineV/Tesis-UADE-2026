using System.Linq;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.DiceBag;
using Rollgeon.UI.Screens;
using Rollgeon.Upgrades.Dice.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Installer del dice bag drawer nuevo (mock "new dice bag drawer"): reconstruye
    /// la card del dado (solo sprite, sin marco), crea el prefab de fila del acordeón
    /// de encantamientos y rearma el subtree <c>DiceBagDrawer</c> de
    /// <c>Canvas_PlayerStatus</c>. Idempotente — reejecutar actualiza sin duplicar.
    /// </summary>
    /// <remarks>
    /// Conserva el <see cref="SlidingDrawer"/> (el onClick del DiceBagIcon apunta a
    /// ese componente) y las bandas ya autoradas a mano (Header NewUI_18, DiceSection
    /// NewUI_16, FacesSection NewUI_20) — solo agrega/reemplaza lo que falta. No
    /// renombrar <c>DiceBagIcon</c>: <c>CharacterFrameController</c> lo busca por nombre.
    /// </remarks>
    public static class NewDiceBagSetupTools
    {
        private const string PlayerStatusPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_PlayerStatus.prefab";
        private const string DieCardPrefabPath = "Assets/Prefabs/UI/DiceBagDieCard.prefab";
        private const string EnchantRowPrefabPath = "Assets/Prefabs/UI/DiceBagEnchantRow.prefab";
        private const string FaceCardPrefabPath = "Assets/Rollgeon/Upgrades/Dice/Prefabs/EnchantmentFaceCard.prefab";
        private const string DiceUiSettingsPath = "Assets/Rollgeon/Services/DiceBuildUiSettings.asset";

        private const string NewUiSheetPath = "Assets/Art/UI/NewUI.png";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";

        private const string RowHeaderSlice = "NewUI_15";    // barra del listado
        private const string RowBodySlice = "NewUI_4";       // panel de la descripción

        private const float RowHeaderPpuMultiplier = 0.3f;
        private const float RowBodyPpuMultiplier = 0.4f;

        // Rect del texto de la descripción (pedido de playtest 28/08).
        private const float BodyTextLeft = 35f;
        private const float BodyTextTop = 20f;
        private const float BodyTextRight = 35f;
        private const float BodyTextBottom = 20f;

        // Lista con scroll: si las filas + una descripción abierta superan el alto
        // del viewport, aparece la scrollbar.
        private const float EnchantListWidth = 450f;
        private const float EnchantListPosY = -430f;
        private const float EnchantScrollHeight = 260f;
        private const float ScrollbarWidth = 10f;

        private const float DieCardSize = 70f;

        // Rect del contenedor Dice dentro de DiceSection (pedido de playtest 28/08).
        private const float DiceRectLeft = 70f;
        private const float DiceRectTop = 38f;
        private const float DiceRectRight = 76f;
        private const float DiceRectBottom = 35f;

        // Rect del contenedor Faces dentro de FacesSection (pedido de playtest 28/08).
        private const float FacesRectLeft = 60f;
        private const float FacesRectTop = 30f;
        private const float FacesRectRight = 60f;
        private const float FacesRectBottom = 30f;
        private const float RowHeaderHeight = 44f;
        private const float RowBodyHeight = 96f;
        private const float RowTextPadding = 12f;

        [MenuItem("Rollgeon/Dice Bag Drawer/Setup New Dice Bag")]
        public static void SetupAll()
        {
            CreateDieCardPrefab();
            CreateEnchantRowPrefab();
            SetupDrawer();
        }

        // ================================================================
        // 1 - Card del dado: solo el sprite, clickeable
        // ================================================================

        public static void CreateDieCardPrefab()
        {
            RebuildPrefab(DieCardPrefabPath, "DiceBagDieCard", root =>
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = Vector2.one * DieCardSize;

                // Estructura vieja (marco + contadores) que el mock elimina.
                DestroyChildIfPresent(rootRect, "Frame");
                DestroyChildIfPresent(rootRect, "Slots");

                var icon = Ensure<Image>(root);
                icon.preserveAspect = true;
                icon.raycastTarget = true;

                var button = Ensure<Button>(root);
                button.transition = Selectable.Transition.None;

                // Número de caras centrado sobre el sprite, en negro — como en la mesa
                // de encantamientos.
                var faceCount = EnsureChildRect(rootRect, "FaceCount", Vector2.zero, Vector2.zero);
                Stretch(faceCount, 4f, 2f);
                var faceCountLabel = EnsureLabel(faceCount.gameObject,
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), 26f,
                    TextAlignmentOptions.Center);
                faceCountLabel.color = Color.black;

                var view = Ensure<DiceBagDieCardView>(root);
                var so = new SerializedObject(view);
                so.FindProperty("_diceIcon").objectReferenceValue = icon;
                so.FindProperty("_faceCountLabel").objectReferenceValue = faceCountLabel;
                so.FindProperty("_button").objectReferenceValue = button;
                so.ApplyModifiedPropertiesWithoutUndo();

                // El contenedor es un HorizontalLayoutGroup sin control de tamaño:
                // el LayoutElement fija la celda.
                var layout = Ensure<LayoutElement>(root);
                layout.preferredWidth = DieCardSize;
                layout.preferredHeight = DieCardSize;
            });
        }

        // ================================================================
        // 2 - Fila del acordeón de encantamientos
        // ================================================================

        public static void CreateEnchantRowPrefab()
        {
            var headerSprite = LoadSpriteOrError(NewUiSheetPath, RowHeaderSlice);
            var bodySprite = LoadSpriteOrError(NewUiSheetPath, RowBodySlice);
            if (headerSprite == null || bodySprite == null) return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            RebuildPrefab(EnchantRowPrefabPath, "DiceBagEnchantRow", root =>
            {
                var rootRect = (RectTransform)root.transform;

                // El alto de la fila lo computa su propio VLG (header + descripción si
                // está activa) y lo consume el VLG del EnchantList — sin ContentSizeFitter.
                var vlg = Ensure<VerticalLayoutGroup>(root);
                vlg.spacing = 4f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // -- Header clickeable: "Nombre - Tipo" sobre la barra --
                var header = EnsureChildRect(rootRect, "Header", Vector2.zero,
                    new Vector2(0f, RowHeaderHeight));
                var headerImage = Ensure<Image>(header.gameObject);
                headerImage.sprite = headerSprite;
                headerImage.type = Image.Type.Sliced;
                headerImage.pixelsPerUnitMultiplier = RowHeaderPpuMultiplier;
                headerImage.raycastTarget = true;
                var headerButton = Ensure<Button>(header.gameObject);
                headerButton.transition = Selectable.Transition.None;
                var headerLayout = Ensure<LayoutElement>(header.gameObject);
                headerLayout.preferredHeight = RowHeaderHeight;

                var headerText = EnsureChildRect(header, "Label", Vector2.zero, Vector2.zero);
                headerText.anchorMin = Vector2.zero;
                headerText.anchorMax = Vector2.one;
                headerText.offsetMin = new Vector2(RowTextPadding, 4f);
                headerText.offsetMax = new Vector2(-RowTextPadding, -2f);
                var headerLabel = EnsureLabel(headerText.gameObject, font, 24f, TextAlignmentOptions.Left);

                // -- Descripción expandible --
                var body = EnsureChildRect(rootRect, "DescriptionPanel", Vector2.zero,
                    new Vector2(0f, RowBodyHeight));
                var bodyImage = Ensure<Image>(body.gameObject);
                bodyImage.sprite = bodySprite;
                bodyImage.type = Image.Type.Sliced;
                bodyImage.pixelsPerUnitMultiplier = RowBodyPpuMultiplier;
                bodyImage.raycastTarget = false;
                var bodyLayout = Ensure<LayoutElement>(body.gameObject);
                bodyLayout.preferredHeight = RowBodyHeight;

                var bodyText = EnsureChildRect(body, "Text", Vector2.zero, Vector2.zero);
                bodyText.anchorMin = Vector2.zero;
                bodyText.anchorMax = Vector2.one;
                bodyText.offsetMin = new Vector2(BodyTextLeft, BodyTextBottom);
                bodyText.offsetMax = new Vector2(-BodyTextRight, -BodyTextTop);
                var bodyLabel = EnsureLabel(bodyText.gameObject, font, 20f, TextAlignmentOptions.TopLeft);
                bodyLabel.textWrappingMode = TextWrappingModes.Normal;
                bodyLabel.color = Color.black;

                body.gameObject.SetActive(false);

                var view = Ensure<DiceBagEnchantRowView>(root);
                var so = new SerializedObject(view);
                so.FindProperty("_headerLabel").objectReferenceValue = headerLabel;
                so.FindProperty("_headerButton").objectReferenceValue = headerButton;
                so.FindProperty("_descriptionPanel").objectReferenceValue = body.gameObject;
                so.FindProperty("_descriptionLabel").objectReferenceValue = bodyLabel;
                so.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        // ================================================================
        // 3 - Panel dentro del DiceBagDrawer existente
        // ================================================================

        public static void SetupDrawer()
        {
            var dieCard = AssetDatabase.LoadAssetAtPath<GameObject>(DieCardPrefabPath)
                ?.GetComponent<DiceBagDieCardView>();
            var enchantRow = AssetDatabase.LoadAssetAtPath<GameObject>(EnchantRowPrefabPath)
                ?.GetComponent<DiceBagEnchantRowView>();
            var faceCard = AssetDatabase.LoadAssetAtPath<GameObject>(FaceCardPrefabPath)
                ?.GetComponent<EnchantmentFaceCardView>();
            var diceSettings = AssetDatabase.LoadAssetAtPath<DiceBuildUiSettingsSO>(DiceUiSettingsPath);
            if (dieCard == null || enchantRow == null)
            {
                Debug.LogError("[NewDiceBag] Faltan los prefabs de card/fila — correr primero los pasos 1 y 2.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = PrefabUtility.LoadPrefabContents(PlayerStatusPrefabPath);
            try
            {
                var drawerTransform = FindDeep(root.transform, "DiceBagDrawer");
                var panel = drawerTransform != null ? drawerTransform.Find("Panel") as RectTransform : null;
                if (panel == null)
                {
                    Debug.LogError("[NewDiceBag] DiceBagDrawer/Panel no encontrado — abortando sin guardar.");
                    return;
                }

                // Las bandas autoradas a mano (Header/DiceSection/FacesSection) no se tocan.
                var diceContainer = panel.Find("DiceSection/Dice") as RectTransform;
                var facesContainer = panel.Find("FacesSection/Faces") as RectTransform;
                var titleLabel = panel.Find("Header/Title")?.GetComponent<TextMeshProUGUI>();
                if (diceContainer == null || facesContainer == null)
                {
                    Debug.LogError("[NewDiceBag] DiceSection/Dice o FacesSection/Faces no encontrados — abortando sin guardar.");
                    return;
                }

                // Rect pedido de playtest: stretch dentro de DiceSection con márgenes fijos.
                diceContainer.anchorMin = Vector2.zero;
                diceContainer.anchorMax = Vector2.one;
                diceContainer.offsetMin = new Vector2(DiceRectLeft, DiceRectBottom);
                diceContainer.offsetMax = new Vector2(-DiceRectRight, -DiceRectTop);

                // Rect pedido de playtest (28/08): stretch con márgenes fijos.
                facesContainer.anchorMin = Vector2.zero;
                facesContainer.anchorMax = Vector2.one;
                facesContainer.offsetMin = new Vector2(FacesRectLeft, FacesRectBottom);
                facesContainer.offsetMax = new Vector2(-FacesRectRight, -FacesRectTop);

                // El tamaño de celda lo escribe la view con DiceBagFaceLayout; el
                // constraint queda Flexible para que un d20 wrapee a otra fila cuando
                // ni la celda mínima entra en una sola.
                if (facesContainer.TryGetComponent<GridLayoutGroup>(out var facesGrid))
                {
                    facesGrid.constraint = GridLayoutGroup.Constraint.Flexible;
                    facesGrid.childAlignment = TextAnchor.MiddleCenter;
                }

                // El acordeón reemplaza al box de descripción viejo. Estructura con
                // scroll: si las filas + una descripción abierta superan el alto del
                // viewport, aparece la scrollbar.
                DestroyChildIfPresent(panel, "Description");
                DestroyChildIfPresent(panel, "EnchantList"); // versión pre-scroll

                var scroll = EnsureChildRect(panel, "EnchantScroll",
                    new Vector2(0f, EnchantListPosY), new Vector2(EnchantListWidth, EnchantScrollHeight));
                scroll.anchorMin = scroll.anchorMax = new Vector2(0.5f, 1f);
                scroll.pivot = new Vector2(0.5f, 1f);
                scroll.anchoredPosition = new Vector2(0f, EnchantListPosY);
                scroll.sizeDelta = new Vector2(EnchantListWidth, EnchantScrollHeight);

                var viewport = EnsureChildRect(scroll, "Viewport", Vector2.zero, Vector2.zero);
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = new Vector2(-(ScrollbarWidth + 4f), 0f);
                viewport.pivot = new Vector2(0f, 1f);
                Ensure<RectMask2D>(viewport.gameObject);

                var list = EnsureChildRect(viewport, "EnchantList", Vector2.zero, Vector2.zero);
                list.anchorMin = new Vector2(0f, 1f);
                list.anchorMax = new Vector2(1f, 1f);
                list.pivot = new Vector2(0.5f, 1f);
                list.anchoredPosition = Vector2.zero;
                list.sizeDelta = new Vector2(0f, 0f);
                var listVlg = Ensure<VerticalLayoutGroup>(list.gameObject);
                listVlg.spacing = 8f;
                listVlg.childAlignment = TextAnchor.UpperCenter;
                listVlg.childControlWidth = true;
                listVlg.childControlHeight = true;
                listVlg.childForceExpandWidth = true;
                listVlg.childForceExpandHeight = false;
                var listFitter = Ensure<ContentSizeFitter>(list.gameObject);
                listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                listFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                var noEnch = EnsureChildRect(list, "NoEnchantments", Vector2.zero, new Vector2(0f, 32f));
                var noEnchLabel = EnsureLabel(noEnch.gameObject, font, 22f, TextAlignmentOptions.Center);
                var noEnchLayout = Ensure<LayoutElement>(noEnch.gameObject);
                noEnchLayout.preferredHeight = 32f;

                // Scrollbar vertical al borde derecho — colores del panel genérico de la paleta.
                var scrollbarRect = EnsureChildRect(scroll, "Scrollbar", Vector2.zero, Vector2.zero);
                scrollbarRect.anchorMin = new Vector2(1f, 0f);
                scrollbarRect.anchorMax = Vector2.one;
                scrollbarRect.pivot = new Vector2(1f, 0.5f);
                scrollbarRect.anchoredPosition = Vector2.zero;
                scrollbarRect.sizeDelta = new Vector2(ScrollbarWidth, 0f);
                var scrollbarBg = Ensure<Image>(scrollbarRect.gameObject);
                scrollbarBg.color = new Color32(0x1F, 0x23, 0x2E, 0xFF);
                var handleArea = EnsureChildRect(scrollbarRect, "SlidingArea", Vector2.zero, Vector2.zero);
                Stretch(handleArea, 1f, 1f);
                var handle = EnsureChildRect(handleArea, "Handle", Vector2.zero, Vector2.zero);
                var handleImage = Ensure<Image>(handle.gameObject);
                handleImage.color = new Color32(0x5F, 0x73, 0x7A, 0xFF);
                var scrollbar = Ensure<Scrollbar>(scrollbarRect.gameObject);
                scrollbar.handleRect = handle;
                scrollbar.direction = Scrollbar.Direction.BottomToTop;

                var scrollRect = Ensure<ScrollRect>(scroll.gameObject);
                scrollRect.content = list;
                scrollRect.viewport = viewport;
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 20f;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

                var view = Ensure<DiceBagView>(drawerTransform.gameObject);
                var so = new SerializedObject(view);
                so.FindProperty("_diceContainer").objectReferenceValue = diceContainer;
                so.FindProperty("_dieCardPrefab").objectReferenceValue = dieCard;
                so.FindProperty("_facesContainer").objectReferenceValue = facesContainer;
                if (faceCard != null)
                    so.FindProperty("_faceCardPrefab").objectReferenceValue = faceCard;
                so.FindProperty("_enchantListContainer").objectReferenceValue = list;
                so.FindProperty("_enchantRowPrefab").objectReferenceValue = enchantRow;
                so.FindProperty("_noEnchantmentsLabel").objectReferenceValue = noEnchLabel;
                if (titleLabel != null)
                    so.FindProperty("_titleLabel").objectReferenceValue = titleLabel;
                if (diceSettings != null)
                    so.FindProperty("_diceUiSettings").objectReferenceValue = diceSettings;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerStatusPrefabPath);
                Debug.Log("[NewDiceBag] Drawer rearmado en Canvas_PlayerStatus.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static void DestroyChildIfPresent(RectTransform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
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
                Debug.Log($"[NewDiceBag] Prefab actualizado: {path}");
                return;
            }

            var go = new GameObject(rootName, typeof(RectTransform));
            try
            {
                build(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Debug.Log($"[NewDiceBag] Prefab creado: {path}");
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

        private static void Stretch(RectTransform rect, float paddingX, float paddingY)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(paddingX, paddingY);
            rect.offsetMax = new Vector2(-paddingX, -paddingY);
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
                Debug.LogError($"[NewDiceBag] Slice '{spriteName}' no encontrado en {assetPath}.");
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
