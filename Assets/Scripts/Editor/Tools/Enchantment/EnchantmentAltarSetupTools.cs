using System.Collections.Generic;
using System.Linq;
using Rollgeon.EditorTools.Localization;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Menu;
using Rollgeon.UI.Screens;
using Rollgeon.Upgrades.Dice.UI;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.Enchantment
{
    /// <summary>
    /// Installer del rediseño de la view del Altar de Encantamiento (mock
    /// <c>enchantment_alter_screen.jpeg</c>): autora los borders 9-slice de los
    /// selects, crea el settings de juice, upserta la localización nueva,
    /// reconstruye los prefabs de card (slot in place + dice card + face card) y
    /// rearma <c>Canvas_EnchantmentAltar.prefab</c> con el layout nuevo.
    /// Idempotente — reejecutar actualiza sin duplicar.
    /// </summary>
    public static class EnchantmentAltarSetupTools
    {
        private const string LogPrefix = "[EnchantmentAltarSetup] ";

        private const string UiSheetPath = "Assets/Art/UI/UI-Sheet-sheet.png";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";
        private const string OutlineMaterialPath = "Assets/Fonts/m6x11plus SDF - MenuOutline.mat";
        private const string MenuJuiceSettingsPath = "Assets/Rollgeon/Services/MenuJuiceSettings.asset";
        private const string ChipStackSettingsPath = "Assets/Rollgeon/Services/ChipStackSettings.asset";
        private const string DiceBuildSettingsPath = "Assets/Rollgeon/Services/DiceBuildUiSettings.asset";
        private const string AltarUiSettingsPath = "Assets/Rollgeon/Services/EnchantmentAltarUiSettings.asset";
        private const string ItemButtonPrefabPath = "Assets/Rollgeon/Upgrades/Dice/Prefabs/EnchantmentItemButton.prefab";
        private const string DiceCardPrefabPath = "Assets/Rollgeon/Upgrades/Dice/Prefabs/EnchantmentDiceCard.prefab";
        private const string FaceCardPrefabPath = "Assets/Rollgeon/Upgrades/Dice/Prefabs/EnchantmentFaceCard.prefab";
        private const string AltarCanvasPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_EnchantmentAltar.prefab";

        private const string SelectSpriteName = "UI-Sheet-sheet_10";
        private const string SelectedSpriteName = "UI-Sheet-sheet_11";
        private const string PanelSpriteName = "UI-Sheet-sheet_1";

        // Paleta (Rollgeon_Paleta_de_Color.md): texto primario, detalle cálido,
        // texto secundario de descripciones (mismo gris que class selection).
        private static readonly Color TextColor = new Color32(0xE7, 0xE3, 0xE2, 0xFF);
        private static readonly Color DescColor = new Color32(0xB8, 0xC0, 0xC8, 0xFF);

        // Números sobre los sprites de dado (claros) — oscuro para que lean bien.
        private static readonly Color NumberColor = new Color32(0x33, 0x33, 0x33, 0xFF);

        // Tuning fino de playtest: posición de la pila de oro dentro de su row y
        // escala del ChipRoot (la pila del HUD queda grande dentro del panel).
        private static readonly Vector2 GoldPilePos = new Vector2(-64.4f, 38f);
        private static readonly Vector3 GoldPileScale = new Vector3(0.7f, 0.7f, 1f);

        // ================================================================
        // Menú
        // ================================================================

        [MenuItem("Rollgeon/Enchantment Altar/Setup All")]
        public static void SetupAll()
        {
            AuthorSpriteBorders();
            CreateUiSettings();
            UpsertLocalization();
            RebuildCardPrefabs();
            RebuildAltarPanel();
        }

        // ================================================================
        // 1 - Borders 9-slice de los selects
        // ================================================================

        [MenuItem("Rollgeon/Enchantment Altar/1 - Author Sprite Borders")]
        public static void AuthorSpriteBorders()
        {
            // L, B, R, T — mismos 12px que el resto de los frames del sheet.
            var borders = new Dictionary<string, Vector4>
            {
                [SelectSpriteName] = new Vector4(12, 12, 12, 12),
                [SelectedSpriteName] = new Vector4(12, 12, 12, 12),
            };

            var importer = (TextureImporter)AssetImporter.GetAtPath(UiSheetPath);
            if (importer == null)
            {
                Debug.LogError(LogPrefix + "No se pudo abrir el importer de " + UiSheetPath);
                return;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // Mutar los MISMOS SpriteRect devueltos: preserva spriteID y por lo
            // tanto el nameFileIdTable — ninguna referencia existente se rompe.
            var rects = provider.GetSpriteRects();
            bool dirty = false;
            foreach (var rect in rects)
            {
                if (!borders.TryGetValue(rect.name, out var border)) continue;
                if (rect.border == border) continue;
                rect.border = border;
                dirty = true;
            }

            if (!dirty)
            {
                Debug.Log(LogPrefix + "Bordes ya autorados — sin cambios.");
                return;
            }

            provider.SetSpriteRects(rects);
            provider.Apply();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log(LogPrefix + "Bordes 9-slice autorados en _10/_11.");
        }

        // ================================================================
        // 2 - Settings de juice
        // ================================================================

        [MenuItem("Rollgeon/Enchantment Altar/2 - Create Ui Settings")]
        public static void CreateUiSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<EnchantmentAltarUiSettingsSO>(AltarUiSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<EnchantmentAltarUiSettingsSO>();
                AssetDatabase.CreateAsset(settings, AltarUiSettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log(LogPrefix + AltarUiSettingsPath + " creado con defaults.");
                return;
            }
            Debug.Log(LogPrefix + "Ui settings ya existía — se respeta el tuning actual.");
        }

        // ================================================================
        // 3 - Localización
        // ================================================================

        [MenuItem("Rollgeon/Enchantment Altar/3 - Upsert Localization")]
        public static void UpsertLocalization()
        {
            LocalizationSetupTools.UpsertEntry("UI", "altar.title",
                "Altar de Encantamiento", "Enchantment Altar");
            LocalizationSetupTools.UpsertEntry("UI", "altar.your_dice",
                "Tus dados:", "Your dice:");
            LocalizationSetupTools.UpsertEntry("UI", "altar.choose_slot",
                "Elige el cupo del dado seleccionado:", "Choose the selected die's slot:");
            LocalizationSetupTools.UpsertEntry("UI", "altar.slot",
                "Cupo", "Slot");
            LocalizationSetupTools.UpsertEntry("UI", "altar.slots_suffix",
                "cupos", "slots");
            LocalizationSetupTools.UpsertEntry("UI", "altar.enchantment",
                "Encantamiento", "Enchantment");
            LocalizationSetupTools.UpsertEntry("UI", "altar.confirm",
                "Confirmar", "Confirm");
            LocalizationSetupTools.UpsertEntry("UI", "altar.close",
                "Cerrar", "Close");
            Debug.Log(LogPrefix + "Localización del altar upserted.");
        }

        // ================================================================
        // 4 - Prefabs de card
        // ================================================================

        [MenuItem("Rollgeon/Enchantment Altar/4 - Rebuild Card Prefabs")]
        public static void RebuildCardPrefabs()
        {
            RebuildSlotCardPrefab();
            RebuildDiceCardPrefab();
            RebuildFaceCardPrefab();
        }

        /// <summary>Slot card — se edita el prefab existente IN PLACE (conserva guid).</summary>
        private static void RebuildSlotCardPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(ItemButtonPrefabPath);
            try
            {
                BuildCardInto(root, isDiceCard: false);
                PrefabUtility.SaveAsPrefabAsset(root, ItemButtonPrefabPath);
                Debug.Log(LogPrefix + "EnchantmentItemButton (slot card) reconstruido in place.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RebuildDiceCardPrefab()
        {
            RebuildOrCreatePrefab(DiceCardPrefabPath, "EnchantmentDiceCard",
                root => BuildCardInto(root, isDiceCard: true));
            Debug.Log(LogPrefix + "EnchantmentDiceCard reconstruido.");
        }

        private static void RebuildFaceCardPrefab()
        {
            RebuildOrCreatePrefab(FaceCardPrefabPath, "EnchantmentFaceCard", BuildFaceCardInto);
            Debug.Log(LogPrefix + "EnchantmentFaceCard reconstruido.");
        }

        private static void RebuildOrCreatePrefab(string path, string rootName, System.Action<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    build(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                return;
            }

            var go = new GameObject(rootName, typeof(RectTransform));
            try
            {
                build(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Estructura común de las cards de dado y cupo: fondo _10, highlight _11,
        /// Label/SubLabel; la dice card suma Icon + IconNumber (nº de caras).
        /// </summary>
        private static void BuildCardInto(GameObject root, bool isDiceCard)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            var selectSprite = LoadSpriteOrError(SelectSpriteName);
            var selectedSprite = LoadSpriteOrError(SelectedSpriteName);
            var juiceSettings = AssetDatabase.LoadAssetAtPath<EnchantmentAltarUiSettingsSO>(AltarUiSettingsPath);
            if (juiceSettings == null)
                Debug.LogWarning(LogPrefix + "EnchantmentAltarUiSettings no existe — correr primero '2 - Create Ui Settings'.");

            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = isDiceCard ? new Vector2(112f, 132f) : new Vector2(172f, 116f);

            // El prefab viejo traía VerticalLayoutGroup + LayoutElement en el root:
            // el layout apilaba los hijos como filas y dejaba al SelectedHighlight
            // (sizeDelta 0) con alto 0 — invisible. Los hijos se posicionan a mano.
            StripLayoutComponents(root);

            if (!root.TryGetComponent<Image>(out var bg)) bg = root.AddComponent<Image>();
            bg.sprite = selectSprite;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            bg.raycastTarget = true;

            if (!root.TryGetComponent<Button>(out var button)) button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None; // hover/selección los maneja el view

            // -- Children --
            var expected = new List<string> { "SelectedHighlight", "Label", "SubLabel" };
            if (isDiceCard) expected.Add("Icon");

            var highlight = EnsureChildRect(rootRect, "SelectedHighlight");
            Stretch(highlight);
            if (!highlight.TryGetComponent<Image>(out var highlightImage))
                highlightImage = highlight.gameObject.AddComponent<Image>();
            highlightImage.sprite = selectedSprite;
            highlightImage.type = Image.Type.Sliced;
            highlightImage.color = Color.white;
            highlightImage.raycastTarget = false;
            highlight.gameObject.SetActive(false);

            var label = EnsureTmp(rootRect, "Label", string.Empty, 20f, TextColor, font, null, wrap: false);
            StripLayoutComponents(label.gameObject); // LayoutElement heredado del prefab viejo
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -12f);
            labelRect.sizeDelta = new Vector2(150f, 26f);

            var subLabel = EnsureTmp(rootRect, "SubLabel", string.Empty, 16f, TextColor, font, null, wrap: true);
            StripLayoutComponents(subLabel.gameObject);
            var subRect = (RectTransform)subLabel.transform;
            subRect.anchorMin = subRect.anchorMax = new Vector2(0.5f, 0f);
            subRect.pivot = new Vector2(0.5f, 0f);
            if (isDiceCard)
            {
                subRect.anchoredPosition = new Vector2(0f, 10f);
                subRect.sizeDelta = new Vector2(100f, 24f);
            }
            else
            {
                subRect.anchoredPosition = new Vector2(0f, 12f);
                subRect.sizeDelta = new Vector2(150f, 58f);
            }

            RectTransform icon = null;
            TMP_Text iconNumber = null;
            if (isDiceCard)
            {
                icon = EnsureChildRect(rootRect, "Icon");
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);
                icon.anchoredPosition = new Vector2(0f, 14f);
                icon.sizeDelta = new Vector2(68f, 68f);
                if (!icon.TryGetComponent<Image>(out var iconImage))
                    iconImage = icon.gameObject.AddComponent<Image>();
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;

                // El nº de caras va centrado SOBRE el sprite del dado — oscuro y
                // sin outline: los dados son claros (#E7E3E2).
                iconNumber = EnsureTmp(icon, "IconNumber", string.Empty, 28f, NumberColor, font, null, wrap: false);
                if (font != null) iconNumber.fontSharedMaterial = font.material; // limpiar outline heredado
                Stretch((RectTransform)iconNumber.transform);
            }

            DestroyUnexpectedChildren(rootRect, expected);

            // Highlight detrás de los textos (el bg vive en el root, siempre atrás).
            highlight.SetSiblingIndex(0);

            if (!root.TryGetComponent<EnchantmentItemButtonView>(out var view))
                view = root.AddComponent<EnchantmentItemButtonView>();
            var so = new SerializedObject(view);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_subLabel").objectReferenceValue = subLabel;
            so.FindProperty("_selectedHighlight").objectReferenceValue = highlight.gameObject;
            so.FindProperty("_icon").objectReferenceValue = icon != null ? icon.GetComponent<Image>() : null;
            so.FindProperty("_iconNumberLabel").objectReferenceValue = iconNumber;
            so.FindProperty("_juiceSettings").objectReferenceValue = juiceSettings;
            so.ApplyModifiedProperties();
        }

        private static void BuildFaceCardInto(GameObject root)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(48f, 48f);

            var icon = EnsureChildRect(rootRect, "Icon");
            Stretch(icon);
            if (!icon.TryGetComponent<Image>(out var iconImage))
                iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var number = EnsureTmp(rootRect, "Number", string.Empty, 20f, NumberColor, font, null, wrap: false);
            if (font != null) number.fontSharedMaterial = font.material; // limpiar outline heredado
            Stretch((RectTransform)number.transform);

            DestroyUnexpectedChildren(rootRect, new List<string> { "Icon", "Number" });

            if (!root.TryGetComponent<EnchantmentFaceCardView>(out var view))
                view = root.AddComponent<EnchantmentFaceCardView>();
            var so = new SerializedObject(view);
            so.FindProperty("_icon").objectReferenceValue = iconImage;
            so.FindProperty("_numberLabel").objectReferenceValue = number;
            so.ApplyModifiedProperties();
        }

        // ================================================================
        // 5 - Panel del altar
        // ================================================================

        [MenuItem("Rollgeon/Enchantment Altar/5 - Rebuild Altar Panel")]
        public static void RebuildAltarPanel()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            var menuJuice = AssetDatabase.LoadAssetAtPath<MenuJuiceSettingsSO>(MenuJuiceSettingsPath);
            var chipSettings = AssetDatabase.LoadAssetAtPath<ChipStackSettingsSO>(ChipStackSettingsPath);
            var diceUiSettings = AssetDatabase.LoadAssetAtPath<DiceBuildUiSettingsSO>(DiceBuildSettingsPath);
            var altarUiSettings = AssetDatabase.LoadAssetAtPath<EnchantmentAltarUiSettingsSO>(AltarUiSettingsPath);
            var panelSprite = LoadSpriteOrError(PanelSpriteName);
            var diceCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DiceCardPrefabPath);
            var faceCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FaceCardPrefabPath);
            var slotCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemButtonPrefabPath);

            if (chipSettings == null || diceUiSettings == null)
            {
                Debug.LogError(LogPrefix + "Faltan ChipStackSettings o DiceBuildUiSettings — correr sus installers primero " +
                               "(Rollgeon → Chip Stack HUD / Build Selection).");
                return;
            }
            if (diceCardPrefab == null || faceCardPrefab == null || slotCardPrefab == null)
            {
                Debug.LogError(LogPrefix + "Faltan prefabs de card — correr primero '4 - Rebuild Card Prefabs'.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(AltarCanvasPrefabPath);
            try
            {
                var view = root.GetComponentInChildren<EnchantmentAltarView>(true);
                if (view == null)
                {
                    Debug.LogError(LogPrefix + "EnchantmentAltarView no encontrado en " + AltarCanvasPrefabPath);
                    return;
                }
                // El host de la view puede tener un Transform plano (prefab viejo);
                // AddComponent<RectTransform> lo convierte preservando hijos.
                var viewRect = view.transform as RectTransform;
                if (viewRect == null)
                {
                    viewRect = view.gameObject.AddComponent<RectTransform>();
                    Stretch(viewRect); // recién convertido: ocupar todo el canvas
                }

                // -- Panel root --
                var panel = EnsureChildRect(viewRect, "EnchantmentAltarPanel");
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = Vector2.zero;
                // BUG-035: presupuesto vertical con FacesContainer en su máximo de 2
                // filas (D20, 102px) — 12 filas (40+80+24+140+24+122+40+24+102+80+56+56
                // = 788) + 11 spacings×6 (66) + padding vertical 24+16 (40) = 894 ≤ 900.
                panel.sizeDelta = new Vector2(680f, 900f);

                if (!panel.TryGetComponent<Image>(out var panelBg)) panelBg = panel.gameObject.AddComponent<Image>();
                panelBg.sprite = panelSprite;
                panelBg.type = Image.Type.Sliced;
                panelBg.color = Color.white;
                panelBg.raycastTarget = true; // el panel bloquea clicks al mundo detrás

                if (!panel.TryGetComponent<CanvasGroup>(out _)) panel.gameObject.AddComponent<CanvasGroup>();

                var layout = panel.GetComponent<VerticalLayoutGroup>();
                if (layout == null) layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(30, 30, 24, 16);
                layout.spacing = 6f; // BUG-035: 8→6 para cerrar el presupuesto vertical a 900 (ver cuenta arriba)
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                // -- Children (en orden del mock) --
                var expected = new List<string>
                {
                    "Title", "GoldRow", "DiceCaption", "DiceContainer", "SlotCaption",
                    "SlotContainer", "SlotDescription", "FacesCaption", "FacesContainer",
                    "CostRow", "ButtonRow", "ResultLabel",
                };

                var title = EnsureLayoutTmp(panel, "Title", "Altar de Encantamiento", 40f, TextColor, font, outlineMat, 40f, wrap: false);
                var goldRow = EnsureChildRect(panel, "GoldRow");
                SetLayoutChildHeight(goldRow, 80f);
                var goldParts = BuildGoldPileRow(goldRow, "GoldStack", "GoldLabel", 28f, chipSettings, font);

                var diceCaption = EnsureLayoutTmp(panel, "DiceCaption", "Tus dados:", 22f, TextColor, font, null, 24f, wrap: false);
                var diceContainer = EnsureCardsContainer(panel, "DiceContainer", 140f, 12f);
                var slotCaption = EnsureLayoutTmp(panel, "SlotCaption", "Elige el cupo del dado seleccionado:", 22f, TextColor, font, null, 24f, wrap: false);
                // BUG-035: grid en vez de horizontal — D20 = 4 cupos de 148 no entraban
                // en una fila de Horizontal sin control de ancho (736px > 620 útiles).
                // 4×148 + 3×8 = 616 ≤ 620, siempre 1 fila.
                var slotContainer = EnsureGridCardsContainer(panel, "SlotContainer",
                    new Vector2(148f, 116f), new Vector2(8f, 8f), 122f, dynamicHeight: false);
                var slotDescription = EnsureLayoutTmp(panel, "SlotDescription", string.Empty, 18f, DescColor, font, null, 40f, wrap: true);
                var facesCaption = EnsureLayoutTmp(panel, "FacesCaption", "Caras actuales:", 22f, TextColor, font, null, 24f, wrap: false);
                // BUG-035: D20 = 20 caras de 48 no entraban en 1 fila (1150px > 620
                // útiles). 10×48 + 9×10 = 570 ≤ 620 → 10 por fila, D20 = 2 filas;
                // alto dinámico via ContentSizeFitter (ver EnsureGridCardsContainer).
                var facesContainer = EnsureGridCardsContainer(panel, "FacesContainer",
                    new Vector2(48f, 48f), new Vector2(10f, 6f), 54f, dynamicHeight: true);
                var costRow = EnsureChildRect(panel, "CostRow");
                SetLayoutChildHeight(costRow, 80f);
                var costParts = BuildGoldPileRow(costRow, "CostStack", "CostLabel", 24f, chipSettings, font);
                var buttonRow = EnsureChildRect(panel, "ButtonRow");
                SetLayoutChildHeight(buttonRow, 56f);
                var buttons = BuildButtonRow(buttonRow, font, outlineMat, menuJuice);
                var resultLabel = EnsureLayoutTmp(panel, "ResultLabel", string.Empty, 18f, TextColor, font, null, 56f, wrap: true);

                DestroyUnexpectedChildren(panel, expected);

                // Orden de hermanos = orden visual del VerticalLayoutGroup.
                for (int i = 0; i < expected.Count; i++)
                {
                    var child = panel.Find(expected[i]);
                    if (child != null) child.SetSiblingIndex(i);
                }

                // -- Wiring del view --
                var so = new SerializedObject(view);
                so.FindProperty("_panelRoot").objectReferenceValue = panel.gameObject;
                so.FindProperty("_titleLabel").objectReferenceValue = title;
                so.FindProperty("_goldLabel").objectReferenceValue = goldParts.Label;
                so.FindProperty("_goldDisplay").objectReferenceValue = goldParts.Display;
                so.FindProperty("_diceContainer").objectReferenceValue = diceContainer;
                so.FindProperty("_diceButtonPrefab").objectReferenceValue = diceCardPrefab.GetComponent<EnchantmentItemButtonView>();
                so.FindProperty("_slotContainer").objectReferenceValue = slotContainer;
                so.FindProperty("_slotButtonPrefab").objectReferenceValue = slotCardPrefab.GetComponent<EnchantmentItemButtonView>();
                so.FindProperty("_slotDescriptionLabel").objectReferenceValue = slotDescription;
                so.FindProperty("_facesContainer").objectReferenceValue = facesContainer;
                so.FindProperty("_faceCardPrefab").objectReferenceValue = faceCardPrefab.GetComponent<EnchantmentFaceCardView>();
                so.FindProperty("_diceCaptionLabel").objectReferenceValue = diceCaption;
                so.FindProperty("_slotCaptionLabel").objectReferenceValue = slotCaption;
                so.FindProperty("_facesCaptionLabel").objectReferenceValue = facesCaption;
                so.FindProperty("_costLabel").objectReferenceValue = costParts.Label;
                so.FindProperty("_costGoldDisplay").objectReferenceValue = costParts.Display;
                so.FindProperty("_facesPreviewLabel").objectReferenceValue = null; // reemplazado por las mini-cards
                so.FindProperty("_confirmButton").objectReferenceValue = buttons.Confirm;
                so.FindProperty("_closeButton").objectReferenceValue = buttons.Close;
                so.FindProperty("_resultLabel").objectReferenceValue = resultLabel;
                so.FindProperty("_diceUiSettings").objectReferenceValue = diceUiSettings;
                so.FindProperty("_uiSettings").objectReferenceValue = altarUiSettings;
                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(root, AltarCanvasPrefabPath);
                Debug.Log(LogPrefix + "Canvas_EnchantmentAltar reconstruido y cableado.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private readonly struct GoldRowParts
        {
            public readonly AltarGoldDisplayView Display;
            public readonly TextMeshProUGUI Label;

            public GoldRowParts(AltarGoldDisplayView display, TextMeshProUGUI label)
            {
                Display = display;
                Label = label;
            }
        }

        /// <summary>
        /// Row con pila de oro (ChipStackView + tilted, siempre completa) a la
        /// izquierda y un label al lado — misma receta que el HUD pero estática
        /// en composición. Usada por el GoldRow ("Oro: N") y el CostRow ("Costo: N oro").
        /// </summary>
        private static GoldRowParts BuildGoldPileRow(RectTransform row, string stackName, string labelName,
            float labelFontSize, ChipStackSettingsSO chipSettings, TMP_FontAsset font)
        {
            var stack = EnsureChildRect(row, stackName);
            stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 0f);
            stack.pivot = new Vector2(0.5f, 0f);
            stack.anchoredPosition = GoldPilePos;
            stack.sizeDelta = new Vector2(80f, 90f);

            var chipRoot = EnsureChildRect(stack, "ChipRoot");
            chipRoot.anchorMin = chipRoot.anchorMax = new Vector2(0.5f, 0f);
            chipRoot.pivot = new Vector2(0.5f, 0f);
            chipRoot.anchoredPosition = Vector2.zero;
            chipRoot.sizeDelta = new Vector2(80f, 0f);
            chipRoot.localScale = GoldPileScale;

            // Ficha inclinada apoyada — misma posición fina que el HUD; acá vive
            // siempre activa (la pila del altar está siempre completa).
            var tilted = EnsureChildRect(chipRoot, "TiltedChip");
            tilted.anchorMin = tilted.anchorMax = new Vector2(0.5f, 0f);
            tilted.pivot = new Vector2(0.5f, 0f);
            tilted.anchoredPosition = new Vector2(19.5f, -0.3f);
            if (!tilted.TryGetComponent<Image>(out var tiltedImage))
                tiltedImage = tilted.gameObject.AddComponent<Image>();
            tiltedImage.sprite = chipSettings.GoldChipTilted;
            tiltedImage.raycastTarget = false;
            if (chipSettings.GoldChipTilted != null)
                tilted.sizeDelta = chipSettings.GoldChipTilted.rect.size * Mathf.Max(1f, chipSettings.ChipScale);
            tilted.gameObject.SetActive(true);

            if (!stack.TryGetComponent<ChipStackView>(out var stackView))
                stackView = stack.gameObject.AddComponent<ChipStackView>();
            var stackSo = new SerializedObject(stackView);
            stackSo.FindProperty("_chipRoot").objectReferenceValue = chipRoot;
            stackSo.ApplyModifiedProperties();

            var label = EnsureTmp(row, labelName, string.Empty, labelFontSize, TextColor, font, null, wrap: false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(-30f, 0f);
            labelRect.sizeDelta = new Vector2(240f, 40f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            if (!row.TryGetComponent<AltarGoldDisplayView>(out var display))
                display = row.gameObject.AddComponent<AltarGoldDisplayView>();
            var so = new SerializedObject(display);
            so.FindProperty("_stack").objectReferenceValue = stackView;
            so.FindProperty("_tiltedChip").objectReferenceValue = tiltedImage;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_settings").objectReferenceValue = chipSettings;
            so.ApplyModifiedProperties();

            DestroyUnexpectedChildren(row, new List<string> { stackName, labelName });
            return new GoldRowParts(display, label);
        }

        private readonly struct ButtonRowParts
        {
            public readonly Button Confirm;
            public readonly Button Close;

            public ButtonRowParts(Button confirm, Button close)
            {
                Confirm = confirm;
                Close = close;
            }
        }

        private static ButtonRowParts BuildButtonRow(RectTransform buttonRow, TMP_FontAsset font,
            Material outlineMat, MenuJuiceSettingsSO menuJuice)
        {
            // SIN layout group: JuicyMenuButton escribe la anchoredPosition del
            // botón cada frame hacia la pose capturada en su primer OnEnable — con
            // un HorizontalLayoutGroup pelean y los botones colapsan al centro.
            // Posición fija a mano, como el stack del main menu.
            StripLayoutComponents(buttonRow.gameObject);

            var confirm = EnsureMenuStyleButton(buttonRow, "ConfirmButton", "Confirmar",
                new Vector2(-140f, 0f), font, outlineMat, menuJuice);
            var close = EnsureMenuStyleButton(buttonRow, "CloseButton", "Cerrar",
                new Vector2(140f, 0f), font, outlineMat, menuJuice);
            DestroyUnexpectedChildren(buttonRow, new List<string> { "ConfirmButton", "CloseButton" });
            confirm.transform.SetSiblingIndex(0);
            return new ButtonRowParts(confirm, close);
        }

        /// <summary>
        /// Botón estilo main menu — misma receta que <c>JuicyMenuSetupTools.EnsureJuicyButton</c>:
        /// fondo invisible como raycast target, label m6x11plus con el material de
        /// outline COMPARTIDO (nunca outline per-instance), underline + JuicyMenuButton.
        /// </summary>
        private static Button EnsureMenuStyleButton(RectTransform parent, string name, string text,
            Vector2 anchoredPos, TMP_FontAsset font, Material outlineMat, MenuJuiceSettingsSO menuJuice)
        {
            var rect = EnsureChildRect(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(220f, 56f);

            if (!rect.TryGetComponent<Image>(out var bg)) bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            if (!rect.TryGetComponent<Button>(out var button)) button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None;

            if (!rect.TryGetComponent<CanvasGroup>(out _)) rect.gameObject.AddComponent<CanvasGroup>();

            var label = EnsureTmp(rect, "Label", text, 30f, TextColor, font, outlineMat, wrap: false);
            Stretch((RectTransform)label.transform);

            var underline = EnsureChildRect(rect, "Underline");
            underline.anchorMin = underline.anchorMax = new Vector2(0.5f, 0f);
            underline.pivot = new Vector2(0.5f, 0.5f);
            underline.anchoredPosition = new Vector2(0f, 6f);
            underline.sizeDelta = new Vector2(0f, 3f);
            if (!underline.TryGetComponent<Image>(out var underlineImage))
                underlineImage = underline.gameObject.AddComponent<Image>();
            underlineImage.color = new Color32(0xE0, 0xC0, 0xA9, 0xFF);
            underlineImage.raycastTarget = false;

            if (!rect.TryGetComponent<JuicyMenuButton>(out var juicy))
                juicy = rect.gameObject.AddComponent<JuicyMenuButton>();
            var so = new SerializedObject(juicy);
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_underline").objectReferenceValue = underline;
            so.FindProperty("_settings").objectReferenceValue = menuJuice;
            so.ApplyModifiedProperties();

            return button;
        }

        // ================================================================
        // Helpers genéricos
        // ================================================================

        private static Sprite LoadSpriteOrError(string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(UiSheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogError(LogPrefix + $"Slice '{spriteName}' no encontrado en {UiSheetPath}.");
            return sprite;
        }

        private static RectTransform EnsureChildRect(RectTransform parent, string name)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                rect = (RectTransform)go.transform;
                rect.SetParent(parent, worldPositionStays: false);
            }
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        /// <summary>El VerticalLayoutGroup del panel no controla alturas — cada hijo fija la suya.</summary>
        private static void SetLayoutChildHeight(RectTransform rect, float height)
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private static TextMeshProUGUI EnsureLayoutTmp(RectTransform parent, string name, string text,
            float fontSize, Color color, TMP_FontAsset font, Material outlineMat, float height, bool wrap)
        {
            var tmp = EnsureTmp(parent, name, text, fontSize, color, font, outlineMat, wrap);
            SetLayoutChildHeight((RectTransform)tmp.transform, height);
            return tmp;
        }

        private static TextMeshProUGUI EnsureTmp(RectTransform parent, string name, string text,
            float fontSize, Color color, TMP_FontAsset font, Material outlineMat, bool wrap)
        {
            var rect = EnsureChildRect(parent, name);
            var tmp = rect.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.richText = true;
            tmp.raycastTarget = false;

            // La fuente va antes que el material: el preset de outline pertenece
            // al atlas de m6x11plus, no al default de TMP.
            if (font != null) tmp.font = font;
            if (outlineMat != null) tmp.fontSharedMaterial = outlineMat;
            EditorUtility.SetDirty(tmp);
            return tmp;
        }

        private static RectTransform EnsureCardsContainer(RectTransform parent, string name, float height, float spacing)
        {
            var rect = EnsureChildRect(parent, name);
            SetLayoutChildHeight(rect, height);

            // Limpiar leftovers de un rebuild anterior que haya usado la variante grid.
            if (rect.TryGetComponent<GridLayoutGroup>(out var oldGrid)) Object.DestroyImmediate(oldGrid);
            if (rect.TryGetComponent<ContentSizeFitter>(out var oldFitter)) Object.DestroyImmediate(oldFitter);
            if (rect.TryGetComponent<LayoutElement>(out var oldElement)) Object.DestroyImmediate(oldElement);

            var layout = rect.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        /// <summary>
        /// Container en grilla que envuelve (wrap) a la fila siguiente cuando el
        /// contenido excede el ancho útil del panel — BUG-035: con dados grandes
        /// (D20 = 4 cupos, 20 caras) el HorizontalLayoutGroup sin control de ancho
        /// desbordaba el panel en vez de comprimir. <paramref name="dynamicHeight"/>
        /// suma un ContentSizeFitter vertical (usado por FacesContainer, que crece a
        /// 2 filas con D20): el padre (panel) tiene childControlHeight=false, así que
        /// el CSF controla el propio rect del container sin pelear con el
        /// VerticalLayoutGroup — mismo patrón que el resto de las filas fixed-height.
        /// </summary>
        private static RectTransform EnsureGridCardsContainer(RectTransform parent, string name,
            Vector2 cellSize, Vector2 spacing, float height, bool dynamicHeight)
        {
            var rect = EnsureChildRect(parent, name);

            // Leftover de la versión Horizontal previa (pre BUG-035).
            if (rect.TryGetComponent<HorizontalLayoutGroup>(out var oldHorizontal))
                Object.DestroyImmediate(oldHorizontal);

            var grid = rect.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = rect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.Flexible; // columnas = ancho disponible / (cell + spacing)

            if (dynamicHeight)
            {
                var fitter = rect.GetComponent<ContentSizeFitter>();
                if (fitter == null) fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // el ancho lo fuerza el VLG padre
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var element = rect.GetComponent<LayoutElement>();
                if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
                element.minHeight = height; // piso visual para el caso vacío / 1 fila
            }
            else
            {
                if (rect.TryGetComponent<ContentSizeFitter>(out var oldFitter)) Object.DestroyImmediate(oldFitter);
                if (rect.TryGetComponent<LayoutElement>(out var oldElement)) Object.DestroyImmediate(oldElement);
                SetLayoutChildHeight(rect, height);
            }

            return rect;
        }

        /// <summary>
        /// Borra layout components heredados de versiones previas del prefab
        /// (LayoutGroup/LayoutElement/ContentSizeFitter) — los hijos de las cards
        /// y del ButtonRow se posicionan a mano y cualquier layout residual los
        /// pisa en el próximo rebuild (bug del highlight con alto 0).
        /// </summary>
        private static void StripLayoutComponents(GameObject go)
        {
            foreach (var group in go.GetComponents<LayoutGroup>()) Object.DestroyImmediate(group);
            foreach (var element in go.GetComponents<LayoutElement>()) Object.DestroyImmediate(element);
            foreach (var fitter in go.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(fitter);
        }

        private static void DestroyUnexpectedChildren(RectTransform parent, List<string> expected)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (expected.Contains(child.name)) continue;
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
