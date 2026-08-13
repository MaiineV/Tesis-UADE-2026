using System.Linq;
using Rollgeon.EditorTools.Localization;
using Rollgeon.UI.Menu;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.Menu
{
    /// <summary>
    /// Installer de la pantalla de armado de bolsa (mock
    /// <c>SELECCION_DADOS.jpeg</c>): settings de sprites de dados, rebuild del
    /// prefab de fila del pool y del layout de la screen en <c>01_MainMenu</c>.
    /// Idempotente — reejecutar converge sin duplicar.
    /// </summary>
    public static class BuildSelectionSetupTools
    {
        private const string DicesSheetPath = "Assets/Art/UI/Dices/Dices.png";
        private const string UiSheetPath = "Assets/Art/UI/UI-Sheet-sheet.png";
        private const string SettingsPath = "Assets/Rollgeon/Services/DiceBuildUiSettings.asset";
        private const string RowPrefabPath = "Assets/Prefabs/UI/PoolOfferingRow.prefab";
        private const string MainMenuScenePath = "Assets/Scenes/01_MainMenu.unity";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";
        private const string OutlineMaterialPath = "Assets/Fonts/m6x11plus SDF - MenuOutline.mat";
        private const string JuiceSettingsPath = "Assets/Rollgeon/Services/MenuJuiceSettings.asset";

        private static readonly Color TextColor = new Color32(0xE7, 0xE3, 0xE2, 0xFF);
        private static readonly Color CounterColor = new Color32(0x8A, 0x96, 0xA0, 0xFF);
        private static readonly Color DividerColor = new Color32(0x3A, 0x45, 0x52, 0xFF);

        [MenuItem("Rollgeon/Build Selection/Setup All")]
        public static void SetupAll()
        {
            CreateSettings();
            SetupRowPrefab();
            RebuildScreenLayout();
        }

        // ================================================================
        // 1 - Settings con los sprites elegidos por el usuario
        // ================================================================

        [MenuItem("Rollgeon/Build Selection/1 - Create Settings")]
        public static void CreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DiceBuildUiSettingsSO>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DiceBuildUiSettingsSO>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.D4 = LoadSpriteOrError(DicesSheetPath, "Dices_2");
            settings.D6 = LoadSpriteOrError(DicesSheetPath, "Dices_6");
            settings.D8 = LoadSpriteOrError(DicesSheetPath, "Dices_11");
            settings.D10 = LoadSpriteOrError(DicesSheetPath, "Dices_16");
            settings.D12 = LoadSpriteOrError(DicesSheetPath, "Dices_21");
            settings.D20 = LoadSpriteOrError(DicesSheetPath, "Dices_26");

            if (settings.D4 == null || settings.D6 == null || settings.D8 == null
                || settings.D10 == null || settings.D12 == null || settings.D20 == null)
            {
                Debug.LogError("[BuildSelectionSetup] Faltan slices en Dices.png — abortando asignación.");
                return;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildSelectionSetup] DiceBuildUiSettings listo con los 6 sprites.");
        }

        // ================================================================
        // 2 - Prefab de la fila del pool
        // ================================================================

        [MenuItem("Rollgeon/Build Selection/2 - Setup PoolOfferingRow Prefab")]
        public static void SetupRowPrefab()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DiceBuildUiSettingsSO>(SettingsPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (settings == null || font == null || outlineMat == null)
            {
                Debug.LogError("[BuildSelectionSetup] Correr primero '1 - Create Settings' (y los installers del menú).");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(RowPrefabPath);
            try
            {
                var row = root.GetComponent<PoolOfferingRow>();
                if (row == null)
                {
                    Debug.LogError("[BuildSelectionSetup] PoolOfferingRow no encontrado en el prefab.");
                    return;
                }

                var rowRect = (RectTransform)root.transform;
                StripLayoutComponents(root);
                // 6 tipos tienen que entrar en la columna — pitch compacto.
                rowRect.sizeDelta = new Vector2(520f, 92f);

                // Fondo transparente (el mock no tiene fill por fila).
                if (root.TryGetComponent<Image>(out var rowBg))
                {
                    var c = rowBg.color;
                    rowBg.color = new Color(c.r, c.g, c.b, 0f);
                    rowBg.raycastTarget = false;
                }

                if (!root.TryGetComponent<CanvasGroup>(out var rowGroup))
                    rowGroup = root.AddComponent<CanvasGroup>();

                // -- Dado clickeable (el viejo AddButton se reemplaza) --
                var dieButtonRect = rowRect.Find("DieButton") as RectTransform;
                if (dieButtonRect == null)
                {
                    var go = new GameObject("DieButton", typeof(RectTransform), typeof(Image), typeof(Button));
                    dieButtonRect = (RectTransform)go.transform;
                    dieButtonRect.SetParent(rowRect, worldPositionStays: false);
                }
                dieButtonRect.anchorMin = dieButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
                dieButtonRect.pivot = new Vector2(0.5f, 0.5f);
                dieButtonRect.anchoredPosition = new Vector2(-200f, 4f);
                dieButtonRect.sizeDelta = new Vector2(72f, 72f);
                var dieImage = dieButtonRect.GetComponent<Image>();
                dieImage.sprite = settings.D4; // placeholder — Bind() setea el real
                dieImage.preserveAspect = true;
                dieImage.raycastTarget = true;
                var dieButton = dieButtonRect.GetComponent<Button>();
                dieButton.transition = Selectable.Transition.None;
                dieButton.targetGraphic = dieImage;

                // -- Labels --
                var typeLabel = EnsureTmpLabel(rowRect, "TypeLabel", null, "D4", 32f,
                    new Vector2(-60f, 4f), new Vector2(120f, 44f), font, outlineMat, TextColor);
                typeLabel.alignment = TextAlignmentOptions.MidlineLeft;

                var countLabel = EnsureTmpLabel(rowRect, "CountLabel", null, "0 / 5", 26f,
                    new Vector2(110f, 4f), new Vector2(140f, 40f), font, outlineMat, CounterColor);
                countLabel.alignment = TextAlignmentOptions.MidlineLeft;

                // -- Divider inferior --
                var divider = rowRect.Find("Divider") as RectTransform;
                if (divider == null)
                {
                    var go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
                    divider = (RectTransform)go.transform;
                    divider.SetParent(rowRect, worldPositionStays: false);
                }
                divider.anchorMin = divider.anchorMax = new Vector2(0.5f, 0f);
                divider.pivot = new Vector2(0.5f, 0.5f);
                divider.anchoredPosition = new Vector2(0f, 0f);
                divider.sizeDelta = new Vector2(470f, 2f);
                var dividerImage = divider.GetComponent<Image>();
                dividerImage.color = DividerColor;
                dividerImage.raycastTarget = false;

                // -- Borrar los botones +/X viejos --
                foreach (var legacy in new[] { "AddButton", "RemoveButton" })
                {
                    var t = rowRect.Find(legacy);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }

                // -- Wiring --
                var so = new SerializedObject(row);
                so.FindProperty("_typeLabel").objectReferenceValue = typeLabel;
                so.FindProperty("_countLabel").objectReferenceValue = countLabel;
                so.FindProperty("_addButton").objectReferenceValue = dieButton;
                so.FindProperty("_removeButton").objectReferenceValue = null;
                so.FindProperty("_dieImage").objectReferenceValue = dieImage;
                so.FindProperty("_divider").objectReferenceValue = divider;
                so.FindProperty("_rowGroup").objectReferenceValue = rowGroup;
                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
                Debug.Log("[BuildSelectionSetup] PoolOfferingRow.prefab reconstruido (dado clickeable + divider).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ================================================================
        // 3 - Layout de la screen
        // ================================================================

        [MenuItem("Rollgeon/Build Selection/3 - Rebuild Screen Layout")]
        public static void RebuildScreenLayout()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DiceBuildUiSettingsSO>(SettingsPath);
            var juice = AssetDatabase.LoadAssetAtPath<MenuJuiceSettingsSO>(JuiceSettingsPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (settings == null || juice == null || font == null || outlineMat == null)
            {
                Debug.LogError("[BuildSelectionSetup] Faltan settings/juice/font/material — correr los installers previos.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene().path == MainMenuScenePath
                ? EditorSceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            var screen = scene.GetRootGameObjects()
                .Select(r => r.GetComponentInChildren<BuildSelectionScreen>(true))
                .FirstOrDefault(s => s != null);
            if (screen == null)
            {
                Debug.LogError("[BuildSelectionSetup] BuildSelectionScreen no encontrado en la escena.");
                return;
            }

            var screenRect = (RectTransform)screen.transform;

            // -- Contador de bolsa "5/5" arriba-izquierda --
            var bagCounter = FindAnywhere(screenRect, "BagCounter");
            var bagCounterLabel = EnsureTmpLabel(screenRect, "BagCounter", bagCounter, "0 / 5", 48f,
                new Vector2(-755f, 445f), new Vector2(220f, 60f), font, outlineMat, TextColor);
            bagCounterLabel.alignment = TextAlignmentOptions.MidlineLeft;

            // -- LIMPIAR (juicy) --
            var clear = FindAnywhere(screenRect, "ClearBag");
            if (clear == null)
            {
                var go = new GameObject("ClearBag", typeof(RectTransform), typeof(Image), typeof(Button));
                clear = (RectTransform)go.transform;
            }
            clear.SetParent(screenRect, worldPositionStays: false);
            StripLayoutComponents(clear.gameObject);
            Place(clear, screenRect, new Vector2(-560f, 445f), new Vector2(220f, 60f));
            EnsureButtonLabel(clear, "Limpiar", font, outlineMat);
            LocalizationSetupTools.BindTMP(clear.GetComponentInChildren<TMP_Text>(true), "UI", "build.clear_bag");

            // -- Header: nombre del héroe + ícono enmarcado --
            var heroName = FindAnywhere(screenRect, "HeroNameLabel");
            var heroNameLabel = EnsureTmpLabel(screenRect, "HeroNameLabel", heroName, "Guerrero", 40f,
                new Vector2(-160f, 435f), new Vector2(360f, 60f), font, outlineMat, TextColor);
            heroNameLabel.alignment = TextAlignmentOptions.MidlineRight;

            var heroFrame = EnsureRect(screenRect, "HeroIconFrame", new Vector2(80f, 435f), new Vector2(92f, 92f));
            if (!heroFrame.TryGetComponent<Image>(out var heroFrameImage))
                heroFrameImage = heroFrame.gameObject.AddComponent<Image>();
            heroFrameImage.sprite = LoadSpriteOrError(UiSheetPath, "UI-Sheet-sheet_7");
            heroFrameImage.type = Image.Type.Simple;
            heroFrameImage.preserveAspect = true;
            heroFrameImage.raycastTarget = false;

            var heroPortrait = FindAnywhere(screenRect, "HeroPortrait");
            if (heroPortrait == null)
            {
                var go = new GameObject("HeroPortrait", typeof(RectTransform), typeof(Image));
                heroPortrait = (RectTransform)go.transform;
            }
            heroPortrait.SetParent(heroFrame, worldPositionStays: false);
            Place(heroPortrait, heroFrame, Vector2.zero, new Vector2(70f, 70f));
            var heroPortraitImage = heroPortrait.GetComponent<Image>();
            heroPortraitImage.preserveAspect = true;
            heroPortraitImage.raycastTarget = false;

            // -- HeroDescription: no está en el mock --
            var heroDesc = FindAnywhere(screenRect, "HeroDescriptionLabel");
            if (heroDesc != null) heroDesc.gameObject.SetActive(false);

            // -- Pool a la izquierda --
            var poolContainer = FindAnywhere(screenRect, "PoolOfferingContainer");
            if (poolContainer == null)
            {
                var go = new GameObject("PoolOfferingContainer", typeof(RectTransform));
                poolContainer = (RectTransform)go.transform;
            }
            poolContainer.SetParent(screenRect, worldPositionStays: false);
            Place(poolContainer, screenRect, new Vector2(-650f, -80f), new Vector2(520f, 600f));
            StripLayoutComponents(poolContainer.gameObject);
            var vlg = poolContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            // -- Tira de la bolsa --
            var strip = EnsureRect(screenRect, "DiceStrip", new Vector2(250f, -20f), new Vector2(900f, 140f));
            if (!strip.TryGetComponent<DiceStripView>(out var stripView))
                stripView = strip.gameObject.AddComponent<DiceStripView>();

            // -- Fallback legacy --
            var fallback = FindAnywhere(screenRect, "DiceBagFallbackLabel");
            if (fallback != null)
            {
                var fallbackLabel = fallback.GetComponent<TMP_Text>();
                if (fallbackLabel != null)
                    LocalizationSetupTools.BindTMP(fallbackLabel, "UI", "build.no_dice_bag");
            }

            // -- Confirmar + Atrás (juicy) --
            var confirm = FindAnywhere(screenRect, "ConfirmButton");
            if (confirm == null)
            {
                var go = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
                confirm = (RectTransform)go.transform;
            }
            confirm.SetParent(screenRect, worldPositionStays: false);
            StripLayoutComponents(confirm.gameObject);
            Place(confirm, screenRect, new Vector2(0f, -455f), new Vector2(280f, 70f));
            EnsureButtonLabel(confirm, "Confirmar", font, outlineMat);
            LocalizationSetupTools.BindTMP(confirm.GetComponentInChildren<TMP_Text>(true), "UI", "screen.confirm");

            var back = FindAnywhere(screenRect, "BackButton");
            if (back == null)
            {
                var go = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
                back = (RectTransform)go.transform;
            }
            back.SetParent(screenRect, worldPositionStays: false);
            StripLayoutComponents(back.gameObject);
            Place(back, screenRect, new Vector2(285f, -455f), new Vector2(200f, 70f));
            EnsureButtonLabel(back, "Atrás", font, outlineMat);
            LocalizationSetupTools.BindTMP(back.GetComponentInChildren<TMP_Text>(true), "UI", "screen.back");

            // -- Botón de ayuda "?" arriba-derecha --
            // A propósito FUERA del JuicyMenuGroup: no es una opción de menú, es un
            // afordance de esquina. Metido en el grupo entraría con el stagger (y con
            // la lógica de rombos), que es justo lo que la guía necesita esperar.
            var help = FindAnywhere(screenRect, "HelpButton");
            if (help == null)
            {
                var go = new GameObject("HelpButton", typeof(RectTransform), typeof(Image), typeof(Button));
                help = (RectTransform)go.transform;
            }
            help.SetParent(screenRect, worldPositionStays: false);
            StripLayoutComponents(help.gameObject);
            Place(help, screenRect, new Vector2(890f, 445f), new Vector2(64f, 64f));
            EnsureButtonLabel(help, "?", font, outlineMat);

            var clearJuicy = EnsureJuicyButton(clear.GetComponent<Button>(), juice, outlineMat, font);
            var confirmJuicy = EnsureJuicyButton(confirm.GetComponent<Button>(), juice, outlineMat, font);
            var backJuicy = EnsureJuicyButton(back.GetComponent<Button>(), juice, outlineMat, font);
            EnsureGroup(screen.gameObject, new[] { clearJuicy, confirmJuicy, backJuicy }, juice);

            // -- Rewire de la screen --
            var so = new SerializedObject(screen);
            so.FindProperty("_heroNameLabel").objectReferenceValue = heroNameLabel;
            so.FindProperty("_heroPortrait").objectReferenceValue = heroPortraitImage;
            so.FindProperty("_bagCounterLabel").objectReferenceValue = bagCounterLabel;
            so.FindProperty("_clearBagButton").objectReferenceValue = clear.GetComponent<Button>();
            so.FindProperty("_confirmButton").objectReferenceValue = confirm.GetComponent<Button>();
            so.FindProperty("_backButton").objectReferenceValue = back.GetComponent<Button>();
            so.FindProperty("_poolOfferingsContainer").objectReferenceValue = poolContainer;
            so.FindProperty("_poolOfferingPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<PoolOfferingRow>(RowPrefabPath);
            so.FindProperty("_diceStrip").objectReferenceValue = stripView;
            so.FindProperty("_diceUiSettings").objectReferenceValue = settings;
            so.FindProperty("_helpButton").objectReferenceValue = help.GetComponent<Button>();
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BuildSelectionSetup] Pantalla de armado de bolsa reconstruida según el mock.");
        }

        // ================================================================
        // Helpers (convención ClassSelectionSetupTools)
        // ================================================================

        private static Sprite LoadSpriteOrError(string assetPath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogError($"[BuildSelectionSetup] Slice '{spriteName}' no encontrado en {assetPath}.");
            return sprite;
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
            // Labels legacy venían chicos — tamaño de botón de menú.
            label.enableAutoSizing = false;
            label.fontSize = 32f;
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
            underlineImage.color = new Color32(0xE0, 0xC0, 0xA9, 0xFF);
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
