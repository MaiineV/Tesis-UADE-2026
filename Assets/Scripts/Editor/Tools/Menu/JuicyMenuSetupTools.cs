using System.Collections.Generic;
using System.Linq;
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

        // Stack vertical centrado alrededor de la banda que ocupaban los 4
        // botones originales (68 .. -232).
        private static readonly float[] StackY = { 118f, 18f, -82f, -182f, -282f };
        private static readonly Vector2 ButtonSize = new Vector2(300f, 75f);

        [MenuItem("Rollgeon/Juicy Menu/Setup All")]
        public static void SetupAll()
        {
            CreateAssets();
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

            WireIntro(screen.gameObject, group, stack);

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

        private static void WireIntro(GameObject screenGo, JuicyMenuGroup group, Button[] primaryButtons)
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
            var primaryGos = new HashSet<GameObject>(primaryButtons.Select(b => b.gameObject));
            var fadeArray = introSo.FindProperty("_buttonsToFadeIn");
            for (int i = fadeArray.arraySize - 1; i >= 0; i--)
            {
                var element = fadeArray.GetArrayElementAtIndex(i);
                var cg = element.objectReferenceValue as CanvasGroup;
                if (cg == null || !primaryGos.Contains(cg.gameObject)) continue;
                element.objectReferenceValue = null;
                fadeArray.DeleteArrayElementAtIndex(i);
            }

            float fadeDelay = introSo.FindProperty("_buttonsFadeDelay").floatValue;
            introSo.ApplyModifiedProperties();

            var groupSo = new SerializedObject(group);
            groupSo.FindProperty("_waitForIntro").objectReferenceValue = intro.gameObject;
            groupSo.FindProperty("_introDelay").floatValue = fadeDelay;
            groupSo.FindProperty("_playEntranceOnEnable").boolValue = true;
            groupSo.ApplyModifiedProperties();
        }
    }
}
