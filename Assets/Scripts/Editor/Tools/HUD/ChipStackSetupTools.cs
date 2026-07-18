using System.Linq;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Installer del HUD de pilas de fichas: crea el settings SO con los slices,
    /// arma el cluster bottom-left en <c>Canvas_PlayerStatus.prefab</c>
    /// (vida+escudo, energía, oro, poción), desactiva las barras viejas y
    /// recablea los orquestadores en <c>02_Gameplay</c>. Idempotente —
    /// reejecutar actualiza sin duplicar.
    /// </summary>
    public static class ChipStackSetupTools
    {
        private const string SettingsPath = "Assets/Rollgeon/Services/ChipStackSettings.asset";
        private const string UiSheetPath = "Assets/Art/UI/UI-Sheet-sheet.png";
        private const string CoinSheetPath = "Assets/Art/UI/Inventory/CoinStyle1.1.png";
        private const string PotionSheetPath = "Assets/Art/UI/Inventory/PotionSheet.png";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";
        private const string PlayerStatusPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_PlayerStatus.prefab";
        private const string CombatHudPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_CombatHUD.prefab";
        private const string ExplorationHudPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_ExplorationHUD.prefab";
        private const string GameplayScenePath = "Assets/Scenes/02_Gameplay.unity";

        // Cluster abajo-izquierda, a la altura de la barra de acciones (y 20..130).
        private static readonly Vector2 ClusterPos = new Vector2(24f, 20f);
        private const float HealthStackX = 0f;
        private const float EnergyStackX = 90f;
        private const float GoldStackX = 170f;
        private const float PotionSlotX = 240f;
        private const float PotionSlotY = 45f;
        private const float ChipRootY = 50f;
        private const float LabelY = 10f;

        [MenuItem("Rollgeon/Chip Stack HUD/Setup All")]
        public static void SetupAll()
        {
            CreateSettings();
            SetupPlayerStatusPrefab();
            CleanCombatHudPrefab();
            CleanExplorationHudPrefab();
            RewireGameplayScene();
        }

        [MenuItem("Rollgeon/Chip Stack HUD/1 - Create Settings")]
        public static void CreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ChipStackSettingsSO>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ChipStackSettingsSO>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.HealthChip = LoadSpriteOrError(UiSheetPath, "UI-Sheet-sheet_4");
            settings.EnergyChip = LoadSpriteOrError(UiSheetPath, "UI-Sheet-sheet_5");
            settings.ShieldChip = LoadSpriteOrError(UiSheetPath, "UI-Sheet-sheet_9");
            settings.GoldChipFlat = LoadSpriteOrError(CoinSheetPath, "CoinStyle1.1_0");
            settings.GoldChipTilted = LoadSpriteOrError(CoinSheetPath, "CoinStyle1.1_1");
            settings.PotionFull = LoadSpriteOrError(PotionSheetPath, "PotionSheet_0");
            settings.PotionEmpty = LoadSpriteOrError(PotionSheetPath, "PotionSheet_1");

            if (settings.HealthChip == null || settings.EnergyChip == null || settings.ShieldChip == null
                || settings.GoldChipFlat == null || settings.GoldChipTilted == null
                || settings.PotionFull == null || settings.PotionEmpty == null)
            {
                Debug.LogError("[ChipStackSetup] Falta al menos un slice — revisar los sheets. Abortando asignación.");
                return;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChipStackSetup] ChipStackSettings listo con los 7 sprites.");
        }

        [MenuItem("Rollgeon/Chip Stack HUD/2 - Setup PlayerStatus Prefab")]
        public static void SetupPlayerStatusPrefab()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ChipStackSettingsSO>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError("[ChipStackSetup] Correr primero '1 - Create Settings'.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerStatusPrefabPath);
            try
            {
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas == null)
                {
                    Debug.LogError("[ChipStackSetup] Canvas no encontrado en Canvas_PlayerStatus.");
                    return;
                }
                var canvasRect = (RectTransform)canvas.transform;

                // -- Cluster bottom-left --
                var cluster = EnsureRect(canvasRect, "ChipCluster", Vector2.zero, Vector2.zero);
                cluster.anchorMin = cluster.anchorMax = Vector2.zero;
                cluster.pivot = Vector2.zero;
                cluster.anchoredPosition = ClusterPos;
                cluster.sizeDelta = Vector2.zero;

                // -- Pilas --
                var healthStack = EnsureStack(cluster, "HealthStack", HealthStackX, out var healthRoot, out var healthLabel);
                var energyStack = EnsureStack(cluster, "EnergyStack", EnergyStackX, out var energyRoot, out var energyLabel);
                var goldStack = EnsureStack(cluster, "GoldStack", GoldStackX, out var goldRoot, out var goldLabel);

                var healthView = EnsureView<HealthChipStackView>(healthStack.gameObject, healthRoot, healthLabel, settings);
                var energyView = EnsureView<EnergyChipStackView>(energyStack.gameObject, energyRoot, energyLabel, settings);
                var goldView = EnsureView<GoldChipStackView>(goldStack.gameObject, goldRoot, goldLabel, settings);

                // -- Ficha inclinada del oro (hija del ChipRoot, arranca inactiva) --
                var tilted = EnsureRect(goldRoot, "TiltedChip", new Vector2(14f, 4f * settings.GoldChipSpacingY),
                    settings.GoldChipTilted != null ? settings.GoldChipTilted.rect.size * Mathf.Max(1f, settings.ChipScale) : new Vector2(19f, 18f));
                if (!tilted.TryGetComponent<Image>(out var tiltedImage)) tiltedImage = tilted.gameObject.AddComponent<Image>();
                if (!tilted.TryGetComponent<CanvasGroup>(out _)) tilted.gameObject.AddComponent<CanvasGroup>();
                tiltedImage.sprite = settings.GoldChipTilted;
                tiltedImage.raycastTarget = false;
                tilted.gameObject.SetActive(false);

                var goldSo = new SerializedObject(goldView);
                goldSo.FindProperty("_tiltedChip").objectReferenceValue = tiltedImage;
                goldSo.ApplyModifiedProperties();

                // -- Poción: mover al cluster y asignar sprites por cantidad --
                var potionSlot = root.GetComponentsInChildren<ActiveItemSlotView>(true)
                    .FirstOrDefault(s => s.gameObject.name == "PocionSlot");
                if (potionSlot != null)
                {
                    var slotRect = (RectTransform)potionSlot.transform;
                    slotRect.anchorMin = slotRect.anchorMax = Vector2.zero;
                    slotRect.pivot = Vector2.zero;
                    slotRect.anchoredPosition = new Vector2(ClusterPos.x + PotionSlotX, ClusterPos.y + PotionSlotY);
                    if (settings.PotionFull != null)
                    {
                        slotRect.sizeDelta = settings.PotionFull.rect.size * 2f;
                    }

                    var slotSo = new SerializedObject(potionSlot);
                    slotSo.FindProperty("_iconWhenCountPositive").objectReferenceValue = settings.PotionFull;
                    slotSo.FindProperty("_iconWhenCountZero").objectReferenceValue = settings.PotionEmpty;
                    slotSo.FindProperty("_iconActive").objectReferenceValue = null;
                    slotSo.FindProperty("_iconInactive").objectReferenceValue = null;
                    slotSo.ApplyModifiedProperties();

                    var slotIcon = new SerializedObject(potionSlot).FindProperty("_icon").objectReferenceValue as Image;
                    if (slotIcon != null) slotIcon.sprite = settings.PotionFull;
                }
                else
                {
                    Debug.LogWarning("[ChipStackSetup] PocionSlot no encontrado en Canvas_PlayerStatus.");
                }

                // -- Barras viejas: desactivadas (decisión: vuelta atrás rápida) --
                DeactivateHolder<HealthBarView>(root);
                DeactivateHolder<EnergyBarView>(root);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerStatusPrefabPath);
                Debug.Log("[ChipStackSetup] Canvas_PlayerStatus cableado: cluster vida/energía/oro + poción.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Rollgeon/Chip Stack HUD/3 - Clean CombatHUD Prefab")]
        public static void CleanCombatHudPrefab()
        {
            DeactivateInPrefab<ShieldBarView>(CombatHudPrefabPath, "ShieldBarView");
        }

        [MenuItem("Rollgeon/Chip Stack HUD/4 - Clean ExplorationHUD Prefab")]
        public static void CleanExplorationHudPrefab()
        {
            DeactivateInPrefab<GoldCounterView>(ExplorationHudPrefabPath, "GoldCounterView");
        }

        [MenuItem("Rollgeon/Chip Stack HUD/5 - Rewire Gameplay Scene")]
        public static void RewireGameplayScene()
        {
            var scene = EditorSceneManager.GetActiveScene().path == GameplayScenePath
                ? EditorSceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            HealthChipStackView healthView = null;
            EnergyChipStackView energyView = null;
            CombatHUDView combatHud = null;
            ExplorationHUDView explorationHud = null;

            foreach (var rootGo in scene.GetRootGameObjects())
            {
                if (healthView == null) healthView = rootGo.GetComponentInChildren<HealthChipStackView>(true);
                if (energyView == null) energyView = rootGo.GetComponentInChildren<EnergyChipStackView>(true);
                if (combatHud == null) combatHud = rootGo.GetComponentInChildren<CombatHUDView>(true);
                if (explorationHud == null) explorationHud = rootGo.GetComponentInChildren<ExplorationHUDView>(true);
            }

            if (healthView == null || energyView == null)
            {
                Debug.LogError("[ChipStackSetup] Chip stacks no encontrados en la escena — correr primero '2 - Setup PlayerStatus Prefab'.");
                return;
            }

            if (combatHud != null) WireHud(combatHud, healthView, energyView);
            else Debug.LogWarning("[ChipStackSetup] CombatHUDView no encontrado en la escena.");

            if (explorationHud != null) WireHud(explorationHud, healthView, energyView);
            else Debug.LogWarning("[ChipStackSetup] ExplorationHUDView no encontrado en la escena.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ChipStackSetup] 02_Gameplay recableado (orquestadores → chip stacks).");
        }

        private static void WireHud(Component hud, HealthChipStackView health, EnergyChipStackView energy)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("_healthChips").objectReferenceValue = health;
            so.FindProperty("_energyChips").objectReferenceValue = energy;
            so.ApplyModifiedProperties();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static Sprite LoadSpriteOrError(string assetPath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
            {
                Debug.LogError($"[ChipStackSetup] Slice '{spriteName}' no encontrado en {assetPath}.");
            }
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
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform EnsureStack(RectTransform cluster, string name, float x,
            out RectTransform chipRoot, out TextMeshProUGUI label)
        {
            var stack = cluster.Find(name) as RectTransform;
            if (stack == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                stack = (RectTransform)go.transform;
                stack.SetParent(cluster, worldPositionStays: false);
            }
            stack.anchorMin = stack.anchorMax = Vector2.zero;
            stack.pivot = Vector2.zero;
            stack.anchoredPosition = new Vector2(x, 0f);
            stack.sizeDelta = new Vector2(80f, 420f);

            chipRoot = EnsureRect(stack, "ChipRoot", new Vector2(0f, ChipRootY), new Vector2(80f, 0f));

            var labelRect = EnsureRect(stack, "Label", new Vector2(0f, LabelY), new Vector2(84f, 32f));
            label = labelRect.GetComponent<TextMeshProUGUI>();
            if (label == null) label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 26f;
            label.richText = true;
            label.raycastTarget = false;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            EditorUtility.SetDirty(label);

            return stack;
        }

        private static T EnsureView<T>(GameObject host, RectTransform chipRoot, TextMeshProUGUI label,
            ChipStackSettingsSO settings) where T : MonoBehaviour
        {
            if (!host.TryGetComponent<ChipStackView>(out var stackView))
                stackView = host.AddComponent<ChipStackView>();
            var stackSo = new SerializedObject(stackView);
            stackSo.FindProperty("_chipRoot").objectReferenceValue = chipRoot;
            stackSo.ApplyModifiedProperties();

            if (!host.TryGetComponent<T>(out var view)) view = host.AddComponent<T>();
            var so = new SerializedObject(view);
            so.FindProperty("_stack").objectReferenceValue = stackView;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();
            return view;
        }

        private static void DeactivateHolder<T>(GameObject root) where T : Component
        {
            var comp = root.GetComponentInChildren<T>(true);
            if (comp != null && comp.gameObject.activeSelf)
            {
                comp.gameObject.SetActive(false);
            }
        }

        private static void DeactivateInPrefab<T>(string prefabPath, string labelForLog) where T : Component
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var comp = root.GetComponentInChildren<T>(true);
                if (comp == null)
                {
                    Debug.LogWarning($"[ChipStackSetup] {labelForLog} no encontrado en {prefabPath} — nada que desactivar.");
                    return;
                }
                comp.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[ChipStackSetup] {labelForLog} desactivado en {System.IO.Path.GetFileName(prefabPath)}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
