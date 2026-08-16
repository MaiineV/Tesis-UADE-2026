using System.Collections.Generic;
using System.Linq;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using Rollgeon.EditorTools.Localization;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Menu;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.Menu
{
    /// <summary>
    /// Installer de la pantalla de selección de clase (mockup
    /// <c>Seleccion_personaje.jpeg</c>): bordes 9-slice del UI-Sheet, keys de
    /// localización, rebuild del layout en <c>01_MainMenu</c> y retune del
    /// prefab ComboRow. Idempotente — reejecutar converge sin duplicar.
    /// </summary>
    public static class ClassSelectionSetupTools
    {
        private const string UiSheetPath = "Assets/Art/UI/UI-Sheet-sheet.png";
        private const string ComboRowPrefabPath = "Assets/Prefabs/UI/ComboRow.prefab";
        private const string ClassContractRowPrefabPath = "Assets/Prefabs/UI/ClassSelectContractRow.prefab";
        private const string ContractSettingsPath = "Assets/Rollgeon/Services/ContractSheetUiSettings.asset";
        private const string MainMenuScenePath = "Assets/Scenes/01_MainMenu.unity";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";
        private const string OutlineMaterialPath = "Assets/Fonts/m6x11plus SDF - MenuOutline.mat";
        private const string JuiceSettingsPath = "Assets/Rollgeon/Services/MenuJuiceSettings.asset";
        private const string LockSpritePath = "Assets/Art/UI/Unlocks/LockImage.png";
        private const string ClassIconsPath = "Assets/Art/UI/ClassIcons/ClassIcons.png";
        private const string TooltipCanvasPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_Tooltip.prefab";

        private static readonly Color AccentColor = new Color32(0xE0, 0xC0, 0xA9, 0xFF);
        private static readonly Color TextColor = new Color32(0xE7, 0xE3, 0xE2, 0xFF);
        private static readonly Color LockedColor = new Color32(0x5F, 0x73, 0x7A, 0xFF);
        private static readonly Color FooterColor = new Color32(0x8A, 0x96, 0xA0, 0xFF);

        // Multiplicadores 9-slice (pixel-art: cerca de escala entera; retunear
        // acá). El resto de la pantalla usa sprites nativos o paneles planos —
        // sin fichas (feedback de playtest).
        private const float PortraitFramePpu = 0.5f;
        private const float ContractFramePpu = 1f;

        // Columna de descripción de la fila de contrato drawer-style (la fila base del
        // drawer mide 508 px; con la columna queda en 756).
        private const float ContractDescriptionWidth = 240f;

        [MenuItem("Rollgeon/Class Selection/Setup All")]
        public static void SetupAll()
        {
            AuthorSpriteBorders();
            UpsertLocalization();
            SetupComboRowPrefab();
            SetupContractRowPrefab();
            RebuildScreenLayout();
        }

        // ================================================================
        // 1 - Bordes 9-slice del sheet
        // ================================================================

        [MenuItem("Rollgeon/Class Selection/1 - Author Sprite Borders")]
        public static void AuthorSpriteBorders()
        {
            // L, B, R, T
            var borders = new Dictionary<string, Vector4>
            {
                ["UI-Sheet-sheet_0"] = new Vector4(16, 16, 16, 16),
                ["UI-Sheet-sheet_1"] = new Vector4(20, 20, 20, 20),
                ["UI-Sheet-sheet_2"] = new Vector4(12, 12, 12, 12),
                ["UI-Sheet-sheet_3"] = new Vector4(12, 12, 12, 12),
                ["UI-Sheet-sheet_7"] = new Vector4(12, 12, 12, 12),
                ["UI-Sheet-sheet_13"] = new Vector4(12, 12, 12, 12),
                ["UI-Sheet-sheet_14"] = new Vector4(12, 12, 12, 12),
            };

            var importer = (TextureImporter)AssetImporter.GetAtPath(UiSheetPath);
            if (importer == null)
            {
                Debug.LogError("[ClassSelectionSetup] No se pudo abrir el importer de " + UiSheetPath);
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
                Debug.Log("[ClassSelectionSetup] Bordes ya autorados — sin cambios.");
                return;
            }

            provider.SetSpriteRects(rects);
            provider.Apply();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("[ClassSelectionSetup] Bordes 9-slice autorados en UI-Sheet-sheet.");
        }

        // ================================================================
        // 2 - Localización
        // ================================================================

        [MenuItem("Rollgeon/Class Selection/2 - Upsert Localization")]
        public static void UpsertLocalization()
        {
            LocalizationSetupTools.UpsertEntry("UI", "class_select.contract_title",
                "Contrato", "Contract");
            LocalizationSetupTools.UpsertEntry("UI", "class_select.min_damage",
                "Daño mínimo = dado más alto", "Minimum damage = highest die");
            LocalizationSetupTools.UpsertEntry("UI", "class_select.locked_tooltip",
                "Próximamente", "Coming soon");
            LocalizationSetupTools.UpsertEntry("Content", "passive.warrior.low_hp_rage.name",
                "Furia del Guerrero", "Warrior's Fury");
            // "30 o menos": las tablas vivas ya decían 30 (el "3" de acá era una versión
            // vieja que una re-corrida habría regresado).
            LocalizationSetupTools.UpsertEntry("Content", "passive.warrior.low_hp_rage.desc",
                "Cuando tu vida está en 30 o menos, tu daño base aumenta en 5 puntos. " +
                "Al curarte por encima de ese umbral, vuelve a la normalidad.",
                "When your health is 30 or less, your base damage increases by 5 points. " +
                "Healing above that threshold returns it to normal.");
            AssetDatabase.SaveAssets();
            Debug.Log("[ClassSelectionSetup] Keys de localización upserteadas.");
        }

        // ================================================================
        // 4 - Prefab ComboRow
        // ================================================================

        [MenuItem("Rollgeon/Class Selection/4 - Setup ComboRow Prefab")]
        public static void SetupComboRowPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(ComboRowPrefabPath);
            try
            {
                var row = root.GetComponent<ComboRowView>();
                if (row == null)
                {
                    Debug.LogError("[ClassSelectionSetup] ComboRowView no encontrado en el prefab.");
                    return;
                }

                var rowRect = (RectTransform)root.transform;

                if (root.TryGetComponent<ContentSizeFitter>(out var fitter))
                {
                    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                }

                if (!root.TryGetComponent<LayoutElement>(out var layout))
                    layout = root.AddComponent<LayoutElement>();
                layout.preferredWidth = 570f;
                layout.preferredHeight = 56f;
                rowRect.sizeDelta = new Vector2(570f, 56f);

                if (root.TryGetComponent<HorizontalLayoutGroup>(out var hlg))
                {
                    hlg.padding = new RectOffset(8, 8, 4, 4);
                    hlg.spacing = 10f;
                    hlg.childAlignment = TextAnchor.MiddleLeft;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                }

                // Sin background de fila (feedback de playtest) — el Image queda
                // transparente por si algún día se quiere un hover, pero no pinta.
                if (!root.TryGetComponent<Image>(out var rowBg)) rowBg = root.AddComponent<Image>();
                rowBg.color = new Color(1f, 1f, 1f, 0f);
                rowBg.raycastTarget = false;

                // -- IconFrame (marco de dado siempre visible) con el Icon adentro --
                var iconFrame = rowRect.Find("IconFrame") as RectTransform;
                if (iconFrame == null)
                {
                    var go = new GameObject("IconFrame", typeof(RectTransform), typeof(Image));
                    iconFrame = (RectTransform)go.transform;
                    iconFrame.SetParent(rowRect, worldPositionStays: false);
                }
                iconFrame.SetSiblingIndex(0);
                iconFrame.sizeDelta = new Vector2(48f, 48f);
                var frameImage = iconFrame.GetComponent<Image>();
                // Sin frame en el ícono (feedback de playtest): el slot reserva
                // layout pero no dibuja — solo se ve el Icon interior cuando
                // llegue el arte de los dados.
                frameImage.enabled = false;
                frameImage.raycastTarget = false;
                var frameLayout = iconFrame.GetComponent<LayoutElement>();
                if (frameLayout == null) frameLayout = iconFrame.gameObject.AddComponent<LayoutElement>();
                frameLayout.preferredWidth = 48f;
                frameLayout.preferredHeight = 48f;

                // Reparentar el IconImage existente ADENTRO del frame — mismo
                // fileID, la ref _iconImage del componente sobrevive.
                var iconImage = rowRect.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(t => t.name == "IconImage" && t != iconFrame);
                if (iconImage != null)
                {
                    iconImage.SetParent(iconFrame, worldPositionStays: false);
                    iconImage.anchorMin = iconImage.anchorMax = new Vector2(0.5f, 0.5f);
                    iconImage.pivot = new Vector2(0.5f, 0.5f);
                    iconImage.anchoredPosition = Vector2.zero;
                    iconImage.sizeDelta = new Vector2(32f, 32f);
                    var iconLayout = iconImage.GetComponent<LayoutElement>();
                    if (iconLayout == null) iconLayout = iconImage.gameObject.AddComponent<LayoutElement>();
                    iconLayout.ignoreLayout = true;
                }

                // -- Labels: anchos de columna + tipografía --
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);

                TuneLabel(rowRect, "NameLabel", 140f, 48f, font, outlineMat, TextColor,
                    TextAlignmentOptions.MidlineLeft, 14f, 22f, wrap: false);
                TuneLabel(rowRect, "DamageLabel", 55f, 48f, font, outlineMat, AccentColor,
                    TextAlignmentOptions.Center, 18f, 26f, wrap: false);
                TuneLabel(rowRect, "DescriptionLabel", 285f, 48f, font, outlineMat,
                    new Color32(0xB8, 0xC0, 0xC8, 0xFF),
                    TextAlignmentOptions.MidlineLeft, 14f, 18f, wrap: true);

                // Orden de columnas: IconFrame, Name, Damage, Description.
                SetSibling(rowRect, "NameLabel", 1);
                SetSibling(rowRect, "DamageLabel", 2);
                SetSibling(rowRect, "DescriptionLabel", 3);

                PrefabUtility.SaveAsPrefabAsset(root, ComboRowPrefabPath);
                Debug.Log("[ClassSelectionSetup] ComboRow.prefab retuneado (570x56 + IconFrame).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ================================================================
        // 5 - Prefab de fila de contrato drawer-style (con descripción)
        // ================================================================

        /// <summary>
        /// Genera la variante de fila drawer-style con columna de descripción, reusando el
        /// builder del drawer in-game. Requiere haber corrido antes los pasos 1-2 de
        /// <c>Rollgeon/Contract Drawer</c> (settings + prefab del dado).
        /// </summary>
        [MenuItem("Rollgeon/Class Selection/5 - Setup Contract Row Prefab")]
        public static void SetupContractRowPrefab()
            => Rollgeon.EditorTools.HUD.ContractDrawerSetupTools.BuildRowPrefab(
                ClassContractRowPrefabPath, ContractDescriptionWidth);

        // ================================================================
        // 3 - Layout de la pantalla
        // ================================================================

        [MenuItem("Rollgeon/Class Selection/3 - Rebuild Screen Layout")]
        public static void RebuildScreenLayout()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MenuJuiceSettingsSO>(JuiceSettingsPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (settings == null || outlineMat == null || font == null)
            {
                Debug.LogError("[ClassSelectionSetup] Faltan MenuJuiceSettings / outline mat / font — correr los installers del Juicy Menu primero.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene().path == MainMenuScenePath
                ? EditorSceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            var screen = scene.GetRootGameObjects()
                .Select(r => r.GetComponentInChildren<ClassSelectionScreen>(true))
                .FirstOrDefault(s => s != null);
            if (screen == null)
            {
                Debug.LogError("[ClassSelectionSetup] ClassSelectionScreen no encontrado en la escena.");
                return;
            }

            var screenRect = (RectTransform)screen.transform;
            if (!screen.TryGetComponent<CanvasGroup>(out var rootGroup))
                rootGroup = screen.gameObject.AddComponent<CanvasGroup>();

            // -- Panel izquierdo: 3 clases --
            var leftPanel = EnsureRect(screenRect, "LeftPanel", new Vector2(-800f, 0f), new Vector2(280f, 420f));
            StripLayoutComponents(leftPanel.gameObject);
            var warrior = EnsureClassEntry(screenRect, leftPanel, new[] { "WarriorButton" },
                "WarriorButton", new Vector2(0f, 140f), "class.warrior", "ClassIcons_0",
                locked: false, font, outlineMat);
            var picaro = EnsureClassEntry(screenRect, leftPanel, new[] { "PicaroButton", "RogueButton" },
                "PicaroButton", new Vector2(0f, 0f), "class.rogue", "ClassIcons_1",
                locked: true, font, outlineMat);
            var mago = EnsureClassEntry(screenRect, leftPanel, new[] { "MagoButton", "MageButton" },
                "MagoButton", new Vector2(0f, -140f), "class.mage", "ClassIcons_2",
                locked: true, font, outlineMat);

            DestroyChildrenNotIn(leftPanel, "WarriorButton", "PicaroButton", "MagoButton");

            // -- Retrato central enmarcado --
            // -330 y no -405: rebalancea el hueco tras ensanchar el RightPanel para la
            // fila drawer-style con descripción.
            var portraitFrame = EnsureRect(screenRect, "PortraitFrame", new Vector2(-330f, 10f), new Vector2(450f, 650f));
            if (!portraitFrame.TryGetComponent<Image>(out var portraitFrameImage))
                portraitFrameImage = portraitFrame.gameObject.AddComponent<Image>();
            SetSliced(portraitFrameImage, LoadSprite("UI-Sheet-sheet_1"), PortraitFramePpu);
            portraitFrameImage.raycastTarget = false;

            var portrait = FindAnywhere(screenRect, "Portrait") ?? FindAnywhere(screenRect, "PortraitImage");
            if (portrait == null)
            {
                var go = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portrait = (RectTransform)go.transform;
            }
            portrait.SetParent(portraitFrame, worldPositionStays: false);
            portrait.name = "Portrait";
            portrait.anchorMin = Vector2.zero;
            portrait.anchorMax = Vector2.one;
            portrait.pivot = new Vector2(0.5f, 0.5f);
            portrait.offsetMin = new Vector2(45f, 45f);
            portrait.offsetMax = new Vector2(-45f, -45f);
            var portraitImage = portrait.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            // -- Panel derecho: Contrato --
            // Más pegado a la derecha (feedback: quedaba muy al medio). Ensanchado a 820
            // para la fila drawer-style (508) + columna de descripción (248).
            var rightPanel = EnsureRect(screenRect, "RightPanel", new Vector2(510f, 0f), new Vector2(820f, 900f));

            var header = FindAnywhere(screenRect, "HeaderLabel");
            var headerLabel = EnsureTmpLabel(rightPanel, "HeaderLabel", header, "Contrato", 48f,
                new Vector2(0f, 385f), new Vector2(320f, 64f), font, outlineMat, TextColor);
            if (headerLabel.GetComponent<TextAnimator_TMP>() == null)
                headerLabel.gameObject.AddComponent<TextAnimator_TMP>();

            // Panel del contrato: fill oscuro + MARCO sheet_7 9-sliced encima
            // (fillCenter off: solo el borde del sprite, el centro lo pone el fill).
            // 800 de ancho: fila de 756 + los 22 px de offset del RowsContainer por lado.
            // Alto 640 = 9 combos de 60 px (el contrato del Warrior trae 9) + spacing +
            // padding + offsets, con 8 px de aire. Un 10º combo pide agrandar el panel.
            var contractPanel = EnsureRect(rightPanel, "ContractPanel", new Vector2(0f, 25f), new Vector2(800f, 640f));
            if (!contractPanel.TryGetComponent<Image>(out var panelImage))
                panelImage = contractPanel.gameObject.AddComponent<Image>();
            panelImage.sprite = null;
            panelImage.type = Image.Type.Simple;
            panelImage.color = new Color(0.09f, 0.10f, 0.15f, 0.85f);
            panelImage.raycastTarget = false;
            if (contractPanel.TryGetComponent<Outline>(out var legacyOutline))
                Object.DestroyImmediate(legacyOutline);

            var panelFrame = EnsureRect(contractPanel, "PanelFrame", Vector2.zero, Vector2.zero);
            panelFrame.anchorMin = Vector2.zero;
            panelFrame.anchorMax = Vector2.one;
            panelFrame.pivot = new Vector2(0.5f, 0.5f);
            panelFrame.anchoredPosition = Vector2.zero;
            panelFrame.sizeDelta = Vector2.zero;
            panelFrame.SetAsLastSibling();
            if (!panelFrame.TryGetComponent<Image>(out var panelFrameImage))
                panelFrameImage = panelFrame.gameObject.AddComponent<Image>();
            panelFrameImage.sprite = LoadSprite("UI-Sheet-sheet_7");
            panelFrameImage.type = Image.Type.Sliced;
            panelFrameImage.fillCenter = false;
            panelFrameImage.pixelsPerUnitMultiplier = ContractFramePpu;
            panelFrameImage.raycastTarget = false;

            var rows = FindAnywhere(screenRect, "RowsContainer");
            if (rows == null)
            {
                var go = new GameObject("RowsContainer", typeof(RectTransform));
                rows = (RectTransform)go.transform;
            }
            rows.SetParent(contractPanel, worldPositionStays: false);
            rows.anchorMin = Vector2.zero;
            rows.anchorMax = Vector2.one;
            rows.pivot = new Vector2(0.5f, 0.5f);
            rows.offsetMin = new Vector2(22f, 22f);
            rows.offsetMax = new Vector2(-22f, -22f);
            if (!rows.TryGetComponent<VerticalLayoutGroup>(out var vlg))
                vlg = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 8, 8);
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var footer = FindAnywhere(screenRect, "FooterLabel");
            var footerLabel = EnsureTmpLabel(rightPanel, "FooterLabel", footer, "Daño mínimo = dado más alto",
                22f, new Vector2(0f, -320f), new Vector2(780f, 32f), font, outlineMat, FooterColor);
            LocalizationSetupTools.BindTMP(footerLabel, "UI", "class_select.min_damage");

            var passive = FindAnywhere(screenRect, "PassiveLabel");
            var passiveLabel = EnsureTmpLabel(rightPanel, "PassiveLabel", passive, string.Empty,
                24f, new Vector2(0f, -385f), new Vector2(780f, 72f), font, outlineMat, TextColor);
            passiveLabel.enableWordWrapping = true;
            passiveLabel.overflowMode = TextOverflowModes.Ellipsis;

            DestroyChildrenNotIn(rightPanel, "HeaderLabel", "ContractPanel", "FooterLabel", "PassiveLabel");

            // -- ContractDisplayView: una sola instancia, en ContractPanel --
            foreach (var old in screen.GetComponentsInChildren<ContractDisplayView>(true))
            {
                if (old.gameObject != contractPanel.gameObject)
                {
                    var holder = old.gameObject;
                    Object.DestroyImmediate(old);
                    // El holder viejo ("ContractDisplayView" GO) queda vacío tras
                    // migrar hijos — si quedó fuera de los paneles, borrarlo.
                    if (holder != screen.gameObject && holder.transform.parent == screenRect
                        && holder.GetComponents<Component>().Length <= 2 && holder.transform.childCount == 0)
                    {
                        Object.DestroyImmediate(holder);
                    }
                }
            }
            if (!contractPanel.TryGetComponent<ContractDisplayView>(out var contractView))
                contractView = contractPanel.gameObject.AddComponent<ContractDisplayView>();

            var rowPrefab = AssetDatabase.LoadAssetAtPath<ComboRowView>(ComboRowPrefabPath);

            // Fila drawer-style con descripción + settings del arte de dados. Si faltan,
            // la view cae sola al ComboRow legacy (que queda cableado como fallback).
            var contractRowGo = AssetDatabase.LoadAssetAtPath<GameObject>(ClassContractRowPrefabPath);
            var contractRowPrefab = contractRowGo != null
                ? contractRowGo.GetComponent<Rollgeon.UI.HUD.Contract.ContractComboRowView>()
                : null;
            var contractSettings = AssetDatabase
                .LoadAssetAtPath<Rollgeon.UI.HUD.Contract.ContractSheetUiSettingsSO>(ContractSettingsPath);
            if (contractRowPrefab == null || contractSettings == null)
            {
                Debug.LogError("[ClassSelectionSetup] Falta el prefab de fila drawer-style o " +
                               "ContractSheetUiSettings — correr 'Rollgeon/Contract Drawer/1 - Create " +
                               "Settings', '2 - Create Die Prefab' y 'Rollgeon/Class Selection/5 - Setup " +
                               "Contract Row Prefab'. La tabla cae al ComboRow legacy.");
            }

            var cdSo = new SerializedObject(contractView);
            cdSo.FindProperty("_headerLabel").objectReferenceValue = headerLabel;
            cdSo.FindProperty("_rowsContainer").objectReferenceValue = rows;
            cdSo.FindProperty("_rowPrefab").objectReferenceValue = rowPrefab;
            cdSo.FindProperty("_footerLabel").objectReferenceValue = footerLabel;
            cdSo.FindProperty("_contractRowPrefab").objectReferenceValue = contractRowPrefab;
            cdSo.FindProperty("_uiSettings").objectReferenceValue = contractSettings;
            cdSo.ApplyModifiedProperties();

            // -- Confirmar + Atrás (estilo juicy) --
            var confirm = FindAnywhere(screenRect, "ConfirmButton");
            if (confirm == null)
            {
                var go = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
                confirm = (RectTransform)go.transform;
            }
            confirm.SetParent(screenRect, worldPositionStays: false);
            // Par centrado en pantalla: Atrás a la izquierda, Confirmar a la derecha,
            // 40 px de gap entre ambos (mismo criterio que Build Selection).
            Place(confirm, screenRect, new Vector2(120f, -480f), new Vector2(260f, 70f));
            EnsureButtonLabel(confirm, "Confirmar", font, outlineMat);
            LocalizationSetupTools.BindTMP(confirm.GetComponentInChildren<TMP_Text>(true), "UI", "screen.confirm");

            var back = FindAnywhere(screenRect, "BackButton");
            if (back == null)
            {
                var go = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
                back = (RectTransform)go.transform;
            }
            back.SetParent(screenRect, worldPositionStays: false);
            Place(back, screenRect, new Vector2(-150f, -480f), new Vector2(200f, 70f));
            EnsureButtonLabel(back, "Atrás", font, outlineMat);
            LocalizationSetupTools.BindTMP(back.GetComponentInChildren<TMP_Text>(true), "UI", "screen.back");

            var confirmJuicy = EnsureJuicyButton(confirm.GetComponent<Button>(), settings, outlineMat, font);
            var backJuicy = EnsureJuicyButton(back.GetComponent<Button>(), settings, outlineMat, font);
            EnsureGroup(screen.gameObject, new[] { confirmJuicy, backJuicy }, settings);

            // -- Rewire del screen --
            var so = new SerializedObject(screen);
            so.FindProperty("_warriorButton").objectReferenceValue = warrior.Button;
            so.FindProperty("_picaroButton").objectReferenceValue = picaro.Button;
            so.FindProperty("_magoButton").objectReferenceValue = mago.Button;
            so.FindProperty("_confirmButton").objectReferenceValue = confirm.GetComponent<Button>();
            so.FindProperty("_backButton").objectReferenceValue = back.GetComponent<Button>();
            so.FindProperty("_contractDisplay").objectReferenceValue = contractView;
            so.FindProperty("_portraitDisplay").objectReferenceValue = portraitImage;
            so.FindProperty("_passiveDisplay").objectReferenceValue = passiveLabel;
            so.FindProperty("_warriorSelectionIndicator").objectReferenceValue = warrior.Underline.gameObject;
            so.FindProperty("_rootCanvasGroup").objectReferenceValue = rootGroup;
            so.FindProperty("_portraitFrame").objectReferenceValue = portraitFrame;
            so.FindProperty("_contractPanel").objectReferenceValue = contractPanel;

            // Clases bloqueadas via sistema de unlocks (#164): Mago/Pícaro entran a
            // _unlockableClasses con Hero=null (no existe el SO todavía) y ClassId
            // como TargetId. Cuando exista CH_Mage/CH_Rogue, re-correr este paso
            // los engancha solo vía FindHeroByEntityId.
            var unlockables = so.FindProperty("_unlockableClasses");
            unlockables.arraySize = 2;
            WireUnlockableEntry(unlockables.GetArrayElementAtIndex(0), "Mage", mago);
            WireUnlockableEntry(unlockables.GetArrayElementAtIndex(1), "Rogue", picaro);
            so.ApplyModifiedProperties();

            EnsureTooltipCanvas(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ClassSelectionSetup] Pantalla de selección de clase reconstruida según el mockup.");
        }

        private static void WireUnlockableEntry(SerializedProperty element, string classId, ClassEntry entry)
        {
            element.FindPropertyRelative("Hero").objectReferenceValue = FindHeroByEntityId(classId);
            element.FindPropertyRelative("ClassId").stringValue = classId;
            element.FindPropertyRelative("Button").objectReferenceValue = entry.Button;
            element.FindPropertyRelative("SelectionIndicator").objectReferenceValue =
                entry.Underline != null ? entry.Underline.gameObject : null;
            element.FindPropertyRelative("LockIndicator").objectReferenceValue = entry.LockIcon;
        }

        private static Rollgeon.Heroes.ClassHeroSO FindHeroByEntityId(string entityId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ClassHeroSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var hero = AssetDatabase.LoadAssetAtPath<Rollgeon.Heroes.ClassHeroSO>(path);
                if (hero != null && hero.EntityId == entityId) return hero;
            }
            return null;
        }

        /// <summary>
        /// 01_MainMenu no trae Canvas_Tooltip (solo 02_Gameplay lo tiene) — sin el
        /// <see cref="Rollgeon.UI.Tooltips.TooltipController"/> en escena, los
        /// tooltips de clases bloqueadas no-opean en silencio.
        /// </summary>
        private static void EnsureTooltipCanvas(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<Rollgeon.UI.Tooltips.TooltipController>(true) != null)
                    return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipCanvasPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[ClassSelectionSetup] No se encontró " + TooltipCanvasPrefabPath +
                               " — los tooltips de clases bloqueadas no van a mostrarse.");
                return;
            }

            PrefabUtility.InstantiatePrefab(prefab, scene);
            Debug.Log("[ClassSelectionSetup] Canvas_Tooltip instanciado en 01_MainMenu.");
        }

        // ================================================================
        // Class entry builder
        // ================================================================

        private readonly struct ClassEntry
        {
            public readonly Button Button;
            public readonly RectTransform Underline;
            public readonly GameObject LockIcon;

            public ClassEntry(Button button, RectTransform underline, GameObject lockIcon)
            {
                Button = button;
                Underline = underline;
                LockIcon = lockIcon;
            }
        }

        private static ClassEntry EnsureClassEntry(RectTransform searchRoot, RectTransform leftPanel,
            string[] candidateNames, string canonicalName, Vector2 pos, string locKey,
            string iconSpriteName, bool locked, TMP_FontAsset font, Material outlineMat)
        {
            RectTransform entry = null;
            foreach (var candidate in candidateNames)
            {
                entry = FindAnywhere(searchRoot, candidate);
                if (entry != null) break;
            }
            if (entry == null)
            {
                var go = new GameObject(canonicalName, typeof(RectTransform), typeof(Image), typeof(Button));
                entry = (RectTransform)go.transform;
            }
            entry.name = canonicalName;
            entry.SetParent(leftPanel, worldPositionStays: false);
            Place(entry, leftPanel, pos, new Vector2(280f, 110f));

            // Los botones viejos traían LayoutGroups que pisan las posiciones de
            // los hijos (nombre-izquierda/ícono-derecha + underline "colgando") —
            // acá manda el anchoredPosition, fuera todo layout automático.
            StripLayoutComponents(entry.gameObject);

            if (!entry.TryGetComponent<Image>(out var bg)) bg = entry.gameObject.AddComponent<Image>();
            var c = bg.color;
            bg.color = new Color(c.r, c.g, c.b, 0f);
            bg.raycastTarget = true;

            if (!entry.TryGetComponent<Button>(out var button)) button = entry.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bg;

            // Marco del ícono: sheet_8 deseleccionado / sheet_7 seleccionado (el
            // swap lo hace ClassSelectionFrameView en runtime). Sprites nativos,
            // sin 9-slice — son bordes cuadrados a tamaño casi 1:1.
            var iconFrame = EnsureRect(entry, "IconFrame", new Vector2(-90f, 0f), new Vector2(90f, 90f));
            if (!iconFrame.TryGetComponent<Image>(out var frameImage))
                frameImage = iconFrame.gameObject.AddComponent<Image>();
            frameImage.sprite = LoadSprite("UI-Sheet-sheet_8");
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = true;
            frameImage.raycastTarget = false;
            frameImage.color = locked ? LockedColor : Color.white;

            if (!entry.TryGetComponent<ClassSelectionFrameView>(out var frameView))
                frameView = entry.gameObject.AddComponent<ClassSelectionFrameView>();
            var frameSo = new SerializedObject(frameView);
            frameSo.FindProperty("_frame").objectReferenceValue = frameImage;
            frameSo.FindProperty("_selectedSprite").objectReferenceValue = LoadSprite("UI-Sheet-sheet_7");
            frameSo.FindProperty("_deselectedSprite").objectReferenceValue = LoadSprite("UI-Sheet-sheet_8");
            frameSo.ApplyModifiedProperties();

            // Ícono de la clase (sheet ClassIcons). Fallback: si el slice no existe
            // se conserva el sprite que ya tenga la escena — nunca pisar con null,
            // que deja el cuadrado blanco.
            var placeholder = EnsureRect(iconFrame, "IconPlaceholder", Vector2.zero, new Vector2(54f, 54f));
            if (!placeholder.TryGetComponent<Image>(out var placeholderImage))
                placeholderImage = placeholder.gameObject.AddComponent<Image>();
            var iconSprite = LoadClassIcon(iconSpriteName);
            if (iconSprite != null) placeholderImage.sprite = iconSprite;
            placeholderImage.preserveAspect = true;
            placeholderImage.color = locked ? LockedColor : Color.white;
            placeholderImage.raycastTarget = false;

            // Candado de clase bloqueada — hijo de IconFrame a propósito: sobrevive
            // al DestroyChildrenNotIn(entry, ...) del final sin ampliar la whitelist.
            // Badge claro + candado encima: el sprite del candado es negro puro y
            // desaparece sobre el icono oscurecido; el badge le da contraste.
            GameObject lockIcon = null;
            var existingLock = iconFrame.Find("LockIcon");
            if (locked)
            {
                var lockRect = EnsureRect(iconFrame, "LockIcon", new Vector2(27f, -27f), new Vector2(36f, 36f));
                if (!lockRect.TryGetComponent<Image>(out var badgeImage))
                    badgeImage = lockRect.gameObject.AddComponent<Image>();
                badgeImage.sprite = null;
                badgeImage.color = AccentColor;
                badgeImage.raycastTarget = false;

                var glyphRect = EnsureRect(lockRect, "LockGlyph", Vector2.zero, new Vector2(26f, 26f));
                if (!glyphRect.TryGetComponent<Image>(out var glyphImage))
                    glyphImage = glyphRect.gameObject.AddComponent<Image>();
                glyphImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LockSpritePath);
                glyphImage.preserveAspect = true;
                glyphImage.raycastTarget = false;

                lockRect.SetAsLastSibling();
                lockIcon = lockRect.gameObject;
            }
            else if (existingLock != null)
            {
                Object.DestroyImmediate(existingLock.gameObject);
            }

            // Label de la clase.
            var existingLabelRect = entry.Find("NameLabel") as RectTransform;
            TMP_Text existingLabel = existingLabelRect != null ? existingLabelRect.GetComponent<TMP_Text>() : null;
            if (existingLabel == null) existingLabel = entry.GetComponentInChildren<TMP_Text>(true);
            // Mock: [ícono] a la izquierda, nombre a su derecha centrado vertical.
            var nameLabel = EnsureTmpLabel(entry, "NameLabel",
                existingLabel != null ? (RectTransform)existingLabel.transform : null,
                canonicalName, 34f, new Vector2(55f, 0f), new Vector2(170f, 44f), font, outlineMat,
                locked ? LockedColor : TextColor);
            nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            LocalizationSetupTools.BindTMP(nameLabel, "UI", locKey);

            // Underline dorado = indicador de selección, SOLO al seleccionar,
            // mismo ancho pero por debajo de la card de la clase (feedback).
            var underline = EnsureRect(entry, "Underline", new Vector2(35f, -43f), new Vector2(130f, 3f));
            if (!underline.TryGetComponent<Image>(out var underlineImage))
                underlineImage = underline.gameObject.AddComponent<Image>();
            underlineImage.color = AccentColor;
            underlineImage.raycastTarget = false;
            underline.gameObject.SetActive(false);

            // Limpiar hijos legacy (outlines/portraits del layout viejo).
            DestroyChildrenNotIn(entry, "IconFrame", "NameLabel", "Underline");

            return new ClassEntry(button, underline, lockIcon);
        }

        // ================================================================
        // Helpers (copias locales — los de JuicyMenuSetupTools son private)
        // ================================================================

        private static Sprite LoadClassIcon(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(ClassIconsPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogWarning($"[ClassSelectionSetup] Slice '{spriteName}' no encontrado en {ClassIconsPath} — se conserva el sprite actual.");
            return sprite;
        }

        private static Sprite LoadSprite(string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(UiSheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogError($"[ClassSelectionSetup] Slice '{spriteName}' no encontrado en {UiSheetPath}.");
            return sprite;
        }

        private static void SetSliced(Image image, Sprite sprite, float ppuMultiplier)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = ppuMultiplier;
        }

        private static RectTransform EnsureRect(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                rect = (RectTransform)go.transform;
                rect.SetParent(parent, worldPositionStays: false);
            }
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Place(RectTransform rect, RectTransform parent, Vector2 position, Vector2 size)
        {
            if (rect.parent != parent) rect.SetParent(parent, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            if (rect.TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = 1f;
        }

        private static RectTransform FindAnywhere(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name && t != root) as RectTransform;
        }

        private static void StripLayoutComponents(GameObject go)
        {
            foreach (var group in go.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(group);
            foreach (var fitter in go.GetComponents<ContentSizeFitter>())
                Object.DestroyImmediate(fitter);
        }

        private static void DestroyChildrenNotIn(RectTransform parent, params string[] whitelist)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (whitelist.Contains(child.name)) continue;
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static TMP_Text EnsureTmpLabel(RectTransform parent, string name, RectTransform existing,
            string fallbackText, float fontSize, Vector2 position, Vector2 size,
            TMP_FontAsset font, Material outlineMat, Color color)
        {
            RectTransform rect = existing != null ? existing : parent.Find(name) as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                rect = (RectTransform)go.transform;
            }
            rect.name = name;
            rect.SetParent(parent, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;

            var tmp = rect.GetComponent<TMP_Text>();
            if (tmp == null) tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (!string.IsNullOrEmpty(fallbackText)) tmp.text = fallbackText;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            if (font != null) tmp.font = font;
            if (outlineMat != null) tmp.fontSharedMaterial = outlineMat;
            tmp.raycastTarget = false;
            EditorUtility.SetDirty(tmp);
            return tmp;
        }

        private static void TuneLabel(RectTransform rowRect, string name, float width, float height,
            TMP_FontAsset font, Material outlineMat, Color color, TextAlignmentOptions alignment,
            float minSize, float maxSize, bool wrap)
        {
            var rect = rowRect.Find(name) as RectTransform;
            if (rect == null)
            {
                Debug.LogWarning($"[ClassSelectionSetup] '{name}' no encontrado en ComboRow.prefab.");
                return;
            }
            rect.sizeDelta = new Vector2(width, height);

            var layout = rect.GetComponent<LayoutElement>();
            if (layout == null) layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;

            var tmp = rect.GetComponent<TMP_Text>();
            if (tmp == null) return;
            if (font != null) tmp.font = font;
            if (outlineMat != null) tmp.fontSharedMaterial = outlineMat;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;
            tmp.enableWordWrapping = wrap;
            tmp.overflowMode = wrap ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            EditorUtility.SetDirty(tmp);
        }

        private static void SetSibling(RectTransform parent, string childName, int index)
        {
            var child = parent.Find(childName);
            if (child != null) child.SetSiblingIndex(index);
        }

        private static void EnsureButtonLabel(RectTransform buttonRect, string fallbackText,
            TMP_FontAsset font, Material outlineMat)
        {
            var label = buttonRect.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                var rect = (RectTransform)go.transform;
                rect.SetParent(buttonRect, worldPositionStays: false);
                label = go.GetComponent<TMP_Text>();
                label.text = fallbackText;
            }
            if (font != null) label.font = font;
            if (outlineMat != null) label.fontSharedMaterial = outlineMat;
            EditorUtility.SetDirty(label);
        }

        private static JuicyMenuButton EnsureJuicyButton(
            Button button, MenuJuiceSettingsSO settings, Material outlineMat, TMP_FontAsset font)
        {
            var go = button.gameObject;
            if (!go.activeSelf) go.SetActive(true);

            if (go.TryGetComponent<Image>(out var background))
            {
                var c = background.color;
                background.color = new Color(c.r, c.g, c.b, 0f);
                background.raycastTarget = true;
            }
            button.transition = Selectable.Transition.None;

            if (!go.TryGetComponent<CanvasGroup>(out _)) go.AddComponent<CanvasGroup>();

            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                if (font != null) label.font = font;
                label.fontSharedMaterial = outlineMat;
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = Vector2.zero;
                label.alignment = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(label);
            }

            var underlineTransform = go.transform.Find("Underline") as RectTransform;
            if (underlineTransform == null)
            {
                var underlineGo = new GameObject("Underline", typeof(RectTransform), typeof(Image));
                underlineTransform = (RectTransform)underlineGo.transform;
                underlineTransform.SetParent(go.transform, worldPositionStays: false);
            }
            underlineTransform.anchorMin = underlineTransform.anchorMax = new Vector2(0.5f, 0f);
            underlineTransform.pivot = new Vector2(0.5f, 0.5f);
            underlineTransform.anchoredPosition = new Vector2(0f, 12f);
            underlineTransform.sizeDelta = new Vector2(0f, 3f);
            var underlineImage = underlineTransform.GetComponent<Image>();
            underlineImage.color = AccentColor;
            underlineImage.raycastTarget = false;

            if (!go.TryGetComponent<JuicyMenuButton>(out var juicy))
                juicy = go.AddComponent<JuicyMenuButton>();

            var so = new SerializedObject(juicy);
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_underline").objectReferenceValue = underlineTransform;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();

            return juicy;
        }

        private static void EnsureGroup(
            GameObject host, JuicyMenuButton[] buttons, MenuJuiceSettingsSO settings)
        {
            if (!host.TryGetComponent<JuicyMenuGroup>(out var group))
                group = host.AddComponent<JuicyMenuGroup>();

            var so = new SerializedObject(group);
            var array = so.FindProperty("_buttons");
            array.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_playEntranceOnEnable").boolValue = true;
            // El rework del menú renombró _waitForIntro → _introAnimation; null-guard
            // para que un próximo rename no vuelva a abortar el rebuild entero.
            var intro = so.FindProperty("_introAnimation") ?? so.FindProperty("_waitForIntro");
            if (intro != null) intro.objectReferenceValue = null;
            so.ApplyModifiedProperties();
        }
    }
}
