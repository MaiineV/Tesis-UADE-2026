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
    /// Installer de la view slot-machine del Altar de Encantamiento (mock
    /// <c>mock-altar-nuevo.png</c>): autora los borders 9-slice de los selects,
    /// crea el settings de juice, upserta la localización, reconstruye los
    /// prefabs de card y rearma <c>Canvas_EnchantmentAltar.prefab</c> con el
    /// layout nuevo — 3 slots de opciones + barra de descripción + fila de
    /// dados + palanca con costo a la derecha (sprites <c>slot-machine-sheet</c>).
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

        private const string SlotMachineSheetPath = "Assets/Art/UI/SlotMachine/slot-machine-sheet.png";
        private const string MachineSheetPath = "Assets/Art/UI/SlotMachine/SlotMachine.png";
        private const string OptionFrameSheetPath = "Assets/Art/UI/SlotMachine/SlotMachineEnchanment.png";
        private const string DescFrameSheetPath = "Assets/Art/UI/SlotMachine/SlotMachineEnchanmentDesc.png";
        private const string ShadowSheetPath = "Assets/Art/UI/SlotMachine/SlotMachineShadow.png";
        private const string ButtonsSheetPath = "Assets/Art/UI/SlotMachine/SlotMachineButtons.png";
        private const string EnchantGlowMaterialPath = "Assets/Art/2D/UI/Materials/EnchantHoloUI.mat";

        private const string SelectSpriteName = "UI-Sheet-sheet_10";
        private const string SelectedSpriteName = "UI-Sheet-sheet_11";
        private const string PanelSpriteName = "UI-Sheet-sheet_1";
        private const string LeverUpSpriteName = "slot-machine-sheet_0";
        private const string LeverDownSpriteName = "slot-machine-sheet_1";

        // Paleta (Rollgeon_Paleta_de_Color.md): texto primario, detalle cálido,
        // texto secundario de descripciones (mismo gris que class selection).
        private static readonly Color TextColor = new Color32(0xE7, 0xE3, 0xE2, 0xFF);
        private static readonly Color DescColor = new Color32(0xB8, 0xC0, 0xC8, 0xFF);

        // Números sobre los sprites de dado (claros) — oscuro para que lean bien.
        private static readonly Color NumberColor = new Color32(0x33, 0x33, 0x33, 0xFF);

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
            // Las keys del layout viejo (altar.choose_slot / altar.slot /
            // altar.slots_suffix / altar.confirm) quedaron huérfanas con la slot
            // machine — se dejan en la tabla, ya nadie las consume.
            LocalizationSetupTools.UpsertEntry("UI", "altar.title",
                "Altar de Encantamiento", "Enchantment Altar");
            LocalizationSetupTools.UpsertEntry("UI", "altar.your_dice",
                "Tus dados:", "Your dice:");
            LocalizationSetupTools.UpsertEntry("UI", "altar.enchantment",
                "Encantamiento", "Enchantment");
            LocalizationSetupTools.UpsertEntry("UI", "altar.close",
                "Cerrar", "Close");
            LocalizationSetupTools.UpsertEntry("UI", "altar.confirm",
                "Confirmar", "Confirm");
            LocalizationSetupTools.UpsertEntry("UI", "altar.roll",
                "Tirada", "Roll");
            LocalizationSetupTools.UpsertEntry("UI", "altar.select_die_hint",
                "Elige un dado para encantar.", "Pick a die to enchant.");
            LocalizationSetupTools.UpsertEntry("UI", "altar.pull_hint",
                "Tira de la palanca para revelar 3 encantamientos.",
                "Pull the lever to reveal 3 enchantments.");
            LocalizationSetupTools.UpsertEntry("UI", "altar.choose_option_hint",
                "Elige un encantamiento para sumarlo al dado — pasa el cursor para leerlos.",
                "Pick an enchantment to add it to the die — hover to read them.");
            LocalizationSetupTools.UpsertEntry("UI", "altar.ench_count_singular",
                "encantamiento", "enchantment");
            LocalizationSetupTools.UpsertEntry("UI", "altar.ench_count_plural",
                "encantamientos", "enchantments");
            LocalizationSetupTools.UpsertEntry("UI", "altar.received",
                "Recibiste", "You got");
            LocalizationSetupTools.UpsertEntry("UI", "altar.die_faces",
                "Caras del dado", "Die faces");
            LocalizationSetupTools.UpsertEntry("UI", "altar.no_enchantments",
                "Sin encantamientos", "No enchantments");
            LocalizationSetupTools.UpsertEntry("UI", "altar.load_error",
                "No se pudieron cargar los dados — cierra la mesa y vuelve a intentar.",
                "Couldn't load the dice — close the table and try again.");
            LocalizationSetupTools.UpsertEntry("UI", "altar.confirm_hint",
                "Aprieta Confirmar para encantar el dado.",
                "Press Confirm to enchant the die.");
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

        // ---- Layout sobre el arte de la máquina (coords locales, centro = 0,0) ----
        // Pixel art ×6: SlotMachine_0 es 113×127 → 678×762 en pantalla.
        private const float MachineScale = 6f;
        private static readonly Vector2 MachineSize = new Vector2(113f * MachineScale, 127f * MachineScale);
        private static readonly Vector2 OptionsRowPos = new Vector2(0f, 36f);
        private static readonly Vector2 OptionSlotSize = new Vector2(28f * MachineScale, 25f * MachineScale);
        private static readonly Vector2 DescBarPos = new Vector2(0f, -110f);
        private static readonly Vector2 DescBarSize = new Vector2(87f * MachineScale, 17f * MachineScale);
        private const float DieShelfY = -282f;
        private const float DieShelfStartX = -182f;
        private const float DieShelfSpacing = 90f;
        private static readonly Vector2 ButtonSize = new Vector2(34f * MachineScale, 13f * MachineScale);
        private const float ButtonsY = -336f;
        private const float ConfirmButtonX = -110f;
        private const float CloseButtonX = 110f;

        // Anclaje vertical del AltarContent (tuning de playtest: Top 0 /
        // Bottom -320 → desborda por abajo y todo asienta bajo).
        private const float MachineOverflowTop = 0f;
        private const float MachineOverflowBottom = 320f;

        // Texto sobre las zonas claras de la máquina (descripción y reels) —
        // negro, sin outline (el blanco con outline no contrastaba). Los nombres
        // de encantamientos conservan su color de paleta via rich text; botones
        // y caja de costo siguen con el crema + outline del resto de la UI.
        private static readonly Color MachineTextColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

        [MenuItem("Rollgeon/Enchantment Altar/5 - Rebuild Altar Panel")]
        public static void RebuildAltarPanel()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            var chipSettings = AssetDatabase.LoadAssetAtPath<ChipStackSettingsSO>(ChipStackSettingsPath);
            var diceUiSettings = AssetDatabase.LoadAssetAtPath<DiceBuildUiSettingsSO>(DiceBuildSettingsPath);
            var altarUiSettings = AssetDatabase.LoadAssetAtPath<EnchantmentAltarUiSettingsSO>(AltarUiSettingsPath);
            var machineSprite = LoadSpriteOrError(MachineSheetPath, "SlotMachine_0");
            var optionFrameSprite = LoadSpriteOrError(OptionFrameSheetPath, "SlotMachineEnchanment_0");
            var descFrameSprite = LoadSpriteOrError(DescFrameSheetPath, "SlotMachineEnchanmentDesc_0");
            var shadowSprite = LoadSpriteOrError(ShadowSheetPath, "SlotMachineShadow_0");
            var confirmIdleSprite = LoadSpriteOrError(ButtonsSheetPath, "SlotMachineButtons_2");
            var confirmReadySprite = LoadSpriteOrError(ButtonsSheetPath, "SlotMachineButtons_0");
            var confirmPressedSprite = LoadSpriteOrError(ButtonsSheetPath, "SlotMachineButtons_1");
            var closeSprite = LoadSpriteOrError(ButtonsSheetPath, "SlotMachineButtons_3");
            var closePressedSprite = LoadSpriteOrError(ButtonsSheetPath, "SlotMachineButtons_4");
            var leverUpSprite = LoadSpriteOrError(SlotMachineSheetPath, LeverUpSpriteName);
            var leverDownSprite = LoadSpriteOrError(SlotMachineSheetPath, LeverDownSpriteName);

            if (chipSettings == null || diceUiSettings == null)
            {
                Debug.LogError(LogPrefix + "Faltan ChipStackSettings o DiceBuildUiSettings — correr sus installers primero " +
                               "(Rollgeon → Chip Stack HUD / Build Selection).");
                return;
            }
            if (machineSprite == null || optionFrameSprite == null || descFrameSprite == null
                || shadowSprite == null || confirmIdleSprite == null || closeSprite == null
                || leverUpSprite == null || leverDownSprite == null)
            {
                Debug.LogError(LogPrefix + "Faltan sprites de la máquina en Assets/Art/UI/SlotMachine/.");
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

                // -- Content root: agrupa máquina + palanca para que abran/cierren
                // juntos. Estirado al canvas con desborde vertical (Top -80 /
                // Bottom -240, tuning de playtest) para que todo asiente bajo.
                var content = EnsureChildRect(viewRect, "AltarContent");
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.pivot = new Vector2(0.5f, 0.5f);
                content.anchoredPosition = new Vector2(0f, (MachineOverflowTop - MachineOverflowBottom) * 0.5f);
                content.sizeDelta = new Vector2(0f, MachineOverflowTop + MachineOverflowBottom);
                if (!content.TryGetComponent<CanvasGroup>(out _)) content.gameObject.AddComponent<CanvasGroup>();
                DestroyUnexpectedChildren(viewRect, new List<string> { "AltarContent" });

                // -- La máquina ES el fondo del modal: posiciones fijas sobre el
                // arte, sin layout groups (cada región del sprite tiene su hijo).
                var panel = EnsureChildRect(content, "EnchantmentAltarPanel");
                StripLayoutComponents(panel.gameObject);
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = Vector2.zero;
                panel.sizeDelta = MachineSize;

                if (!panel.TryGetComponent<Image>(out var panelBg)) panelBg = panel.gameObject.AddComponent<Image>();
                panelBg.sprite = machineSprite;
                panelBg.type = Image.Type.Simple; // pixel art escalado uniforme — sin 9-slice
                panelBg.color = Color.white;
                panelBg.raycastTarget = true; // la máquina bloquea clicks al mundo detrás

                var expected = new List<string>
                {
                    "Title", "OptionsRow", "DescriptionBar",
                    "DieSlot0", "DieSlot1", "DieSlot2", "DieSlot3", "DieSlot4",
                    "ConfirmButton", "CloseButton",
                };

                // -- Título de marquesina sobre el domo, arqueado como el arte.
                // Texto pedido por diseño: "Enchantment Table" (no se localiza).
                var title = EnsureChildRect(panel, "Title");
                StripLayoutComponents(title.gameObject);
                title.anchorMin = title.anchorMax = new Vector2(0.5f, 1f);
                title.pivot = new Vector2(0.5f, 1f);
                title.anchoredPosition = new Vector2(0f, -46f);
                title.sizeDelta = new Vector2(460f, 96f);
                var titleTmp = EnsureTmp(title, "Text", "Enchantment Table", 48f, TextColor, font, outlineMat, wrap: false);
                Stretch((RectTransform)titleTmp.transform);
                titleTmp.raycastTarget = false;
                if (!titleTmp.TryGetComponent<Rollgeon.UI.ArcTextWarp>(out _))
                    titleTmp.gameObject.AddComponent<Rollgeon.UI.ArcTextWarp>();
                DestroyUnexpectedChildren(title, new List<string> { "Text" });

                // -- Ventana de reels: 3 slots en la vidriera de la máquina --
                var optionsRow = EnsureChildRect(panel, "OptionsRow");
                StripLayoutComponents(optionsRow.gameObject);
                optionsRow.anchorMin = optionsRow.anchorMax = new Vector2(0.5f, 0.5f);
                optionsRow.pivot = new Vector2(0.5f, 0.5f);
                optionsRow.anchoredPosition = OptionsRowPos;
                optionsRow.sizeDelta = new Vector2(OptionSlotSize.x * 3f + 16f, OptionSlotSize.y);
                var rowLayout = optionsRow.GetComponent<HorizontalLayoutGroup>();
                if (rowLayout == null) rowLayout = optionsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 8f;
                rowLayout.padding = new RectOffset(0, 0, 0, -20); // tuning de playtest
                rowLayout.childAlignment = TextAnchor.MiddleCenter;
                rowLayout.childControlWidth = false;
                rowLayout.childControlHeight = false;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = false;

                var optionSlots = new EnchantmentOptionSlotView[3];
                var optionNames = new List<string>();
                for (int i = 0; i < optionSlots.Length; i++)
                {
                    string slotName = $"OptionSlot{i}";
                    optionNames.Add(slotName);
                    optionSlots[i] = EnsureOptionSlot(optionsRow, slotName, optionFrameSprite, font, altarUiSettings);
                }
                DestroyUnexpectedChildren(optionsRow, optionNames);

                var descriptionLabel = BuildDescriptionBar(panel, descFrameSprite, font);

                // -- Repisa: 5 dados sobre sus sombras --
                var dieSlots = new AltarDieSlotView[5];
                for (int i = 0; i < dieSlots.Length; i++)
                {
                    float x = DieShelfStartX + i * DieShelfSpacing;
                    dieSlots[i] = EnsureDieSlot(panel, $"DieSlot{i}", new Vector2(x, DieShelfY),
                        shadowSprite, font, altarUiSettings);
                }

                // -- Botones de la máquina --
                var confirmView = EnsureConfirmButton(panel, confirmIdleSprite, confirmReadySprite,
                    confirmPressedSprite, altarUiSettings, font, outlineMat);
                var closeButton = EnsureMachineButton(panel, "CloseButton",
                    new Vector2(CloseButtonX, ButtonsY), closeSprite, closePressedSprite,
                    "Cerrar", font, outlineMat);

                DestroyUnexpectedChildren(panel, expected);

                // -- Palanca + costo a la derecha de la máquina --
                var lever = BuildLeverAssembly(content, leverUpSprite, leverDownSprite,
                    chipSettings, font, outlineMat, altarUiSettings, out var costTitleLabel,
                    out var costLabel, out var costGoldDisplay);
                DestroyUnexpectedChildren(content, new List<string> { "EnchantmentAltarPanel", "LeverAssembly" });

                // -- Wiring del view --
                var so = new SerializedObject(view);
                so.FindProperty("_panelRoot").objectReferenceValue = content.gameObject;
                var slotsProp = so.FindProperty("_optionSlots");
                slotsProp.arraySize = optionSlots.Length;
                for (int i = 0; i < optionSlots.Length; i++)
                    slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = optionSlots[i];
                so.FindProperty("_optionDescriptionLabel").objectReferenceValue = descriptionLabel;
                var diceProp = so.FindProperty("_dieSlots");
                diceProp.arraySize = dieSlots.Length;
                for (int i = 0; i < dieSlots.Length; i++)
                    diceProp.GetArrayElementAtIndex(i).objectReferenceValue = dieSlots[i];
                so.FindProperty("_lever").objectReferenceValue = lever;
                so.FindProperty("_costTitleLabel").objectReferenceValue = costTitleLabel;
                so.FindProperty("_costLabel").objectReferenceValue = costLabel;
                so.FindProperty("_costGoldDisplay").objectReferenceValue = costGoldDisplay;
                so.FindProperty("_confirmButton").objectReferenceValue = confirmView;
                so.FindProperty("_closeButton").objectReferenceValue = closeButton;
                so.FindProperty("_diceUiSettings").objectReferenceValue = diceUiSettings;
                so.FindProperty("_uiSettings").objectReferenceValue = altarUiSettings;
                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(root, AltarCanvasPrefabPath);
                Debug.Log(LogPrefix + "Canvas_EnchantmentAltar reconstruido y cableado (máquina).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Un slot de la slot machine: el marco <c>SlotMachineEnchanment_0</c>
        /// clickeable y un viewport enmascarado (RectMask2D) con la columna del
        /// reel adentro — dos labels apilados (el central y el entrante, una fila
        /// arriba) que la view desplaza hacia abajo durante el spin. Icono
        /// reservado (los encantamientos aún no tienen arte). Outline de hover
        /// en runtime.
        /// </summary>
        private static EnchantmentOptionSlotView EnsureOptionSlot(RectTransform parent, string name,
            Sprite bgSprite, TMP_FontAsset font, EnchantmentAltarUiSettingsSO settings)
        {
            const float ViewportPadding = 12f;
            float rowHeight = OptionSlotSize.y - ViewportPadding * 2f; // = alto del viewport

            var rect = EnsureChildRect(parent, name);
            StripLayoutComponents(rect.gameObject);
            rect.sizeDelta = OptionSlotSize;

            if (!rect.TryGetComponent<Image>(out var bg)) bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = bgSprite;
            bg.type = Image.Type.Simple; // marco pixel art escalado uniforme
            bg.color = Color.white;
            bg.raycastTarget = true;

            if (!rect.TryGetComponent<Button>(out var button)) button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None; // hover = outline, lo maneja la view

            // Viewport enmascarado: lo que desfila fuera de esta ventana no se ve.
            var viewport = EnsureChildRect(rect, "ReelViewport");
            Stretch(viewport);
            viewport.sizeDelta = new Vector2(-ViewportPadding, -ViewportPadding * 2f);
            if (!viewport.TryGetComponent<RectMask2D>(out _)) viewport.gameObject.AddComponent<RectMask2D>();

            var column = EnsureChildRect(viewport, "ReelColumn");
            Stretch(column);

            var nameLabel = EnsureTmp(column, "NameLabel", "?", 20f, MachineTextColor, font, null, wrap: true);
            Stretch((RectTransform)nameLabel.transform);

            // El label entrante vive una fila ARRIBA del central — la columna
            // baja y lo trae al centro.
            var spinLabel = EnsureTmp(column, "SpinLabel", string.Empty, 20f, MachineTextColor, font, null, wrap: true);
            var spinRect = (RectTransform)spinLabel.transform;
            Stretch(spinRect);
            spinRect.anchoredPosition = new Vector2(0f, rowHeight);

            DestroyUnexpectedChildren(column, new List<string> { "NameLabel", "SpinLabel" });
            DestroyUnexpectedChildren(viewport, new List<string> { "ReelColumn" });

            // Reservado para el icono futuro del encantamiento — la view lo
            // deshabilita en Awake hasta que haya arte. Fuera del viewport para
            // que el spin no lo arrastre.
            var icon = EnsureChildRect(rect, "Icon");
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
            icon.pivot = new Vector2(0.5f, 1f);
            icon.anchoredPosition = new Vector2(0f, -6f);
            icon.sizeDelta = new Vector2(32f, 32f);
            if (!icon.TryGetComponent<Image>(out var iconImage))
                iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            DestroyUnexpectedChildren(rect, new List<string> { "ReelViewport", "Icon" });

            if (!rect.TryGetComponent<EnchantmentOptionSlotView>(out var slotView))
                slotView = rect.gameObject.AddComponent<EnchantmentOptionSlotView>();
            var so = new SerializedObject(slotView);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_background").objectReferenceValue = bg;
            so.FindProperty("_nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("_reelColumn").objectReferenceValue = column;
            so.FindProperty("_spinLabel").objectReferenceValue = spinLabel;
            so.FindProperty("_icon").objectReferenceValue = iconImage;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();
            return slotView;
        }

        /// <summary>
        /// Barra de descripción de la máquina: el marco
        /// <c>SlotMachineEnchanmentDesc_0</c> en su región del arte, con el texto
        /// adentro. Es también donde viven los hints de flujo y el resultado.
        /// </summary>
        private static TextMeshProUGUI BuildDescriptionBar(RectTransform panel, Sprite bgSprite,
            TMP_FontAsset font)
        {
            var bar = EnsureChildRect(panel, "DescriptionBar");
            StripLayoutComponents(bar.gameObject);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = DescBarPos;
            bar.sizeDelta = DescBarSize;

            if (!bar.TryGetComponent<Image>(out var bg)) bg = bar.gameObject.AddComponent<Image>();
            bg.sprite = bgSprite;
            bg.type = Image.Type.Simple; // marco pixel art escalado uniforme
            bg.color = Color.white;
            bg.raycastTarget = false;

            var label = EnsureTmp(bar, "Text", string.Empty, 22f, MachineTextColor, font, null, wrap: true);
            var labelRect = (RectTransform)label.transform;
            Stretch(labelRect);
            labelRect.sizeDelta = new Vector2(-36f, -20f);

            DestroyUnexpectedChildren(bar, new List<string> { "Text" });
            return label;
        }

        /// <summary>
        /// Una posición de la repisa: sombra (<c>SlotMachineShadow_0</c>) apoyada
        /// abajo y el ícono del dado encima con su número de caras. El fondo
        /// transparente es el raycast target del click.
        /// </summary>
        private static AltarDieSlotView EnsureDieSlot(RectTransform panel, string name, Vector2 pos,
            Sprite shadowSprite, TMP_FontAsset font, EnchantmentAltarUiSettingsSO settings)
        {
            var rect = EnsureChildRect(panel, name);
            StripLayoutComponents(rect.gameObject);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(92f, 120f);

            if (!rect.TryGetComponent<Image>(out var bg)) bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f); // raycast target invisible
            bg.raycastTarget = true;

            if (!rect.TryGetComponent<Button>(out var button)) button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None; // selección = outline + subida

            var shadow = EnsureChildRect(rect, "Shadow");
            shadow.anchorMin = shadow.anchorMax = new Vector2(0.5f, 0f);
            shadow.pivot = new Vector2(0.5f, 0f);
            shadow.anchoredPosition = new Vector2(0f, 4f);
            shadow.sizeDelta = new Vector2(10f * MachineScale, 6f * MachineScale);
            if (!shadow.TryGetComponent<Image>(out var shadowImage))
                shadowImage = shadow.gameObject.AddComponent<Image>();
            shadowImage.sprite = shadowSprite;
            shadowImage.type = Image.Type.Simple;
            shadowImage.raycastTarget = false;

            var icon = EnsureChildRect(rect, "Icon");
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0f);
            icon.pivot = new Vector2(0.5f, 0f);
            icon.anchoredPosition = new Vector2(0f, 18f);
            icon.sizeDelta = new Vector2(64f, 64f);
            if (!icon.TryGetComponent<Image>(out var iconImage))
                iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            // Nº de caras centrado SOBRE el sprite del dado — oscuro, sin outline
            // (los dados son claros), igual que las cards viejas.
            var number = EnsureTmp(icon, "Number", string.Empty, 26f, NumberColor, font, null, wrap: false);
            if (font != null) number.fontSharedMaterial = font.material;
            Stretch((RectTransform)number.transform);
            number.raycastTarget = false;

            DestroyUnexpectedChildren(rect, new List<string> { "Shadow", "Icon" });
            DestroyUnexpectedChildren(icon, new List<string> { "Number" });

            if (!rect.TryGetComponent<AltarDieSlotView>(out var slotView))
                slotView = rect.gameObject.AddComponent<AltarDieSlotView>();
            var so = new SerializedObject(slotView);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_icon").objectReferenceValue = iconImage;
            so.FindProperty("_numberLabel").objectReferenceValue = number;
            so.FindProperty("_shadow").objectReferenceValue = shadowImage;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();
            return slotView;
        }

        /// <summary>
        /// El Confirmar de la máquina: sprite apagado (_2) / prendido (_0) los
        /// maneja <see cref="AltarConfirmButtonView"/>; el presionado (_1) va por
        /// SpriteSwap del Button (overrideSprite pisa el sprite del pulso).
        /// </summary>
        private static AltarConfirmButtonView EnsureConfirmButton(RectTransform panel,
            Sprite idleSprite, Sprite readySprite, Sprite pressedSprite,
            EnchantmentAltarUiSettingsSO settings, TMP_FontAsset font, Material outlineMat)
        {
            var button = EnsureMachineButton(panel, "ConfirmButton",
                new Vector2(ConfirmButtonX, ButtonsY), idleSprite, pressedSprite,
                "Confirmar", font, outlineMat);

            if (!button.TryGetComponent<AltarConfirmButtonView>(out var view))
                view = button.gameObject.AddComponent<AltarConfirmButtonView>();
            var so = new SerializedObject(view);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_image").objectReferenceValue = button.targetGraphic as Image;
            so.FindProperty("_spriteIdle").objectReferenceValue = idleSprite;
            so.FindProperty("_spriteReady").objectReferenceValue = readySprite;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();
            return view;
        }

        /// <summary>
        /// Botón de arte de la máquina: sprite normal + presionado por SpriteSwap,
        /// con el label centrado encima (la view lo re-localiza al abrir).
        /// </summary>
        private static Button EnsureMachineButton(RectTransform panel, string name, Vector2 pos,
            Sprite normalSprite, Sprite pressedSprite, string labelText, TMP_FontAsset font,
            Material outlineMat)
        {
            var rect = EnsureChildRect(panel, name);
            StripLayoutComponents(rect.gameObject);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = ButtonSize;

            if (!rect.TryGetComponent<Image>(out var image)) image = rect.gameObject.AddComponent<Image>();
            image.sprite = normalSprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = true;

            if (!rect.TryGetComponent<Button>(out var button)) button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            var state = button.spriteState;
            state.pressedSprite = pressedSprite;
            button.spriteState = state;

            // El label es hijo del Image — el tint del pulso (color de la Image)
            // no lo afecta.
            var label = EnsureTmp(rect, "Label", labelText, 26f, TextColor, font, outlineMat, wrap: false);
            Stretch((RectTransform)label.transform);
            label.raycastTarget = false;

            // Leftovers de la receta juicy vieja (underline/JuicyMenuButton).
            DestroyUnexpectedChildren(rect, new List<string> { "Label" });
            if (rect.TryGetComponent<Rollgeon.UI.Menu.JuicyMenuButton>(out var oldJuicy))
                Object.DestroyImmediate(oldJuicy);
            if (rect.TryGetComponent<CanvasGroup>(out var oldGroup))
                Object.DestroyImmediate(oldGroup);

            return button;
        }

        /// <summary>
        /// La palanca a la derecha, PEGADA al borde del modal (feedback de
        /// playtest), con la caja de costo arriba — hermana del panel dentro del
        /// content (comparten fade de apertura). El costo usa el ícono de oro
        /// canónico: la pila de fichas del HUD (ChipStack + ficha inclinada) vía
        /// <see cref="AltarGoldDisplayView"/>, siempre completa.
        /// </summary>
        private static AltarLeverView BuildLeverAssembly(RectTransform content, Sprite leverUp,
            Sprite leverDown, ChipStackSettingsSO chipSettings, TMP_FontAsset font, Material outlineMat,
            EnchantmentAltarUiSettingsSO settings, out TextMeshProUGUI costTitleLabel,
            out TextMeshProUGUI costLabel, out AltarGoldDisplayView costGoldDisplay)
        {
            var assembly = EnsureChildRect(content, "LeverAssembly");
            assembly.anchorMin = assembly.anchorMax = new Vector2(0.5f, 0.5f);
            assembly.pivot = new Vector2(0.5f, 0.5f);
            // Panel: 680 de ancho, borde derecho en +340. La palanca (92 de
            // ancho, centrada en el assembly) queda con su borde izquierdo
            // exactamente en +340 → pegada al modal.
            assembly.anchoredPosition = new Vector2(386f, -20f);
            assembly.sizeDelta = new Vector2(200f, 560f);

            // -- Caja de costo (tuning de playtest: 140×96 en x=23) --
            var costBox = EnsureChildRect(assembly, "CostBox");
            costBox.anchorMin = costBox.anchorMax = new Vector2(0.5f, 1f);
            costBox.pivot = new Vector2(0.5f, 1f);
            costBox.anchoredPosition = new Vector2(23f, -150f);
            costBox.sizeDelta = new Vector2(140f, 96f);
            if (!costBox.TryGetComponent<Image>(out var costBg)) costBg = costBox.gameObject.AddComponent<Image>();
            costBg.sprite = LoadSpriteOrError(SelectSpriteName);
            costBg.type = Image.Type.Sliced;
            costBg.color = Color.white;
            costBg.raycastTarget = false;

            // "Tirada" centrado arriba; el valor + la pila van abajo, también al
            // centro — objetos separados (feedback de playtest).
            costTitleLabel = EnsureTmp(costBox, "RollTitle", "Tirada", 24f, TextColor, font, outlineMat, wrap: false);
            var rollTitleRect = (RectTransform)costTitleLabel.transform;
            rollTitleRect.anchorMin = rollTitleRect.anchorMax = new Vector2(0.5f, 1f);
            rollTitleRect.pivot = new Vector2(0.5f, 1f);
            rollTitleRect.anchoredPosition = new Vector2(0f, -10f);
            rollTitleRect.sizeDelta = new Vector2(120f, 30f);

            costLabel = EnsureTmp(costBox, "CostLabel", string.Empty, 24f, TextColor, font, outlineMat, wrap: false);
            var costLabelRect = (RectTransform)costLabel.transform;
            costLabelRect.anchorMin = costLabelRect.anchorMax = new Vector2(0.5f, 0f);
            costLabelRect.pivot = new Vector2(1f, 0f);
            costLabelRect.anchoredPosition = new Vector2(0f, 16f);
            costLabelRect.sizeDelta = new Vector2(64f, 32f);
            costLabel.alignment = TextAlignmentOptions.MidlineRight;

            // Pila de oro canónica (misma receta que el HUD / la mesa vieja):
            // ChipStackView + ficha inclinada. La escala vive en el GoldStack
            // (tuning de playtest: 0.6 uniforme, pos 9,23) — el ChipRoot queda
            // en identidad para no componer escalas.
            var stack = EnsureChildRect(costBox, "GoldStack");
            stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 0f);
            stack.pivot = new Vector2(0.5f, 0f);
            stack.anchoredPosition = new Vector2(9f, 23f);
            stack.sizeDelta = new Vector2(80f, 90f);
            stack.localScale = new Vector3(0.6f, 0.6f, 0.6f);

            var chipRoot = EnsureChildRect(stack, "ChipRoot");
            chipRoot.anchorMin = chipRoot.anchorMax = new Vector2(0.5f, 0f);
            chipRoot.pivot = new Vector2(0.5f, 0f);
            chipRoot.anchoredPosition = Vector2.zero;
            chipRoot.sizeDelta = new Vector2(80f, 0f);
            chipRoot.localScale = Vector3.one;

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

            if (!costBox.TryGetComponent<AltarGoldDisplayView>(out costGoldDisplay))
                costGoldDisplay = costBox.gameObject.AddComponent<AltarGoldDisplayView>();
            var displaySo = new SerializedObject(costGoldDisplay);
            displaySo.FindProperty("_stack").objectReferenceValue = stackView;
            displaySo.FindProperty("_tiltedChip").objectReferenceValue = tiltedImage;
            displaySo.FindProperty("_label").objectReferenceValue = costLabel;
            displaySo.FindProperty("_settings").objectReferenceValue = chipSettings;
            displaySo.ApplyModifiedProperties();

            DestroyUnexpectedChildren(costBox, new List<string> { "RollTitle", "CostLabel", "GoldStack" });

            // -- Palanca (tuning de playtest: x=-16) --
            var lever = EnsureChildRect(assembly, "Lever");
            lever.anchorMin = lever.anchorMax = new Vector2(0.5f, 0f);
            // Pivot abajo: el squash de AltarLeverView comprime hacia la base y
            // la manija "cae" en vez de encogerse al centro.
            lever.pivot = new Vector2(0.5f, 0f);
            lever.anchoredPosition = new Vector2(-16f, -50f);
            lever.sizeDelta = new Vector2(92f, 328f);
            if (!lever.TryGetComponent<Image>(out var leverImage))
                leverImage = lever.gameObject.AddComponent<Image>();
            leverImage.sprite = leverUp;
            leverImage.type = Image.Type.Simple;
            leverImage.preserveAspect = false;
            leverImage.raycastTarget = true;

            if (!lever.TryGetComponent<Button>(out var leverButton))
                leverButton = lever.gameObject.AddComponent<Button>();
            leverButton.targetGraphic = leverImage;
            leverButton.transition = Selectable.Transition.None;

            DestroyUnexpectedChildren(assembly, new List<string> { "CostBox", "Lever" });

            if (!lever.TryGetComponent<AltarLeverView>(out var leverView))
                leverView = lever.gameObject.AddComponent<AltarLeverView>();
            var so = new SerializedObject(leverView);
            so.FindProperty("_button").objectReferenceValue = leverButton;
            so.FindProperty("_leverImage").objectReferenceValue = leverImage;
            so.FindProperty("_spriteUp").objectReferenceValue = leverUp;
            so.FindProperty("_spriteDown").objectReferenceValue = leverDown;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();
            return leverView;
        }

        // ================================================================
        // Helpers genéricos
        // ================================================================

        private static Sprite LoadSpriteOrError(string spriteName)
            => LoadSpriteOrError(UiSheetPath, spriteName);

        private static Sprite LoadSpriteOrError(string sheetPath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogError(LogPrefix + $"Slice '{spriteName}' no encontrado en {sheetPath}.");
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
