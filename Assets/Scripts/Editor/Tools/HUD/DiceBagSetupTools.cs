using System.Linq;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.DiceBag;
using Rollgeon.UI.Screens;
using Rollgeon.Upgrades.Dice.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Installer del panel de la bolsa de dados: crea los prefabs de la card de dado y del
    /// cupo, arma el panel deslizante en <c>Canvas_PlayerStatus</c> y engancha el ícono de
    /// la bolsa. Idempotente — reejecutar actualiza sin duplicar.
    /// </summary>
    /// <remarks>
    /// Misma estructura que la mesa de encantamientos y reusa su prefab de card de cara,
    /// pero es SOLO INFORMATIVO: sin costo, sin confirmar, sin botón cerrar (se sale con el
    /// ícono, Esc o click afuera, igual que el contrato).
    /// </remarks>
    public static class DiceBagSetupTools
    {
        private const string PlayerStatusPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_PlayerStatus.prefab";
        private const string DieCardPrefabPath = "Assets/Prefabs/UI/DiceBagDieCard.prefab";
        private const string SlotPrefabPath = "Assets/Prefabs/UI/DiceBagSlot.prefab";
        private const string FaceCardPrefabPath = "Assets/Rollgeon/Upgrades/Dice/Prefabs/EnchantmentFaceCard.prefab";
        private const string DiceUiSettingsPath = "Assets/Rollgeon/Services/DiceBuildUiSettings.asset";

        private const string UiSheetPath = "Assets/Art/UI/UI-sheet.png";
        private const string UiSheet2Path = "Assets/Art/UI/UI-Sheet-sheet.png";
        private const string SlotSheetPath = "Assets/Art/UI/EnchanmentSlot/EnchanmentSlot.png";
        private const string RuneSheetPath = "Assets/Art/UI/Rune/Rune.png";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";
        private const string EnchantMaterialPath = "Assets/Art/2D/UI/Materials/EnchantHoloUI.mat";
        private const string CursedMaterialPath = "Assets/Art/2D/UI/Materials/EnchantCurseUI.mat";

        private const string DieIdleSlice = "UI-sheet_7";
        private const string DieSelectedSlice = "UI-sheet_5";
        private const string DescriptionSlice = "UI-sheet_1";
        private const string SectionSlice = "UI-Sheet-sheet_1";
        private const string PanelSlice = "UI-Sheet-sheet_7";

        // Layout en px de referencia (1920x1080). Mismo criterio que el contrato: panel
        // acotado, colgado del borde superior, cerca del ícono que lo abre.
        private static readonly Vector2 DieCardSize = new Vector2(96f, 96f);
        private static readonly Vector2 DieCellSize = new Vector2(96f, 128f);
        private const float DiceSpacing = 10f;
        private static readonly Vector2 SlotSize = new Vector2(72f, 72f);
        private const float SlotSpacing = 12f;
        private static readonly Vector2 FaceCardSize = new Vector2(56f, 56f);
        private const float FaceSpacing = 6f;

        private const float PanelPadding = 16f;

        // La caja de dados arranca en -50 fijo (pedido de playtest) y el título se ubica
        // para entrar arriba de esa línea, no al revés.
        private const float DiceSectionTopY = 50f;
        private const float TitleTopY = 8f;
        private const float TitleHeight = 38f;
        // Más alta que la celda para que el contador de cupos, que cuelga por fuera del
        // marco, no quede pegado al borde de la sección.
        private static readonly float DiceSectionHeight = DieCellSize.y + 32f;
        private static readonly Vector2 PanelSize = new Vector2(
            5f * DieCardSize.x + 4f * DiceSpacing + 2f * PanelPadding + 24f, 620f);
        private const float PanelOpenX = 24f;
        private static readonly float PanelClosedX = -(PanelSize.x + 40f);
        private const float PanelTopY = -136f;

        private static readonly float ContentWidth = PanelSize.x - 2f * PanelPadding;

        [MenuItem("Rollgeon/Dice Bag/Setup All")]
        public static void SetupAll()
        {
            CreateDieCardPrefab();
            CreateSlotPrefab();
            SetupPanel();
        }

        // ================================================================
        // 1 - Card de dado
        // ================================================================

        [MenuItem("Rollgeon/Dice Bag/1 - Create Die Card Prefab")]
        public static void CreateDieCardPrefab()
        {
            var idle = LoadSpriteOrError(UiSheetPath, DieIdleSlice);
            var selected = LoadSpriteOrError(UiSheetPath, DieSelectedSlice);
            if (idle == null || selected == null) return;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var enchantMaterial = AssetDatabase.LoadAssetAtPath<Material>(EnchantMaterialPath);
            if (enchantMaterial == null)
                Debug.LogWarning($"[DiceBag] {EnchantMaterialPath} no encontrado — las cards no van " +
                                 "a mostrar el holo de encantado.");
            var cursedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CursedMaterialPath);
            if (cursedMaterial == null)
                Debug.LogWarning($"[DiceBag] {CursedMaterialPath} no encontrado — las cards malditas " +
                                 "van a caer al holo.");

            RebuildPrefab(DieCardPrefabPath, "DiceBagDieCard", root =>
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = DieCellSize;

                // La casilla ocupa la parte de arriba de la celda; los cupos van DEBAJO,
                // fuera del marco, que es lo que pidió diseño.
                var frame = EnsureChildRect(rootRect, "Frame", new Vector2(0f, 0f), DieCardSize);
                frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 1f);
                frame.pivot = new Vector2(0.5f, 1f);
                frame.anchoredPosition = Vector2.zero;
                frame.sizeDelta = DieCardSize;
                var frameImage = Ensure<Image>(frame.gameObject);
                frameImage.sprite = idle;
                frameImage.type = Image.Type.Sliced;
                frameImage.raycastTarget = true;

                var icon = EnsureChildRect(frame, "Icon", Vector2.zero, Vector2.zero);
                Stretch(icon, 14f);
                var iconImage = Ensure<Image>(icon.gameObject);
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;

                // Número de caras dentro de la casilla, como en la mesa.
                var faceCount = EnsureChildRect(frame, "FaceCount", Vector2.zero, Vector2.zero);
                Stretch(faceCount, 0f);
                var faceLabel = EnsureLabel(faceCount.gameObject, font, 26f, TextAlignmentOptions.Center);

                var slots = EnsureChildRect(rootRect, "Slots", new Vector2(0f, 2f), new Vector2(DieCellSize.x, 26f));
                slots.anchorMin = slots.anchorMax = new Vector2(0.5f, 0f);
                slots.pivot = new Vector2(0.5f, 0f);
                slots.anchoredPosition = new Vector2(0f, 2f);
                var slotsLabel = EnsureLabel(slots.gameObject, font, 16f, TextAlignmentOptions.Center);

                var button = Ensure<Button>(frame.gameObject);
                button.transition = Selectable.Transition.None;
                button.targetGraphic = frameImage;

                var view = Ensure<DiceBagDieCardView>(root);
                var so = new SerializedObject(view);
                so.FindProperty("_frame").objectReferenceValue = frameImage;
                so.FindProperty("_diceIcon").objectReferenceValue = iconImage;
                so.FindProperty("_faceCountLabel").objectReferenceValue = faceLabel;
                so.FindProperty("_slotsLabel").objectReferenceValue = slotsLabel;
                so.FindProperty("_button").objectReferenceValue = button;
                so.FindProperty("_idleFrame").objectReferenceValue = idle;
                so.FindProperty("_selectedFrame").objectReferenceValue = selected;
                so.FindProperty("_enchantMaterial").objectReferenceValue = enchantMaterial;
                so.FindProperty("_cursedMaterial").objectReferenceValue = cursedMaterial;
                so.ApplyModifiedPropertiesWithoutUndo();

                var layout = Ensure<LayoutElement>(root);
                layout.preferredWidth = DieCellSize.x;
                layout.preferredHeight = DieCellSize.y;
            });
        }

        // ================================================================
        // 2 - Cupo
        // ================================================================

        [MenuItem("Rollgeon/Dice Bag/2 - Create Slot Prefab")]
        public static void CreateSlotPrefab()
        {
            var empty = LoadSpriteOrError(SlotSheetPath, "EnchanmentSlot_0");
            var filled = LoadSpriteOrError(SlotSheetPath, "EnchanmentSlot_1");
            var rune = LoadSpriteOrError(RuneSheetPath, "Rune_0");
            if (empty == null || filled == null || rune == null) return;

            RebuildPrefab(SlotPrefabPath, "DiceBagSlot", root =>
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = SlotSize;

                var circle = EnsureChildRect(rootRect, "Circle", Vector2.zero, SlotSize);
                Stretch(circle, 0f);
                var circleImage = Ensure<Image>(circle.gameObject);
                circleImage.sprite = empty;
                circleImage.preserveAspect = true;
                circleImage.raycastTarget = true;

                var runeRect = EnsureChildRect(circle, "Rune", Vector2.zero, Vector2.zero);
                Stretch(runeRect, 16f);
                var runeImage = Ensure<Image>(runeRect.gameObject);
                runeImage.sprite = rune;
                runeImage.preserveAspect = true;
                runeImage.raycastTarget = false;
                runeImage.enabled = false;

                var button = Ensure<Button>(circle.gameObject);
                button.transition = Selectable.Transition.None;
                button.targetGraphic = circleImage;

                var view = Ensure<DiceBagSlotView>(root);
                var so = new SerializedObject(view);
                so.FindProperty("_circle").objectReferenceValue = circleImage;
                so.FindProperty("_rune").objectReferenceValue = runeImage;
                so.FindProperty("_button").objectReferenceValue = button;
                so.FindProperty("_emptyCircle").objectReferenceValue = empty;
                so.FindProperty("_filledCircle").objectReferenceValue = filled;
                so.ApplyModifiedPropertiesWithoutUndo();

                var layout = Ensure<LayoutElement>(root);
                layout.preferredWidth = SlotSize.x;
                layout.preferredHeight = SlotSize.y;
            });
        }

        // ================================================================
        // 3 - Panel en Canvas_PlayerStatus
        // ================================================================

        [MenuItem("Rollgeon/Dice Bag/3 - Setup Panel")]
        public static void SetupPanel()
        {
            var panelSprite = LoadSpriteOrError(UiSheet2Path, PanelSlice);
            var sectionSprite = LoadSpriteOrError(UiSheet2Path, SectionSlice);
            var descriptionSprite = LoadSpriteOrError(UiSheetPath, DescriptionSlice);
            if (panelSprite == null || sectionSprite == null || descriptionSprite == null) return;

            var dieCard = AssetDatabase.LoadAssetAtPath<GameObject>(DieCardPrefabPath)?.GetComponent<DiceBagDieCardView>();
            var slot = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath)?.GetComponent<DiceBagSlotView>();
            var faceCard = AssetDatabase.LoadAssetAtPath<GameObject>(FaceCardPrefabPath)?.GetComponent<EnchantmentFaceCardView>();
            if (dieCard == null || slot == null)
            {
                Debug.LogError("[DiceBag] Correr primero los pasos 1 y 2.");
                return;
            }
            if (faceCard == null)
            {
                Debug.LogError($"[DiceBag] No se encontró EnchantmentFaceCardView en {FaceCardPrefabPath}.");
                return;
            }
            var diceUiSettings = AssetDatabase.LoadAssetAtPath<DiceBuildUiSettingsSO>(DiceUiSettingsPath);
            if (diceUiSettings == null)
                Debug.LogWarning($"[DiceBag] {DiceUiSettingsPath} no encontrado — los dados van a salir sin sprite.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = PrefabUtility.LoadPrefabContents(PlayerStatusPrefabPath);
            try
            {
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas == null)
                {
                    Debug.LogError("[DiceBag] Canvas no encontrado en Canvas_PlayerStatus.");
                    return;
                }
                var canvasRect = (RectTransform)canvas.transform;

                var drawerRect = EnsureChildRect(canvasRect, "DiceBagDrawer", Vector2.zero, Vector2.zero);
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

                // El título se acomoda para que la caja de dados arranque justo en -50.
                float y = -TitleTopY;

                // -- Título --
                var title = EnsureSectionRect(panel, "Title", ref y, TitleHeight);
                var titleLabel = EnsureLabel(title.gameObject, font, 30f, TextAlignmentOptions.Center);
                titleLabel.text = "Bolsa de Dados";
                y = -DiceSectionTopY;

                // -- Dados --
                var diceSection = EnsureSectionRect(panel, "DiceSection", ref y, DiceSectionHeight);
                var diceSectionBg = Ensure<Image>(diceSection.gameObject);
                diceSectionBg.sprite = sectionSprite;
                diceSectionBg.type = Image.Type.Sliced;
                diceSectionBg.raycastTarget = false;
                var diceRow = EnsureChildRect(diceSection, "Dice", Vector2.zero, Vector2.zero);
                Stretch(diceRow, 8f);
                EnsureRowLayout(diceRow.gameObject, DiceSpacing);
                y -= 10f;

                // -- Cupos --
                var slotsCaption = EnsureSectionRect(panel, "SlotsCaption", ref y, 26f);
                var slotsCaptionLabel = EnsureLabel(slotsCaption.gameObject, font, 20f, TextAlignmentOptions.Center);
                slotsCaptionLabel.text = "Cupos de encantamiento";

                var slotsSection = EnsureSectionRect(panel, "SlotsSection", ref y, SlotSize.y + 16f);
                var slotsSectionBg = Ensure<Image>(slotsSection.gameObject);
                slotsSectionBg.sprite = sectionSprite;
                slotsSectionBg.type = Image.Type.Sliced;
                slotsSectionBg.raycastTarget = false;
                var slotsRow = EnsureChildRect(slotsSection, "Slots", Vector2.zero, Vector2.zero);
                Stretch(slotsRow, 8f);
                EnsureRowLayout(slotsRow.gameObject, SlotSpacing);
                y -= 10f;

                // -- Caras --
                var facesSection = EnsureSectionRect(panel, "FacesSection", ref y, FaceCardSize.y + 16f);
                var facesSectionBg = Ensure<Image>(facesSection.gameObject);
                facesSectionBg.sprite = sectionSprite;
                facesSectionBg.type = Image.Type.Sliced;
                facesSectionBg.raycastTarget = false;
                var facesRow = EnsureChildRect(facesSection, "Faces", Vector2.zero, Vector2.zero);
                Stretch(facesRow, 8f);
                EnsureRowLayout(facesRow.gameObject, FaceSpacing);
                y -= 10f;

                // -- Descripción: ocupa lo que queda hasta el borde inferior --
                float descriptionHeight = Mathf.Max(80f, PanelSize.y + y - PanelPadding);
                var description = EnsureSectionRect(panel, "Description", ref y, descriptionHeight);
                var descriptionBg = Ensure<Image>(description.gameObject);
                descriptionBg.sprite = descriptionSprite;
                descriptionBg.type = Image.Type.Sliced;
                descriptionBg.raycastTarget = false;
                var descriptionTextRect = EnsureChildRect(description, "Text", Vector2.zero, Vector2.zero);
                Stretch(descriptionTextRect, 14f);
                var descriptionLabel = EnsureLabel(descriptionTextRect.gameObject, font, 20f, TextAlignmentOptions.TopLeft);
                descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
                // El panel de descripción es claro: el texto va oscuro o no se lee.
                descriptionLabel.color = new Color(0.11f, 0.09f, 0.07f, 1f);

                var slider = Ensure<SlidingDrawer>(drawerRect.gameObject);
                var sliderSo = new SerializedObject(slider);
                sliderSo.FindProperty("_panel").objectReferenceValue = panel;
                sliderSo.FindProperty("_backdrop").objectReferenceValue = backdropButton;
                sliderSo.FindProperty("_closedX").floatValue = PanelClosedX;
                sliderSo.FindProperty("_openX").floatValue = PanelOpenX;
                sliderSo.ApplyModifiedPropertiesWithoutUndo();

                var view = Ensure<DiceBagView>(drawerRect.gameObject);
                var so = new SerializedObject(view);
                so.FindProperty("_diceContainer").objectReferenceValue = diceRow;
                so.FindProperty("_dieCardPrefab").objectReferenceValue = dieCard;
                so.FindProperty("_slotsContainer").objectReferenceValue = slotsRow;
                so.FindProperty("_slotPrefab").objectReferenceValue = slot;
                so.FindProperty("_facesContainer").objectReferenceValue = facesRow;
                so.FindProperty("_faceCardPrefab").objectReferenceValue = faceCard;
                so.FindProperty("_titleLabel").objectReferenceValue = titleLabel;
                so.FindProperty("_slotsCaptionLabel").objectReferenceValue = slotsCaptionLabel;
                so.FindProperty("_descriptionLabel").objectReferenceValue = descriptionLabel;
                so.FindProperty("_diceUiSettings").objectReferenceValue = diceUiSettings;
                so.ApplyModifiedPropertiesWithoutUndo();

                WireDiceBagIcon(root, slider);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerStatusPrefabPath);
                Debug.Log("[DiceBag] Panel armado en Canvas_PlayerStatus.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void WireDiceBagIcon(GameObject root, SlidingDrawer drawer)
        {
            var icon = FindDeep(root.transform, "DiceBagIcon");
            if (icon == null)
            {
                Debug.LogWarning("[DiceBag] DiceBagIcon no encontrado — correr antes " +
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

        private static void EnsureRowLayout(GameObject go, float spacing)
        {
            var layout = Ensure<HorizontalLayoutGroup>(go);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
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
                Debug.Log($"[DiceBag] Prefab actualizado: {path}");
                return;
            }

            var go = new GameObject(rootName, typeof(RectTransform));
            try
            {
                build(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Debug.Log($"[DiceBag] Prefab creado: {path}");
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
                Debug.LogError($"[DiceBag] Slice '{spriteName}' no encontrado en {assetPath}.");
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
