using System.Linq;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using Rollgeon.EditorTools.Localization;
using Rollgeon.UI.Menu;
using Rollgeon.UI.Screens;
using Rollgeon.UI.Unlocks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.Menu
{
    /// <summary>
    /// Installer de la pantalla de desbloqueos (#164): restyle de la escena armada a mano
    /// en <c>01_MainMenu</c> — fondo con marco 9-slice, título con Text Animator, cards
    /// con el slice de fila del drawer y botón de volver juicy. Idempotente — reejecutar
    /// converge sin duplicar.
    /// </summary>
    public static class UnlocksSetupTools
    {
        private const string UiSheetPath = "Assets/Art/UI/UI-Sheet-sheet.png";
        private const string DrawerSheetPath = "Assets/Art/UI/UI-sheet.png";
        private const string EntryPrefabPath = "Assets/Prefabs/UI/UnlockEntry.prefab";
        private const string MainMenuScenePath = "Assets/Scenes/01_MainMenu.unity";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";
        private const string OutlineMaterialPath = "Assets/Fonts/m6x11plus SDF - MenuOutline.mat";
        private const string JuiceSettingsPath = "Assets/Rollgeon/Services/MenuJuiceSettings.asset";
        private const string LockSpritePath = "Assets/Art/UI/Unlocks/LockImage.png";

        // Misma paleta que Class Selection (los installers copian, no comparten — ver
        // nota de helpers en ClassSelectionSetupTools).
        private static readonly Color AccentColor = new Color32(0xE0, 0xC0, 0xA9, 0xFF);
        private static readonly Color TextColor = new Color32(0xE7, 0xE3, 0xE2, 0xFF);
        // Tinta oscura para el cuerpo: la card (UI-sheet_9) es naranja y los grises
        // claros no contrastan sobre ella (mismo criterio que la fila del contrato).
        private static readonly Color BodyColor = new Color32(0x3A, 0x2E, 0x24, 0xFF);
        private static readonly Color FillColor = new Color(0.09f, 0.10f, 0.15f, 0.92f);

        // 700 y no 756 (ancho del viewport): deja aire a la derecha para que la
        // scrollbar no quede pegada a las cards.
        private static readonly Vector2 EntrySize = new Vector2(700f, 110f);
        // Casi negro para el "???" de las cards bloqueadas (feedback de playtest).
        private static readonly Color LockedNameColor = new Color32(0x20, 0x1A, 0x15, 0xFF);

        [MenuItem("Rollgeon/Unlocks/Setup All")]
        public static void SetupAll()
        {
            UpsertLocalization();
            SetupEntryPrefab();
            RebuildScreenLayout();
        }

        // ================================================================
        // 1 - Localización
        // ================================================================

        [MenuItem("Rollgeon/Unlocks/1 - Upsert Localization")]
        public static void UpsertLocalization()
        {
            // El seeder ya la trae; el upsert es idempotente y deja el installer
            // autosuficiente. El botón de volver reusa UI/screen.back.
            LocalizationSetupTools.UpsertEntry("UI", "unlocks.title", "DESBLOQUEOS", "UNLOCKS");
            AssetDatabase.SaveAssets();
            Debug.Log("[UnlocksSetup] Keys de localización upserteadas.");
        }

        // ================================================================
        // 2 - Prefab de la card
        // ================================================================

        [MenuItem("Rollgeon/Unlocks/2 - Setup Unlock Entry Prefab")]
        public static void SetupEntryPrefab()
        {
            var rowSprite = LoadSprite(DrawerSheetPath, "UI-sheet_9");
            var lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LockSpritePath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (rowSprite == null) return;

            var root = PrefabUtility.LoadPrefabContents(EntryPrefabPath);
            try
            {
                if (root.GetComponent<UnlockEntryRowView>() == null)
                {
                    Debug.LogError("[UnlocksSetup] UnlockEntryRowView no encontrado en el prefab.");
                    return;
                }

                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = EntrySize;

                // Card con el slice de fila del drawer — mismo lenguaje visual que el
                // contrato in-game.
                if (!root.TryGetComponent<Image>(out var bg)) bg = root.AddComponent<Image>();
                bg.sprite = rowSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                bg.raycastTarget = false;

                if (!root.TryGetComponent<LayoutElement>(out var layout))
                    layout = root.AddComponent<LayoutElement>();
                layout.preferredWidth = EntrySize.x;
                layout.preferredHeight = EntrySize.y;

                // Para el pop escalonado de UnlocksScreen.PlayRowsEntrance.
                if (!root.TryGetComponent<CanvasGroup>(out _)) root.AddComponent<CanvasGroup>();

                TuneEntryLabel(rootRect, "Title", new Vector2(24f, 24f), new Vector2(560f, 40f),
                    font, outlineMat, TextColor, 18f, 26f, wrap: false);
                TuneEntryLabel(rootRect, "Body", new Vector2(24f, -20f), new Vector2(560f, 52f),
                    font, outlineMat, BodyColor, 14f, 18f, wrap: true);

                // Bind pinta el nombre según locked; los colores los autora el installer.
                var viewSo = new SerializedObject(root.GetComponent<UnlockEntryRowView>());
                viewSo.FindProperty("_unlockedNameColor").colorValue = TextColor;
                viewSo.FindProperty("_lockedNameColor").colorValue = LockedNameColor;
                viewSo.ApplyModifiedPropertiesWithoutUndo();

                // Candado → badge a la derecha (patrón del candado de Class Selection:
                // badge accent + glifo encima, porque el sprite es negro puro).
                var lockRect = rootRect.Find("LockImage") as RectTransform;
                if (lockRect != null)
                {
                    lockRect.anchorMin = lockRect.anchorMax = new Vector2(1f, 0.5f);
                    lockRect.pivot = new Vector2(1f, 0.5f);
                    lockRect.anchoredPosition = new Vector2(-24f, 0f);
                    lockRect.sizeDelta = new Vector2(44f, 44f);
                    if (lockRect.TryGetComponent<Image>(out var badgeImage))
                    {
                        badgeImage.sprite = null;
                        badgeImage.color = AccentColor;
                        badgeImage.raycastTarget = false;
                    }

                    var glyphRect = lockRect.Find("LockGlyph") as RectTransform;
                    if (glyphRect == null)
                    {
                        var go = new GameObject("LockGlyph", typeof(RectTransform), typeof(Image));
                        glyphRect = (RectTransform)go.transform;
                        glyphRect.SetParent(lockRect, worldPositionStays: false);
                    }
                    glyphRect.anchorMin = glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
                    glyphRect.pivot = new Vector2(0.5f, 0.5f);
                    glyphRect.anchoredPosition = Vector2.zero;
                    glyphRect.sizeDelta = new Vector2(30f, 30f);
                    var glyphImage = glyphRect.GetComponent<Image>();
                    glyphImage.sprite = lockSprite;
                    glyphImage.preserveAspect = true;
                    glyphImage.raycastTarget = false;
                }
                else
                {
                    Debug.LogWarning("[UnlocksSetup] 'LockImage' no encontrado en el prefab — sin badge de candado.");
                }

                PrefabUtility.SaveAsPrefabAsset(root, EntryPrefabPath);
                Debug.Log($"[UnlocksSetup] UnlockEntry.prefab restyleado (card {EntrySize.x}x{EntrySize.y}).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ================================================================
        // 3 - Layout de la pantalla
        // ================================================================

        [MenuItem("Rollgeon/Unlocks/3 - Rebuild Screen Layout")]
        public static void RebuildScreenLayout()
        {
            var juice = AssetDatabase.LoadAssetAtPath<MenuJuiceSettingsSO>(JuiceSettingsPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (juice == null || outlineMat == null || font == null)
            {
                Debug.LogError("[UnlocksSetup] Faltan MenuJuiceSettings / outline mat / font — correr los installers del Juicy Menu primero.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene().path == MainMenuScenePath
                ? EditorSceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            var screen = scene.GetRootGameObjects()
                .Select(r => r.GetComponentInChildren<UnlocksScreen>(true))
                .FirstOrDefault(s => s != null);
            if (screen == null)
            {
                Debug.LogError("[UnlocksSetup] UnlocksScreen no encontrada en la escena.");
                return;
            }
            var screenRect = (RectTransform)screen.transform;

            // -- Fondo: fill oscuro a pantalla completa + marco central 9-slice --
            var background = FindAnywhere(screenRect, "Background");
            if (background == null)
            {
                var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
                background = (RectTransform)go.transform;
                background.SetParent(screenRect, worldPositionStays: false);
            }
            background.SetSiblingIndex(0);
            StretchFull(background);
            if (!background.TryGetComponent<Image>(out var bgImage))
                bgImage = background.gameObject.AddComponent<Image>();
            bgImage.sprite = null;
            bgImage.type = Image.Type.Simple;
            bgImage.color = FillColor;
            // Come los clicks: detrás está el menú principal y sus botones.
            bgImage.raycastTarget = true;

            var panelFrame = EnsureRect(background, "PanelFrame", new Vector2(0f, -10f), new Vector2(840f, 780f));
            if (!panelFrame.TryGetComponent<Image>(out var frameImage))
                frameImage = panelFrame.gameObject.AddComponent<Image>();
            frameImage.sprite = LoadSprite(UiSheetPath, "UI-Sheet-sheet_7");
            frameImage.type = Image.Type.Sliced;
            frameImage.fillCenter = false;
            frameImage.pixelsPerUnitMultiplier = 1f;
            frameImage.raycastTarget = false;

            // -- Título (lane B: el texto lo pone UnlocksScreen para poder envolver
            // en <wave>; el LocalizeStringEvent estático lo pisaría) --
            // y=450: el borde superior del PanelFrame llega a 380 y el título no debe
            // pisarlo (feedback de playtest).
            var title = FindAnywhere(screenRect, "Title");
            var titleLabel = EnsureTmpLabel(screenRect, "Title", title, "DESBLOQUEOS", 52f,
                new Vector2(0f, 450f), new Vector2(500f, 70f), font, outlineMat, TextColor);
            if (titleLabel.TryGetComponent<LocalizeStringEvent>(out var localizeEvent))
                Object.DestroyImmediate(localizeEvent);
            if (titleLabel.GetComponent<TextAnimator_TMP>() == null)
                titleLabel.gameObject.AddComponent<TextAnimator_TMP>();
            ((RectTransform)titleLabel.transform).SetSiblingIndex(1);

            // -- ScrollRect existente: solo se reencuadra, la mecánica no se rearma --
            var container = FindAnywhere(screenRect, "UnlocksContainer");
            RectTransform content = null;
            if (container == null)
            {
                Debug.LogWarning("[UnlocksSetup] 'UnlocksContainer' no encontrado — el scroll queda como está.");
            }
            else
            {
                Place(container, screenRect, new Vector2(0f, -10f), new Vector2(800f, 680f));

                // El gris opaco de la escena vieja era la Image de fondo del ScrollRect;
                // fuera — el fondo lo pone el fill oscuro del Background.
                if (container.TryGetComponent<Image>(out var containerImage))
                {
                    containerImage.color = new Color(0f, 0f, 0f, 0f);
                    containerImage.raycastTarget = false;
                }

                var viewport = container.Find("Viewport") as RectTransform;
                if (viewport != null)
                {
                    viewport.anchorMin = Vector2.zero;
                    viewport.anchorMax = Vector2.one;
                    viewport.pivot = new Vector2(0.5f, 1f);
                    viewport.offsetMin = new Vector2(22f, 22f);
                    viewport.offsetMax = new Vector2(-22f, -22f);

                    // El mask graphic tampoco debe pintar; su Image queda solo para el
                    // recorte del Mask y el raycast del drag del ScrollRect.
                    if (viewport.TryGetComponent<Mask>(out var mask))
                        mask.showMaskGraphic = false;
                    if (viewport.TryGetComponent<Image>(out var viewportImage))
                        viewportImage.raycastTarget = true;

                    content = viewport.Find("Content") as RectTransform;
                }

                if (content != null)
                {
                    // La escena a mano traía un GridLayoutGroup — un GO admite un solo
                    // LayoutGroup, así que fuera todo antes de poner el VLG.
                    if (!content.TryGetComponent<VerticalLayoutGroup>(out _))
                        StripLayoutComponents(content.gameObject);

                    content.anchorMin = new Vector2(0f, 1f);
                    content.anchorMax = new Vector2(1f, 1f);
                    content.pivot = new Vector2(0.5f, 1f);
                    content.offsetMin = new Vector2(0f, content.offsetMin.y);
                    content.offsetMax = new Vector2(0f, content.offsetMax.y);
                    content.anchoredPosition = Vector2.zero;

                    if (!content.TryGetComponent<VerticalLayoutGroup>(out var vlg))
                        vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.padding = new RectOffset(0, 0, 8, 8);
                    vlg.spacing = 12f;
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childControlWidth = false;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = false;
                    vlg.childForceExpandHeight = false;

                    if (!content.TryGetComponent<ContentSizeFitter>(out var fitter))
                        fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                else
                {
                    Debug.LogWarning("[UnlocksSetup] 'Viewport/Content' no encontrado bajo UnlocksContainer.");
                }

                // -- ScrollRect: solo vertical, rueda utilizable y scrollbar estilizada --
                if (container.TryGetComponent<ScrollRect>(out var scroll))
                {
                    if (viewport != null) scroll.viewport = viewport;
                    if (content != null) scroll.content = content;
                    scroll.horizontal = false;
                    scroll.vertical = true;
                    scroll.movementType = ScrollRect.MovementType.Clamped;
                    // El default (1) mueve 1 px por tick de rueda — se sentía como
                    // "el scroll no funciona".
                    scroll.scrollSensitivity = 40f;

                    scroll.horizontalScrollbar = null;
                    var horizontalBar = container.Find("Scrollbar Horizontal");
                    if (horizontalBar != null) horizontalBar.gameObject.SetActive(false);

                    var verticalBar = container.Find("Scrollbar Vertical") as RectTransform;
                    if (verticalBar != null)
                    {
                        verticalBar.gameObject.SetActive(true);
                        // Fina (6 px) y contra el borde derecho del panel, despegada
                        // de las cards (que miden 700 sobre un viewport de 756).
                        verticalBar.anchorMin = new Vector2(1f, 0f);
                        verticalBar.anchorMax = Vector2.one;
                        verticalBar.pivot = new Vector2(1f, 0.5f);
                        verticalBar.offsetMin = new Vector2(-16f, 24f);
                        verticalBar.offsetMax = new Vector2(-10f, -24f);

                        if (verticalBar.TryGetComponent<Image>(out var barBg))
                        {
                            barBg.sprite = null;
                            barBg.color = new Color(1f, 1f, 1f, 0.08f);
                            barBg.raycastTarget = true;
                        }

                        if (verticalBar.TryGetComponent<Scrollbar>(out var scrollbar))
                        {
                            scroll.verticalScrollbar = scrollbar;
                            // AutoHide y no AutoHideAndExpandViewport: la barra no debe
                            // reacomodar el viewport al aparecer.
                            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

                            var handle = verticalBar.GetComponentsInChildren<Transform>(true)
                                .FirstOrDefault(t => t.name == "Handle");
                            if (handle != null && handle.TryGetComponent<Image>(out var handleImage))
                                handleImage.color = AccentColor;
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[UnlocksSetup] UnlocksContainer no tiene ScrollRect.");
                }
            }

            // -- Back juicy, centrado abajo --
            var back = FindAnywhere(screenRect, "Back") ?? FindAnywhere(screenRect, "BackButton");
            if (back == null)
            {
                var go = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
                back = (RectTransform)go.transform;
            }
            back.SetParent(screenRect, worldPositionStays: false);
            StripLayoutComponents(back.gameObject);
            Place(back, screenRect, new Vector2(0f, -450f), new Vector2(200f, 70f));
            if (!back.TryGetComponent<Image>(out _)) back.gameObject.AddComponent<Image>();
            if (!back.TryGetComponent<Button>(out var backButton))
                backButton = back.gameObject.AddComponent<Button>();
            EnsureButtonLabel(back, "Atrás", font, outlineMat);
            LocalizationSetupTools.BindTMP(back.GetComponentInChildren<TMP_Text>(true), "UI", "screen.back");

            var backJuicy = EnsureJuicyButton(backButton, juice, outlineMat, font);
            EnsureGroup(screen.gameObject, new[] { backJuicy }, juice);

            // -- Rewire de la screen --
            var entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPrefabPath);
            var so = new SerializedObject(screen);
            if (content != null) so.FindProperty("_entriesContainer").objectReferenceValue = content;
            if (entryPrefab != null)
                so.FindProperty("_entryRowPrefab").objectReferenceValue =
                    entryPrefab.GetComponent<UnlockEntryRowView>();
            so.FindProperty("_backButton").objectReferenceValue = backButton;
            so.FindProperty("_titleLabel").objectReferenceValue = titleLabel;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[UnlocksSetup] Pantalla de desbloqueos restyleada.");
        }

        // ================================================================
        // Helpers (copias locales — convención de los installers)
        // ================================================================

        private static Sprite LoadSprite(string assetPath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogError($"[UnlocksSetup] Slice '{spriteName}' no encontrado en {assetPath}.");
            return sprite;
        }

        private static void TuneEntryLabel(RectTransform rootRect, string name, Vector2 pos, Vector2 size,
            TMP_FontAsset font, Material outlineMat, Color color, float minSize, float maxSize, bool wrap)
        {
            var rect = rootRect.Find(name) as RectTransform;
            if (rect == null)
            {
                Debug.LogWarning($"[UnlocksSetup] '{name}' no encontrado en UnlockEntry.prefab.");
                return;
            }
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var tmp = rect.GetComponent<TMP_Text>();
            if (tmp == null) return;
            if (font != null) tmp.font = font;
            if (outlineMat != null) tmp.fontSharedMaterial = outlineMat;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;
            tmp.enableWordWrapping = wrap;
            tmp.overflowMode = wrap ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            EditorUtility.SetDirty(tmp);
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
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
            var intro = so.FindProperty("_introAnimation") ?? so.FindProperty("_waitForIntro");
            if (intro != null) intro.objectReferenceValue = null;
            so.ApplyModifiedProperties();
        }
    }
}
