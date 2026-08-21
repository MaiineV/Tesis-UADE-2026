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
            SetupPauseOptionsPanel();
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

            UpsertOptionsLocalization();

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

            // El LanguageSelector viejo vivía en el MainMenuScreen — se muda al
            // panel (BuildOptionsPanel agrega el nuevo). Solo aplica al menú.
            if (screen.TryGetComponent<LanguageSelector>(out var oldSelector))
                Object.DestroyImmediate(oldSelector);

            BuildOptionsPanel((RectTransform)screen.transform.parent, settings, outlineMat);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[JuicyMenuSetup] Panel de opciones cableado en 01_MainMenu.");
        }

        [MenuItem("Rollgeon/Juicy Menu/5 - Setup Pause Options Panel")]
        public static void SetupPauseOptionsPanel()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MenuJuiceSettingsSO>(SettingsPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (settings == null || outlineMat == null)
            {
                Debug.LogError("[JuicyMenuSetup] Correr primero '1 - Create Assets'.");
                return;
            }

            UpsertOptionsLocalization();

            var root = PrefabUtility.LoadPrefabContents(PausePrefabPath);
            try
            {
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas == null)
                {
                    Debug.LogError("[JuicyMenuSetup] Canvas no encontrado en el prefab de pausa.");
                    return;
                }

                // Misma OptionsScreen que el menú: el ScreenHost de 02_Gameplay
                // la descubre como BaseScreen hija (inactivas incluidas) y
                // PauseMenuOverlay la abre con PushOverlay<OptionsScreen>().
                BuildOptionsPanel((RectTransform)canvas.transform, settings, outlineMat);

                PrefabUtility.SaveAsPrefabAsset(root, PausePrefabPath);
                Debug.Log("[JuicyMenuSetup] Panel de opciones cableado en Canvas_PauseMenu.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UpsertOptionsLocalization()
        {
            LocalizationSetupTools.UpsertEntry("UI", "menu.language", "Idioma", "Language");
            LocalizationSetupTools.UpsertEntry("UI", "menu.reset_confirm", "¿Seguro?", "Are you sure?");
            LocalizationSetupTools.UpsertEntry("UI", "menu.back", "Volver", "Back");
            LocalizationSetupTools.UpsertEntry("UI", "menu.game_speed", "Velocidad: x{0}", "Speed: x{0}");
            LocalizationSetupTools.UpsertEntry("UI", "menu.reroll_discard", "Reroll: vuelan los elegidos", "Reroll: rerolls selected dice");
            LocalizationSetupTools.UpsertEntry("UI", "menu.reroll_keep", "Reroll: se quedan los elegidos", "Reroll: keeps selected dice");
            LocalizationSetupTools.UpsertEntry("UI", "menu.tab_general", "General", "General");
            LocalizationSetupTools.UpsertEntry("UI", "menu.tab_audio", "Audio", "Audio");
            LocalizationSetupTools.UpsertEntry("UI", "menu.audio_master", "Master", "Master");
            LocalizationSetupTools.UpsertEntry("UI", "menu.audio_music", "Música", "Music");
            LocalizationSetupTools.UpsertEntry("UI", "menu.audio_sfx", "Efectos", "Sound FX");
            LocalizationSetupTools.UpsertEntry("UI", "menu.audio_muted", "Muteado", "Muted");
            LocalizationSetupTools.UpsertEntry("UI", "menu.audio_unmuted", "Sonando", "Playing");
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Arma (o actualiza) el panel de opciones como hija de <paramref name="canvas"/>.
        /// Botones secundarios find-or-create: en <c>01_MainMenu</c> reparenta los
        /// ex-botones de debug existentes; en el prefab de pausa los crea de cero.
        /// Idempotente en ambos destinos.
        /// </summary>
        private static void BuildOptionsPanel(RectTransform canvas, MenuJuiceSettingsSO settings, Material outlineMat)
        {
            // -- Root del overlay (inactivo: lo activa el ScreenHost al pushearlo) --
            var optionsRect = FindChild(canvas, "OptionsScreen");
            if (optionsRect == null)
            {
                var go = new GameObject("OptionsScreen", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                optionsRect = (RectTransform)go.transform;
                optionsRect.SetParent(canvas, worldPositionStays: false);
            }
            Stretch(optionsRect);
            // Último hermano del canvas: el overlay tiene que renderear por
            // encima de la screen que tapa (menú o pausa).
            optionsRect.SetAsLastSibling();
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
            // 860 de alto desde que entró el header de tabs General|Audio (antes 800).
            panel.sizeDelta = new Vector2(560f, 860f);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0x1F / 255f, 0x23 / 255f, 0x2E / 255f, 0.95f);
            var panelOutline = panel.GetComponent<Outline>();
            panelOutline.effectColor = OutlineColor;
            panelOutline.effectDistance = new Vector2(3f, -3f);

            // -- Título (Text Animator para el <wave> del texto code-set) --
            var title = EnsureTmpLabel(panel, "TitleLabel", "Opciones", 54f, new Vector2(0f, 360f),
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
            divider.anchoredPosition = new Vector2(0f, 322f);
            divider.sizeDelta = new Vector2(300f, 3f);
            var dividerImage = divider.GetComponent<Image>();
            dividerImage.color = AccentColor;
            dividerImage.raycastTarget = false;

            // -- Tabs General | Audio: header común + un container por tab --
            var generalTabButton = EnsureSecondaryButton(canvas, panel, "GeneralTabButton", "General", outlineMat);
            var audioTabButton = EnsureSecondaryButton(canvas, panel, "AudioTabButton", "Audio", outlineMat);
            Place(generalTabButton, panel, new Vector2(-80f, 285f), new Vector2(150f, 50f));
            Place(audioTabButton, panel, new Vector2(80f, 285f), new Vector2(150f, 50f));

            // GeneralTab baja 15px el stack heredado para despejar el header;
            // las filas conservan sus posiciones locales históricas.
            var generalTab = EnsureTabContainer(panel, "GeneralTab", new Vector2(0f, -15f));
            var audioTab = EnsureTabContainer(panel, "AudioTab", Vector2.zero);

            // -- Botones secundarios: reparentar los existentes o crearlos --
            // (los textos definitivos los setea OptionsScreen.RefreshLabels por código)
            var tutorialToggle = EnsureSecondaryButton(canvas, generalTab, "TutorialToggleButton", "Tutorial: ON", outlineMat);
            var analyticsToggle = EnsureSecondaryButton(canvas, generalTab, "AnalyticsToggleButton", "Telemetría: ON", outlineMat);
            var gameSpeed = EnsureSecondaryButton(canvas, generalTab, "GameSpeedButton", "Velocidad: x1", outlineMat);
            var rerollMode = EnsureSecondaryButton(canvas, generalTab, "RerollModeButton", "Reroll: vuelan los elegidos", outlineMat);
            var spanish = EnsureSecondaryButton(canvas, generalTab, "SpanishButton", "Español", outlineMat);
            var english = EnsureSecondaryButton(canvas, generalTab, "EnglishButton", "English", outlineMat);
            var deleteSave = EnsureSecondaryButton(canvas, generalTab, "DeleteProgressButton", "Borrar partida", outlineMat);

            Place(tutorialToggle, generalTab, new Vector2(0f, 230f), new Vector2(300f, 70f));
            Place(analyticsToggle, generalTab, new Vector2(0f, 150f), new Vector2(300f, 70f));
            Place(gameSpeed, generalTab, new Vector2(0f, 70f), new Vector2(300f, 70f));
            Place(rerollMode, generalTab, new Vector2(0f, -10f), new Vector2(300f, 70f));
            Place(spanish, generalTab, new Vector2(-90f, -140f), new Vector2(150f, 60f));
            Place(english, generalTab, new Vector2(90f, -140f), new Vector2(150f, 60f));
            Place(deleteSave, generalTab, new Vector2(0f, -240f), new Vector2(300f, 70f));

            // El LanguageLabel de instalaciones previas vivía suelto en el panel:
            // migrarlo al container antes del find-or-create para no duplicarlo.
            var legacyLanguageLabel = FindAnywhere(canvas, "LanguageLabel");
            if (legacyLanguageLabel != null && legacyLanguageLabel.parent != generalTab)
                legacyLanguageLabel.SetParent(generalTab, worldPositionStays: false);
            var languageLabel = EnsureTmpLabel(generalTab, "LanguageLabel", "Idioma", 30f, new Vector2(0f, -80f),
                new Vector2(300f, 40f), outlineMat, new Color32(0x5F, 0x73, 0x7A, 0xFF));

            // -- Tab de audio: label + slider + mute por canal --
            var mutedGray = new Color32(0x5F, 0x73, 0x7A, 0xFF);
            var masterLabel = EnsureTmpLabel(audioTab, "MasterLabel", "Master", 30f, new Vector2(0f, 195f),
                new Vector2(300f, 40f), outlineMat, mutedGray);
            var masterSlider = EnsureSlider(canvas, audioTab, "MasterVolumeSlider");
            Place((RectTransform)masterSlider.transform, audioTab, new Vector2(0f, 148f), new Vector2(360f, 36f));
            var masterMute = EnsureSecondaryButton(canvas, audioTab, "MasterMuteButton", "Sonando", outlineMat);
            Place(masterMute, audioTab, new Vector2(0f, 95f), new Vector2(200f, 50f));

            var musicLabel = EnsureTmpLabel(audioTab, "MusicLabel", "Música", 30f, new Vector2(0f, 10f),
                new Vector2(300f, 40f), outlineMat, mutedGray);
            var musicSlider = EnsureSlider(canvas, audioTab, "MusicVolumeSlider");
            Place((RectTransform)musicSlider.transform, audioTab, new Vector2(0f, -37f), new Vector2(360f, 36f));
            var musicMute = EnsureSecondaryButton(canvas, audioTab, "MusicMuteButton", "Sonando", outlineMat);
            Place(musicMute, audioTab, new Vector2(0f, -90f), new Vector2(200f, 50f));

            var sfxLabel = EnsureTmpLabel(audioTab, "SfxLabel", "Efectos", 30f, new Vector2(0f, -175f),
                new Vector2(300f, 40f), outlineMat, mutedGray);
            var sfxSlider = EnsureSlider(canvas, audioTab, "SfxVolumeSlider");
            Place((RectTransform)sfxSlider.transform, audioTab, new Vector2(0f, -222f), new Vector2(360f, 36f));
            var sfxMute = EnsureSecondaryButton(canvas, audioTab, "SfxMuteButton", "Sonando", outlineMat);
            Place(sfxMute, audioTab, new Vector2(0f, -275f), new Vector2(200f, 50f));

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
            Place(back, panel, new Vector2(0f, -350f), new Vector2(300f, 70f));

            // -- Juice: mismos efectos que el stack del menú, stagger en cada apertura --
            // Los botones del tab de audio quedan FUERA del group: al abrirse el
            // panel su container está inactivo y el stagger los dejaría en alpha 0.
            var buttons = new[] { generalTabButton, audioTabButton, tutorialToggle, analyticsToggle, gameSpeed, rerollMode, spanish, english, deleteSave, back }
                .Select(rect => rect.GetComponent<Button>()).ToArray();
            var juicyButtons = buttons.Select(b => EnsureJuicyButton(b, settings, outlineMat)).ToArray();
            EnsureGroup(optionsGo, juicyButtons, settings);

            var masterMuteJuice = EnsureJuicyButton(masterMute.GetComponent<Button>(), settings, outlineMat);
            var musicMuteJuice = EnsureJuicyButton(musicMute.GetComponent<Button>(), settings, outlineMat);
            var sfxMuteJuice = EnsureJuicyButton(sfxMute.GetComponent<Button>(), settings, outlineMat);

            // -- LanguageSelector vive en el propio panel --
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
            so.FindProperty("_gameSpeedButton").objectReferenceValue = gameSpeed.GetComponent<Button>();
            so.FindProperty("_gameSpeedLabel").objectReferenceValue = gameSpeed.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_rerollModeButton").objectReferenceValue = rerollMode.GetComponent<Button>();
            so.FindProperty("_rerollModeLabel").objectReferenceValue = rerollMode.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_languageLabel").objectReferenceValue = languageLabel;
            so.FindProperty("_resetSaveButton").objectReferenceValue = deleteSave.GetComponent<Button>();
            so.FindProperty("_resetSaveLabel").objectReferenceValue = deleteSave.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_resetSaveJuice").objectReferenceValue = deleteSave.GetComponent<JuicyMenuButton>();
            so.FindProperty("_backButton").objectReferenceValue = back.GetComponent<Button>();
            so.FindProperty("_backLabel").objectReferenceValue = back.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_generalTabButton").objectReferenceValue = generalTabButton.GetComponent<Button>();
            so.FindProperty("_generalTabLabel").objectReferenceValue = generalTabButton.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_audioTabButton").objectReferenceValue = audioTabButton.GetComponent<Button>();
            so.FindProperty("_audioTabLabel").objectReferenceValue = audioTabButton.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_generalTabRoot").objectReferenceValue = generalTab.gameObject;
            so.FindProperty("_audioTabRoot").objectReferenceValue = audioTab.gameObject;
            so.FindProperty("_masterLabel").objectReferenceValue = masterLabel;
            so.FindProperty("_masterSlider").objectReferenceValue = masterSlider;
            so.FindProperty("_masterMuteButton").objectReferenceValue = masterMute.GetComponent<Button>();
            so.FindProperty("_masterMuteLabel").objectReferenceValue = masterMute.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_masterMuteJuice").objectReferenceValue = masterMuteJuice;
            so.FindProperty("_musicLabel").objectReferenceValue = musicLabel;
            so.FindProperty("_musicSlider").objectReferenceValue = musicSlider;
            so.FindProperty("_musicMuteButton").objectReferenceValue = musicMute.GetComponent<Button>();
            so.FindProperty("_musicMuteLabel").objectReferenceValue = musicMute.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_musicMuteJuice").objectReferenceValue = musicMuteJuice;
            so.FindProperty("_sfxLabel").objectReferenceValue = sfxLabel;
            so.FindProperty("_sfxSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("_sfxMuteButton").objectReferenceValue = sfxMute.GetComponent<Button>();
            so.FindProperty("_sfxMuteLabel").objectReferenceValue = sfxMute.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("_sfxMuteJuice").objectReferenceValue = sfxMuteJuice;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();

            // El tab de audio arranca oculto (OptionsScreen abre en General);
            // dejarlo así en el asset evita el flash de un frame con los dos
            // tabs superpuestos.
            audioTab.gameObject.SetActive(false);

            // El overlay arranca desactivado; ScreenHost lo registra igual
            // (_includeInactive) y PushOverlay lo activa.
            optionsGo.SetActive(false);
        }

        /// <summary>
        /// Busca el botón por nombre bajo <paramref name="searchRoot"/> (para
        /// reparentar los ex-botones de debug del menú) y si no existe lo crea
        /// bajo <paramref name="panel"/> con la estructura mínima que espera
        /// <see cref="EnsureJuicyButton"/>: Image de fondo + Button + label TMP.
        /// </summary>
        private static RectTransform EnsureSecondaryButton(RectTransform searchRoot, RectTransform panel,
            string name, string fallbackText, Material outlineMat)
        {
            var existing = FindAnywhere(searchRoot, name);
            if (existing != null) return existing;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel, worldPositionStays: false);

            var button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            EnsureTmpLabel(rect, "Label", fallbackText, 30f, Vector2.zero, Vector2.zero,
                outlineMat, new Color32(0xE7, 0xE3, 0xE2, 0xFF));
            return rect;
        }

        private static RectTransform FindChild(Transform parent, string name)
        {
            return parent.Find(name) as RectTransform;
        }

        /// <summary>
        /// Container full-stretch para el contenido de un tab. <paramref name="offset"/>
        /// desplaza todo el bloque sin tocar las posiciones locales de las filas.
        /// </summary>
        private static RectTransform EnsureTabContainer(RectTransform panel, string name, Vector2 offset)
        {
            var rect = FindChild(panel, name);
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                rect = (RectTransform)go.transform;
                rect.SetParent(panel, worldPositionStays: false);
            }
            Stretch(rect);
            rect.anchoredPosition = offset;
            return rect;
        }

        /// <summary>
        /// Find-or-create de un slider UGUI con la estructura estándar
        /// Background / Fill Area / Handle Slide Area, en la paleta del panel.
        /// Rango [0, 1] continuo — el valor real lo sincroniza
        /// <see cref="OptionsScreen"/> desde <c>IAudioService</c> al abrir.
        /// </summary>
        private static Slider EnsureSlider(RectTransform searchRoot, RectTransform parent, string name)
        {
            var rect = FindAnywhere(searchRoot, name);
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
                rect = (RectTransform)go.transform;
                rect.SetParent(parent, worldPositionStays: false);
            }

            var background = FindChild(rect, "Background");
            if (background == null)
            {
                var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
                background = (RectTransform)go.transform;
                background.SetParent(rect, worldPositionStays: false);
            }
            Stretch(background);
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0x14 / 255f, 0x17 / 255f, 0x1F / 255f, 1f);
            backgroundImage.raycastTarget = false;

            var fillArea = FindChild(rect, "Fill Area");
            if (fillArea == null)
            {
                var go = new GameObject("Fill Area", typeof(RectTransform));
                fillArea = (RectTransform)go.transform;
                fillArea.SetParent(rect, worldPositionStays: false);
            }
            Stretch(fillArea);
            // Margen para que el fill no asome por detrás del handle en los extremos.
            fillArea.offsetMin = new Vector2(6f, 6f);
            fillArea.offsetMax = new Vector2(-6f, -6f);

            var fill = FindChild(fillArea, "Fill");
            if (fill == null)
            {
                var go = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill = (RectTransform)go.transform;
                fill.SetParent(fillArea, worldPositionStays: false);
            }
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0.5f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = AccentColor;
            fillImage.raycastTarget = false;

            var handleArea = FindChild(rect, "Handle Slide Area");
            if (handleArea == null)
            {
                var go = new GameObject("Handle Slide Area", typeof(RectTransform));
                handleArea = (RectTransform)go.transform;
                handleArea.SetParent(rect, worldPositionStays: false);
            }
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);

            var handle = FindChild(handleArea, "Handle");
            if (handle == null)
            {
                var go = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                handle = (RectTransform)go.transform;
                handle.SetParent(handleArea, worldPositionStays: false);
            }
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(20f, 8f);
            var handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color32(0xE7, 0xE3, 0xE2, 0xFF);

            var slider = rect.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            EditorUtility.SetDirty(slider);
            return slider;
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
                // La fuente va antes que el material (mismo orden que EnsureTmpLabel):
                // sin esto, un label heredado con el default de TMP (LiberationSans)
                // quedaba con atlas y material desparejados.
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (font != null) label.font = font;
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
