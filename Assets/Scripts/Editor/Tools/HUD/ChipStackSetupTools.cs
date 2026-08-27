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
        private const string CupSpritePath = "Assets/Art/UI/VasoGenerala/VasoGenerala.png";
        private const string CupFlipSpritePath = "Assets/Art/UI/VasoGenerala/VasoGeneralaFlip.png";
        private const string ParticlePrefabPath = "Assets/Prefabs/UI/DiceThrowParticle.prefab";
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

        // Vaso de generala (Feature#0053): 39×62 nativo × 1.2, arriba del label.
        private const float CupY = 85f;
        private static readonly Vector2 CupSize = new Vector2(47f, 74f);

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
            settings.RollCup = LoadSpriteOrError(CupSpritePath, "VasoGenerala_0");
            settings.RollCupFlip = LoadSpriteOrError(CupFlipSpritePath, "VasoGeneralaFlip_0");

            if (settings.HealthChip == null || settings.EnergyChip == null || settings.ShieldChip == null
                || settings.GoldChipFlat == null || settings.GoldChipTilted == null
                || settings.PotionFull == null || settings.PotionEmpty == null
                || settings.RollCup == null || settings.RollCupFlip == null)
            {
                Debug.LogError("[ChipStackSetup] Falta al menos un slice — revisar los sheets. Abortando asignación.");
                return;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChipStackSetup] ChipStackSettings listo con los 9 sprites.");
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
                var goldStack = EnsureStack(cluster, "GoldStack", GoldStackX, out var goldRoot, out var goldLabel);

                var healthView = EnsureView<HealthChipStackView>(healthStack.gameObject, healthRoot, healthLabel, settings);
                var goldView = EnsureView<GoldChipStackView>(goldStack.gameObject, goldRoot, goldLabel, settings);

                // -- Vaso de generala del pool de rolls (Feature#0053) --
                EnsureCupStack(cluster, settings);

                // -- Ficha inclinada del oro (hija del ChipRoot, arranca inactiva) --
                // Posición fina de playtest: apoyada al pie de la pila, a la derecha.
                var tilted = EnsureRect(goldRoot, "TiltedChip", new Vector2(19.5f, -0.3f),
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

                    // Tuning fino de playtest: sprite chico centrado-alto en el slot
                    // (el icon ancla al centro del slot 80x92 → local (40, 28.9)).
                    var slotIcon = slotSo.FindProperty("_icon").objectReferenceValue as Image;
                    if (slotIcon != null)
                    {
                        slotIcon.sprite = settings.PotionFull;
                        var iconRect = (RectTransform)slotIcon.transform;
                        iconRect.localScale = new Vector3(0.54f, 0.54f, 1f); // 0.45 +20% (pedido playtest)
                        iconRect.anchoredPosition = new Vector2(0f, -17.1f);
                    }

                    // CountLabel autorado (sin esto ActiveItemSlotView lo auto-crea
                    // abajo-derecha en runtime y no respeta la posición tuneada).
                    var countLabel = EnsureCountLabel(slotRect);
                    slotSo.FindProperty("_countLabel").objectReferenceValue = countLabel;
                    slotSo.ApplyModifiedProperties();
                }
                else
                {
                    Debug.LogWarning("[ChipStackSetup] PocionSlot no encontrado en Canvas_PlayerStatus.");
                }

                // -- Barra vieja: desactivada (decisión: vuelta atrás rápida). La
                // EnergyBarView legacy murió con el sistema de energía (Feature#0050);
                // su GO huérfano se limpia del prefab en el pase de assets. --
                DeactivateHolder<HealthBarView>(root);

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
            var root = PrefabUtility.LoadPrefabContents(ExplorationHudPrefabPath);
            try
            {
                var gold = root.GetComponentInChildren<GoldCounterView>(true);
                if (gold != null && gold.gameObject.activeSelf) gold.gameObject.SetActive(false);

                // Tuning fino de playtest: el Heal más cerca del centro del stack
                // de acciones (anchors (0, 0.5) del container ExplorationActions).
                var heal = root.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(t => t.name == "HealButton");
                if (heal != null) heal.anchoredPosition = new Vector2(100f, 0f);
                else Debug.LogWarning("[ChipStackSetup] HealButton no encontrado en Canvas_ExplorationHUD.");

                PrefabUtility.SaveAsPrefabAsset(root, ExplorationHudPrefabPath);
                Debug.Log("[ChipStackSetup] Canvas_ExplorationHUD: GoldCounter desactivado + HealButton reposicionado.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Rollgeon/Chip Stack HUD/5 - Rewire Gameplay Scene")]
        public static void RewireGameplayScene()
        {
            var scene = EditorSceneManager.GetActiveScene().path == GameplayScenePath
                ? EditorSceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            HealthChipStackView healthView = null;
            RollCupView energyView = null;
            CombatHUDView combatHud = null;
            ExplorationHUDView explorationHud = null;

            foreach (var rootGo in scene.GetRootGameObjects())
            {
                if (healthView == null) healthView = rootGo.GetComponentInChildren<HealthChipStackView>(true);
                if (energyView == null) energyView = rootGo.GetComponentInChildren<RollCupView>(true);
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

        private static void WireHud(Component hud, HealthChipStackView health, RollCupView energy)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("_healthChips").objectReferenceValue = health;
            // El ExplorationHUD ya no tiene pila de rolls (el pool es combat-only,
            // Feature#0050) — solo el CombatHUD expone _energyChips.
            var energyProp = so.FindProperty("_energyChips");
            if (energyProp != null) energyProp.objectReferenceValue = energy;
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
            label = EnsureStackLabel(stack);

            return stack;
        }

        private static TextMeshProUGUI EnsureStackLabel(RectTransform stack)
        {
            var labelRect = EnsureRect(stack, "Label", new Vector2(0f, LabelY), new Vector2(84f, 32f));
            var label = labelRect.GetComponent<TextMeshProUGUI>();
            if (label == null) label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 26f;
            label.richText = true;
            label.raycastTarget = false;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            EditorUtility.SetDirty(label);
            return label;
        }

        /// <summary>
        /// EnergyStack versión Feature#0053: el pool de rolls es un vaso de
        /// generala animado, no una pila de chips. Migra el stack viejo si
        /// existe (borra ChipRoot y su ChipStackView) y cablea
        /// RollCupView + RollCupJuice + capa de partículas. Idempotente.
        /// </summary>
        private static void EnsureCupStack(RectTransform cluster, ChipStackSettingsSO settings)
        {
            var stack = cluster.Find("EnergyStack") as RectTransform;
            if (stack == null)
            {
                var go = new GameObject("EnergyStack", typeof(RectTransform));
                stack = (RectTransform)go.transform;
                stack.SetParent(cluster, worldPositionStays: false);
            }
            stack.anchorMin = stack.anchorMax = Vector2.zero;
            stack.pivot = Vector2.zero;
            stack.anchoredPosition = new Vector2(EnergyStackX, 0f);
            stack.sizeDelta = new Vector2(80f, 420f);

            // Migración del stack de chips viejo.
            var chipRoot = stack.Find("ChipRoot");
            if (chipRoot != null) Object.DestroyImmediate(chipRoot.gameObject);
            if (stack.TryGetComponent<ChipStackView>(out var oldStack)) Object.DestroyImmediate(oldStack);

            var label = EnsureStackLabel(stack);

            // Vaso: pivot centrado — el flip gira sobre su centro.
            var cup = stack.Find("Cup") as RectTransform;
            if (cup == null)
            {
                var go = new GameObject("Cup", typeof(RectTransform));
                cup = (RectTransform)go.transform;
                cup.SetParent(stack, worldPositionStays: false);
            }
            cup.anchorMin = cup.anchorMax = new Vector2(0.5f, 0f);
            cup.pivot = new Vector2(0.5f, 0.5f);
            cup.anchoredPosition = new Vector2(0f, CupY);
            cup.sizeDelta = CupSize;
            if (!cup.TryGetComponent<Image>(out var cupImage)) cupImage = cup.gameObject.AddComponent<Image>();
            cupImage.sprite = settings.RollCup;
            cupImage.raycastTarget = false;

            // Capa de partículas (pool de Images). Full-stretch con pivot
            // centrado: así el InverseTransformPoint del juice coincide con el
            // anchoredPosition de las partículas hijas.
            var burstRect = stack.Find("CupBurst") as RectTransform;
            if (burstRect == null)
            {
                var go = new GameObject("CupBurst", typeof(RectTransform));
                burstRect = (RectTransform)go.transform;
                burstRect.SetParent(stack, worldPositionStays: false);
            }
            burstRect.anchorMin = Vector2.zero;
            burstRect.anchorMax = Vector2.one;
            burstRect.pivot = new Vector2(0.5f, 0.5f);
            burstRect.offsetMin = Vector2.zero;
            burstRect.offsetMax = Vector2.zero;
            if (!burstRect.TryGetComponent<DiceThrowImpactBurst>(out var burst))
                burst = burstRect.gameObject.AddComponent<DiceThrowImpactBurst>();
            var particlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ParticlePrefabPath);
            var burstSo = new SerializedObject(burst);
            burstSo.FindProperty("_particlePrefab").objectReferenceValue =
                particlePrefab != null ? particlePrefab.GetComponent<Image>() : null;
            // Tuning a escala HUD: bursts más chicos y lentos que los del tablero.
            burstSo.FindProperty("_countRange").vector2IntValue = new Vector2Int(3, 9);
            burstSo.FindProperty("_speedRange").vector2Value = new Vector2(180f, 420f);
            burstSo.FindProperty("_spreadDegrees").floatValue = 80f;
            burstSo.FindProperty("_gravity").floatValue = 900f;
            burstSo.FindProperty("_sizeRange").vector2Value = new Vector2(3f, 6f);
            burstSo.FindProperty("_maxAlive").intValue = 24;
            burstSo.ApplyModifiedProperties();

            if (!stack.TryGetComponent<RollCupJuice>(out var juice))
                juice = stack.gameObject.AddComponent<RollCupJuice>();
            var juiceSo = new SerializedObject(juice);
            juiceSo.FindProperty("_cup").objectReferenceValue = cup;
            juiceSo.FindProperty("_burst").objectReferenceValue = burst;
            // Swap de sprite a mitad del giro: parado ↔ boca abajo (Flip).
            juiceSo.FindProperty("_cupImage").objectReferenceValue = cupImage;
            juiceSo.FindProperty("_uprightSprite").objectReferenceValue = settings.RollCup;
            juiceSo.FindProperty("_faceDownSprite").objectReferenceValue = settings.RollCupFlip;
            juiceSo.ApplyModifiedProperties();

            if (!stack.TryGetComponent<RollCupView>(out var view))
                view = stack.gameObject.AddComponent<RollCupView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_cup").objectReferenceValue = cup;
            viewSo.FindProperty("_label").objectReferenceValue = label;
            viewSo.FindProperty("_juice").objectReferenceValue = juice;
            viewSo.ApplyModifiedProperties();
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

        /// <summary>
        /// Label de cantidad de la poción, autorado en la posición tuneada de
        /// playtest — anclado al origen (pivot 0,0) del slot, así el
        /// anchoredPosition coincide con el local (53.2, -33.5) medido en editor.
        /// </summary>
        private static TextMeshProUGUI EnsureCountLabel(RectTransform slotRect)
        {
            var rect = slotRect.Find("CountLabel") as RectTransform;
            if (rect == null)
            {
                var go = new GameObject("CountLabel", typeof(RectTransform));
                rect = (RectTransform)go.transform;
                rect.SetParent(slotRect, worldPositionStays: false);
            }
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(53.2f, -33.5f);
            rect.sizeDelta = new Vector2(60f, 30f);

            var label = rect.GetComponent<TextMeshProUGUI>();
            if (label == null) label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 26f;
            label.raycastTarget = false;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            EditorUtility.SetDirty(label);
            return label;
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
