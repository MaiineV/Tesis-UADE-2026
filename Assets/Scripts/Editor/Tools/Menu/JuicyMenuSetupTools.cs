using System.Linq;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using Rollgeon.EditorTools.Localization;
using Rollgeon.Localization;
using Rollgeon.UI.Menu;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.Menu
{
    /// <summary>
    /// Installer del "juicy menu" (video de referencia <c>video/menu.mp4</c>):
    /// crea los assets (settings SO + material TMP con outline) y cablea los
    /// componentes en <c>01_MainMenu</c> y <c>Canvas_PauseMenu.prefab</c>.
    /// Idempotente — reejecutar actualiza sin duplicar objetos.
    /// </summary>
    public static class JuicyMenuSetupTools
    {
        private const string SettingsPath = "Assets/Rollgeon/Services/MenuJuiceSettings.asset";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";
        private const string OutlineMaterialPath = "Assets/Fonts/m6x11plus SDF - MenuOutline.mat";
        private const string MainMenuScenePath = "Assets/Scenes/01_MainMenu.unity";
        private const string PausePrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_PauseMenu.prefab";

        private static readonly Color OutlineColor = new Color32(0x5F, 0x73, 0x7A, 0xFF);
        private static readonly Color AccentColor = new Color32(0xE0, 0xC0, 0xA9, 0xFF);
        private static readonly Color PausePanelColor = new Color(0x1F / 255f, 0x23 / 255f, 0x2E / 255f, 0.85f);

        // Stack vertical compacto y bien por debajo del arte del título
        // (ajustado -100px en playtest: el logo baja más de lo que ocupa
        // su rect y pisaba el tope del stack).
        private static readonly float[] StackY = { -70f, -145f, -220f, -295f, -370f };
        private static readonly Vector2 ButtonSize = new Vector2(300f, 75f);

        [MenuItem("Rollgeon/Juicy Menu/Setup All")]
        public static void SetupAll()
        {
            // El panel (4) reparenta los botones secundarios ANTES de que
            // WireIntro (en 2) vacíe _buttonsToFadeIn — así converge en una pasada.
            CreateAssets();
            SetupOptionsPanel();
            SetupMainMenuScene();
            SetupPausePrefab();
        }

        [MenuItem("Rollgeon/Juicy Menu/1 - Create Assets")]
        public static void CreateAssets()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MenuJuiceSettingsSO>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<MenuJuiceSettingsSO>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError("[JuicyMenuSetup] No se encontró " + FontPath);
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (material == null)
            {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            }

            // Preset compartido: el outline vive acá y no en outlineColor por
            // código, que instancia un material por texto (gotcha conocido del
            // FontMigrationTool).
            material.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            material.EnableKeyword("OUTLINE_ON");
            EditorUtility.SetDirty(material);

            AssetDatabase.SaveAssets();
            Debug.Log("[JuicyMenuSetup] Assets listos: settings SO + material outline.");
        }

        [MenuItem("Rollgeon/Juicy Menu/2 - Setup Main Menu Scene")]
        public static void SetupMainMenuScene()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MenuJuiceSettingsSO>(SettingsPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (settings == null || outlineMat == null)
            {
                Debug.LogError("[JuicyMenuSetup] Correr primero '1 - Create Assets'.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene().path == MainMenuScenePath
                ? EditorSceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            var screen = scene.GetRootGameObjects()
                .Select(root => root.GetComponentInChildren<MainMenuScreen>(true))
                .FirstOrDefault(s => s != null);
            if (screen == null)
            {
                Debug.LogError("[JuicyMenuSetup] MainMenuScreen no encontrado en la escena.");
                return;
            }

            var screenSo = new SerializedObject(screen);
            var play = (Button)screenSo.FindProperty("_playButton").objectReferenceValue;
            var cont = (Button)screenSo.FindProperty("_continueButton").objectReferenceValue;
            var quit = (Button)screenSo.FindProperty("_quitButton").objectReferenceValue;
            var unlocks = (Button)screenSo.FindProperty("_unlocksButton").objectReferenceValue;
            var options = (Button)screenSo.FindProperty("_optionsButton").objectReferenceValue;

            if (play == null || quit == null || unlocks == null || cont == null)
            {
                Debug.LogError("[JuicyMenuSetup] Faltan botones cableados en MainMenuScreen.");
                return;
            }

            if (options == null)
            {
                options = CreateOptionsButton(quit);
                screenSo.FindProperty("_optionsButton").objectReferenceValue = options;
                screenSo.ApplyModifiedProperties();
            }

            var stack = new[] { play, cont, options, unlocks, quit };
            for (int i = 0; i < stack.Length; i++)
            {
                var rect = (RectTransform)stack[i].transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, StackY[i]);
                rect.sizeDelta = ButtonSize;
            }

            var juicyButtons = stack.Select(b => EnsureJuicyButton(b, settings, outlineMat)).ToArray();
            var group = EnsureGroup(screen.gameObject, juicyButtons, settings);

            WireIntro(screen.gameObject, group);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[JuicyMenuSetup] 01_MainMenu cableado (5 botones + grupo + rombos).");
        }

        [MenuItem("Rollgeon/Juicy Menu/3 - Setup Pause Prefab")]
        public static void SetupPausePrefab()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MenuJuiceSettingsSO>(SettingsPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (settings == null || outlineMat == null)
            {
                Debug.LogError("[JuicyMenuSetup] Correr primero '1 - Create Assets'.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PausePrefabPath);
            try
            {
                var overlay = root.GetComponentInChildren<PauseMenuOverlay>(true);
                if (overlay == null)
                {
                    Debug.LogError("[JuicyMenuSetup] PauseMenuOverlay no encontrado en el prefab.");
                    return;
                }

                var overlaySo = new SerializedObject(overlay);
                var resume = (Button)overlaySo.FindProperty("_resumeButton").objectReferenceValue;
                var pauseSettings = (Button)overlaySo.FindProperty("_settingsButton").objectReferenceValue;
                var quitRun = (Button)overlaySo.FindProperty("_quitRunButton").objectReferenceValue;
                if (resume == null || pauseSettings == null || quitRun == null)
                {
                    Debug.LogError("[JuicyMenuSetup] Faltan botones cableados en PauseMenuOverlay.");
                    return;
                }

                var stack = new[] { resume, pauseSettings, quitRun };

                // Mismo ritmo compacto que el stack del menú (ajuste de playtest:
                // venían a ~160px y el Settings era 100x100).
                var pauseStackY = new[] { 80f, 0f, -80f };
                for (int i = 0; i < stack.Length; i++)
                {
                    Place((RectTransform)stack[i].transform,
                        (RectTransform)stack[i].transform.parent, new Vector2(0f, pauseStackY[i]),
                        new Vector2(300f, 75f));
                }

                // Por encima del overlay del tutorial (29000): pausar durante un
                // diálogo del tutorial no debe oscurecer los botones de pausa.
                // Sigue debajo del loading screen (31000).
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas != null) canvas.sortingOrder = 29500;

                var juicyButtons = stack.Select(b => EnsureJuicyButton(b, settings, outlineMat)).ToArray();
                EnsureGroup(overlay.gameObject, juicyButtons, settings);

                var panel = overlay.transform.Find("Panel");
                if (panel != null && panel.TryGetComponent<Image>(out var panelImage))
                {
                    panelImage.color = PausePanelColor;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PausePrefabPath);
                Debug.Log("[JuicyMenuSetup] Canvas_PauseMenu cableado (3 botones + grupo + rombos + panel).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Rollgeon/Juicy Menu/4 - Setup Options Panel")]
        public static void SetupOptionsPanel()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MenuJuiceSettingsSO>(SettingsPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (settings == null || outlineMat == null)
            {
                Debug.LogError("[JuicyMenuSetup] Correr primero '1 - Create Assets'.");
                return;
            }

            LocalizationSetupTools.UpsertEntry("UI", "menu.language", "Idioma", "Language");
            LocalizationSetupTools.UpsertEntry("UI", "menu.reset_confirm", "¿Seguro?", "Are you sure?");
            LocalizationSetupTools.UpsertEntry("UI", "menu.back", "Volver", "Back");
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.GetActiveScene().path == MainMenuScenePath
                ? EditorSceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            var screen = scene.GetRootGameObjects()
                .Select(root => root.GetComponentInChildren<MainMenuScreen>(true))
                .FirstOrDefault(s => s != null);
            if (screen == null)
            {
                Debug.LogError("[JuicyMenuSetup] MainMenuScreen no encontrado en la escena.");
                return;
            }

            var canvas = (RectTransform)screen.transform.parent;

            // -- Root del overlay (inactivo: lo activa el ScreenHost al pushearlo) --
            var optionsRect = FindChild(canvas, "OptionsScreen");
            if (optionsRect == null)
            {
                var go = new GameObject("OptionsScreen", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                optionsRect = (RectTransform)go.transform;
                optionsRect.SetParent(canvas, worldPositionStays: false);
            }
            Stretch(optionsRect);
            var scrim = optionsRect.GetComponent<Image>();
            scrim.color = new Color(0x1F / 255f, 0x23 / 255f, 0x2E / 255f, 0.72f);
            scrim.raycastTarget = true;
            var optionsGo = optionsRect.gameObject;

            // -- Panel --
            var panel = FindChild(optionsRect, "Panel");
            if (panel == null)
            {
                var go = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
                panel = (RectTransform)go.transform;
                panel.SetParent(optionsRect, worldPositionStays: false);
            }
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(560f, 640f);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0x1F / 255f, 0x23 / 255f, 0x2E / 255f, 0.95f);
            var panelOutline = panel.GetComponent<Outline>();
            panelOutline.effectColor = OutlineColor;
            panelOutline.effectDistance = new Vector2(3f, -3f);

            // -- Título (Text Animator para el <wave> del texto code-set) --
            var title = EnsureTmpLabel(panel, "TitleLabel", "Opciones", 54f, new Vector2(0f, 250f),
                new Vector2(500f, 70f), outlineMat, new Color32(0xE7, 0xE3, 0xE2, 0xFF));
            if (title.GetComponent<TextAnimator_TMP>() == null)
                title.gameObject.AddComponent<TextAnimator_TMP>();

            var divider = FindChild(panel, "TitleDivider");
            if (divider == null)
            {
                var go = new GameObject("TitleDivider", typeof(RectTransform), typeof(Image));
                divider = (RectTransform)go.transform;
                divider.SetParent(panel, worldPositionStays: false);
            }
            divider.anchorMin = divider.anchorMax = new Vector2(0.5f, 0.5f);
            divider.pivot = new Vector2(0.5f, 0.5f);
            divider.anchoredPosition = new Vector2(0f, 210f);
            divider.sizeDelta = new Vector2(300f, 3f);
            var dividerImage = divider.GetComponent<Image>();
            dividerImage.color = AccentColor;
            dividerImage.raycastTarget = false;

            // -- Reparentar los ex-botones de debug del menú --
            var tutorialToggle = FindAnywhere(canvas, "TutorialToggleButton");
            var analyticsToggle = FindAnywhere(canvas, "AnalyticsToggleButton");
            var spanish = FindAnywhere(canvas, "SpanishButton");
            var english = FindAnywhere(canvas, "EnglishButton");
            var deleteSave = FindAnywhere(canvas, "DeleteProgressButton");
            if (tutorialToggle == null || analyticsToggle == null || spanish == null
                || english == null || deleteSave == null)
            {
                Debug.LogError("[JuicyMenuSetup] Falta alguno de los botones secundarios en la escena.");
                return;
            }

            Place(tutorialToggle, panel, new Vector2(0f, 150f), new Vector2(300f, 70f));
            Place(analyticsToggle, panel, new Vector2(0f, 70f), new Vector2(300f, 70f));
            Place(spanish, panel, new Vector2(-90f, -60f), new Vector2(150f, 60f));
            Place(english, panel, new Vector2(90f, -60f), new Vector2(150f, 60f));
            Place(deleteSave, panel, new Vector2(0f, -160f), new Vector2(300f, 70f));

            var languageLabel = EnsureTmpLabel(panel, "LanguageLabel", "Idioma", 30f, new Vector2(0f, 0f),
                new Vector2(300f, 40f), outlineMat, new Color32(0x5F, 0x73, 0x7A, 0xFF));

            // -- Botón Volver (clon del delete para heredar estructura/label) --
            var back = FindChild(panel, "BackButton");
            if (back == null)
            {
                var go = Object.Instantiate(deleteSave.gameObject, panel);
                go.name = "BackButton";
                back = (RectTransform)go.transform;
                var backLocalize = go.GetComponentInChildren<LocalizeStringEvent>(true);
                if (backLocalize != null) backLocalize.StringReference.SetReference("UI", "menu.back");
                var backText = go.GetComponentInChildren<TMP_Text>(true);
                if (backText != null) backText.text = "Volver";
            }
            Place(back, panel, new Vector2(0f, -240f), new Vector2(300f, 70f));

            // -- Juice: mismos efectos que el stack del menú, stagger en cada apertura --
            var buttons = new[] { tutorialToggle, analyticsToggle, spanish, english, deleteSave, back }
                .Select(rect => rect.GetComponent<Button>()).ToArray();
            var juicyButtons = buttons.Select(b => EnsureJuicyButton(b, settings, outlineMat)).ToArray();
            EnsureGroup(optionsGo, juicyButtons, settings);

            // -- LanguageSelector se muda del MainMenuScreen al panel --
            if (screen.TryGetComponent<LanguageSelector>(out var oldSelector))
                Object.DestroyImmediate(oldSelector);
            if (!optionsGo.TryGetComponent<LanguageSelector>(out var selector))
                selector = optionsGo.AddComponent<LanguageSelector>();
            var selectorSo = new SerializedObject(selector);
            selectorSo.FindProperty("_spanishButton").objectReferenceValue = spanish.GetComponent<Button>();
            selectorSo.FindProperty("_englishButton").objectReferenceValue = english.GetComponent<Button>();
            selectorSo.ApplyModifiedProperties();

            // -- OptionsScreen component + wiring --
            if (!optionsGo.TryGetComponent<OptionsScreen>(out var options))
                options = optionsGo.AddComponent<OptionsScreen>();
            var so = new SerializedObject(options);
            so.FindProperty("_titleLabel").objectReferenceValue = title;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_rootCanvasGroup").objectReferenceValue = optionsGo.GetComponent<CanvasGroup>();
            so.FindProperty("_tutorialToggleButton").objectReferenceValue = tutorialToggle.GetComponent<Button>();
            so.FindProperty("_tutorialToggleLabel").objectReferenceValue = tutorialToggle.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_analyticsToggleButton").objectReferenceValue = analyticsToggle.GetComponent<Button>();
            so.FindProperty("_analyticsToggleLabel").objectReferenceValue = analyticsToggle.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_languageLabel").objectReferenceValue = languageLabel;
            so.FindProperty("_resetSaveButton").objectReferenceValue = deleteSave.GetComponent<Button>();
            so.FindProperty("_resetSaveLabel").objectReferenceValue = deleteSave.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_resetSaveJuice").objectReferenceValue = deleteSave.GetComponent<JuicyMenuButton>();
            so.FindProperty("_backButton").objectReferenceValue = back.GetComponent<Button>();
            so.FindProperty("_backLabel").objectReferenceValue = back.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();

            // El overlay arranca desactivado; ScreenHost lo registra igual
            // (_includeInactive) y PushOverlay lo activa.
            optionsGo.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[JuicyMenuSetup] Panel de opciones cableado (6 botones + grupo + título animado).");
        }

        private static RectTransform FindChild(Transform parent, string name)
        {
            return parent.Find(name) as RectTransform;
        }

        private static RectTransform FindAnywhere(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name) as RectTransform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void Place(RectTransform rect, RectTransform parent, Vector2 position, Vector2 size)
        {
            if (rect.parent != parent) rect.SetParent(parent, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            if (rect.TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = 1f;
        }

        private static TMP_Text EnsureTmpLabel(RectTransform parent, string name, string fallbackText,
            float fontSize, Vector2 position, Vector2 size, Material outlineMat, Color color)
        {
            var rect = FindChild(parent, name);
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                rect = (RectTransform)go.transform;
                rect.SetParent(parent, worldPositionStays: false);
            }
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var tmp = rect.GetComponent<TMP_Text>();
            tmp.text = fallbackText;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;

            // La fuente va antes que el material: el preset de outline pertenece
            // al atlas de m6x11plus, no al default de TMP.
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) tmp.font = font;
            tmp.fontSharedMaterial = outlineMat;
            tmp.raycastTarget = false;
            EditorUtility.SetDirty(tmp);
            return tmp;
        }

        private static Button CreateOptionsButton(Button template)
        {
            var go = Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = "OptionsButton";

            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "Opciones";

            // Reusa la entry del settings de pausa ("Opciones"/"Settings") en
            // vez de crear una key nueva para el mismo texto.
            var localize = go.GetComponentInChildren<LocalizeStringEvent>(true);
            if (localize != null) localize.StringReference.SetReference("UI", "pause.settings");

            return go.GetComponent<Button>();
        }

        private static JuicyMenuButton EnsureJuicyButton(
            Button button, MenuJuiceSettingsSO settings, Material outlineMat)
        {
            var go = button.gameObject;

            // Un botón del stack juicy tiene que estar activo — el Settings de
            // pausa venía desactivado de su época de stub y nunca se veía.
            if (!go.activeSelf) go.SetActive(true);

            // Look texto-only del video: el fondo se vuelve invisible pero
            // sigue siendo el raycast target del hover/click.
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
                label.fontSharedMaterial = outlineMat;

                // El label llena el botón y centra el texto: rects/alineaciones
                // heredados de layouts viejos (ej. el Settings de pausa era un
                // ícono 100x100 con texto arriba-izquierda) desalinean el texto,
                // el subrayado y los rombos, que se centran en el rect.
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

        private static JuicyMenuGroup EnsureGroup(
            GameObject host, JuicyMenuButton[] buttons, MenuJuiceSettingsSO settings)
        {
            if (!host.TryGetComponent<JuicyMenuGroup>(out var group))
                group = host.AddComponent<JuicyMenuGroup>();

            var left = EnsureDiamond(host.transform, "MenuDiamondL");
            var right = EnsureDiamond(host.transform, "MenuDiamondR");

            var so = new SerializedObject(group);
            var array = so.FindProperty("_buttons");
            array.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            so.FindProperty("_leftDiamond").objectReferenceValue = left;
            so.FindProperty("_rightDiamond").objectReferenceValue = right;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();

            return group;
        }

        private static RectTransform EnsureDiamond(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                existing = (RectTransform)go.transform;
                existing.SetParent(parent, worldPositionStays: false);
            }
            existing.SetAsLastSibling();
            existing.sizeDelta = new Vector2(16f, 16f);
            existing.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var image = existing.GetComponent<Image>();
            image.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0f);
            image.raycastTarget = false;
            return existing;
        }

        private static void WireIntro(GameObject screenGo, JuicyMenuGroup group)
        {
            var intro = screenGo.GetComponentInChildren<MainMenuIntroAnimation>(true);
            if (intro == null)
            {
                Debug.LogWarning("[JuicyMenuSetup] MainMenuIntroAnimation no encontrado — " +
                                 "el grupo entra sin delay de intro.");
                return;
            }

            var introSo = new SerializedObject(intro);

            // La entrada staggered reemplaza el fade sincronizado de los
            // primarios; los CanvasGroups de botones secundarios quedan.
            // Los primarios entran con el stagger del grupo y los secundarios
            // viven ahora dentro del panel de opciones: el fade del intro no
            // aplica a ninguno.
            introSo.FindProperty("_buttonsToFadeIn").arraySize = 0;

            // La entrada se ancla al push del logo (leído del intro serializado
            // para resistir retunes) más un respiro extra pedido en playtest.
            const float extraDelayAfterLogoRise = 8f;
            float logoRise = introSo.FindProperty("_tituloPushDelay").floatValue;
            introSo.ApplyModifiedProperties();

            var groupSo = new SerializedObject(group);
            groupSo.FindProperty("_waitForIntro").objectReferenceValue = intro.gameObject;
            groupSo.FindProperty("_introDelay").floatValue = logoRise + extraDelayAfterLogoRise;
            groupSo.FindProperty("_playEntranceOnEnable").boolValue = true;
            groupSo.ApplyModifiedProperties();
        }
    }
}
