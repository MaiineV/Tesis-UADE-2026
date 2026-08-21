using System.Linq;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Instala el minimapa estilo Isaac (espejo de <c>ChipStackSetupTools</c>):
    /// settings con los slices Minimap_0..8, la instancia de exploración (reusa el GO
    /// stub del prefab, hoy desactivado) y la instancia de combate detrás del toggle
    /// Tab (switcher carrusel ↔ minimapa). Idempotente — reejecutar actualiza.
    /// </summary>
    public static class MinimapSetupTools
    {
        private const string SettingsPath = "Assets/Rollgeon/Services/MinimapSettings.asset";
        private const string SheetPath = "Assets/Art/UI/Minimap/Minimap.png";
        private const string CombatHudPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_CombatHUD.prefab";
        private const string ExplorationHudPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_ExplorationHUD.prefab";

        // Rect del carrusel de turnos — el minimapa vive en el MISMO lugar.
        private static readonly Vector2 PanelAnchor = Vector2.one;   // top-right
        private static readonly Vector2 PanelPos = new Vector2(-50f, -28f);
        private static readonly Vector2 PanelSize = new Vector2(450f, 120f);

        [MenuItem("Rollgeon/Minimap/Setup All")]
        public static void SetupAll()
        {
            CreateSettings();
            WireExplorationHud();
            WireCombatHud();
            Debug.Log("[MinimapSetupTools] Setup All completo.");
        }

        // ================================================================
        // 1 - Settings
        // ================================================================

        [MenuItem("Rollgeon/Minimap/1 - Create Settings")]
        public static void CreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MinimapSettingsSO>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<MinimapSettingsSO>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            bool ok = true;
            for (int i = 0; i < 9; i++)
            {
                var sprite = LoadSpriteOrError(SheetPath, $"Minimap_{i}");
                if (sprite == null) { ok = false; continue; }
                settings.SetCellSprite(i, sprite);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            if (!ok)
            {
                Debug.LogError("[MinimapSetupTools] Faltan slices Minimap_0..8 — settings incompletos.");
                return;
            }
            Debug.Log($"[MinimapSetupTools] Settings listos en '{SettingsPath}' (9 sprites).");
        }

        // ================================================================
        // 2 - Exploration HUD
        // ================================================================

        [MenuItem("Rollgeon/Minimap/2 - Wire Exploration HUD")]
        public static void WireExplorationHud()
        {
            var settings = RequireSettings();
            if (settings == null) return;

            var root = PrefabUtility.LoadPrefabContents(ExplorationHudPrefabPath);
            try
            {
                var hud = root.GetComponentInChildren<ExplorationHUDView>(true);
                if (hud == null)
                {
                    Debug.LogError("[MinimapSetupTools] ExplorationHUDView no encontrado en el prefab.");
                    return;
                }

                var minimap = root.GetComponentInChildren<MinimapView>(true);
                RectTransform panelRect;
                if (minimap == null)
                {
                    panelRect = EnsureRect((RectTransform)hud.transform, "MinimapView");
                    minimap = panelRect.gameObject.AddComponent<MinimapView>();
                }
                else
                {
                    panelRect = (RectTransform)minimap.transform;
                }

                // El GO stub venía desactivado (m_IsActive: 0) — sin esto el minimapa
                // es invisible y el [Required] del HUD warnea.
                minimap.gameObject.SetActive(true);
                ConfigurePanelRect(panelRect);
                var cellRoot = EnsureMinimapInternals(minimap, settings);

                // Re-cablear la referencia del HUD por si el stub viejo quedó dangling.
                var hudSo = new SerializedObject(hud);
                hudSo.FindProperty("_minimap").objectReferenceValue = minimap;
                hudSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ExplorationHudPrefabPath);
                Debug.Log($"[MinimapSetupTools] Exploration HUD wireado (cellRoot='{cellRoot.name}').");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ================================================================
        // 3 - Combat HUD
        // ================================================================

        [MenuItem("Rollgeon/Minimap/3 - Wire Combat HUD")]
        public static void WireCombatHud()
        {
            var settings = RequireSettings();
            if (settings == null) return;

            var root = PrefabUtility.LoadPrefabContents(CombatHudPrefabPath);
            try
            {
                var hud = root.GetComponentInChildren<CombatHUDView>(true);
                var turnQueue = root.GetComponentInChildren<TurnQueueView>(true);
                if (hud == null || turnQueue == null)
                {
                    Debug.LogError("[MinimapSetupTools] CombatHUDView o TurnQueueView no encontrados.");
                    return;
                }

                // CanvasGroup en el carrusel — se oculta por alpha, NUNCA SetActive
                // (debe seguir procesando eventos de turno oculto).
                var carouselGroup = Ensure<CanvasGroup>(turnQueue.gameObject);

                // Minimapa sibling del carrusel, mismo rect, oculto por default.
                var minimapRect = EnsureRect((RectTransform)turnQueue.transform.parent, "Minimap");
                ConfigurePanelRect(minimapRect);
                var minimapGroup = Ensure<CanvasGroup>(minimapRect.gameObject);
                minimapGroup.alpha = 0f;
                minimapGroup.interactable = false;
                minimapGroup.blocksRaycasts = false;

                var minimap = Ensure<MinimapView>(minimapRect.gameObject);
                EnsureMinimapInternals(minimap, settings);

                // Switcher en el root del HUD con las 4 refs.
                var switcher = Ensure<CombatRightPanelSwitcher>(hud.gameObject);
                var switcherSo = new SerializedObject(switcher);
                switcherSo.FindProperty("_carouselPanel").objectReferenceValue = (RectTransform)turnQueue.transform;
                switcherSo.FindProperty("_carouselGroup").objectReferenceValue = carouselGroup;
                switcherSo.FindProperty("_minimapPanel").objectReferenceValue = minimapRect;
                switcherSo.FindProperty("_minimapGroup").objectReferenceValue = minimapGroup;
                switcherSo.ApplyModifiedPropertiesWithoutUndo();

                var hudSo = new SerializedObject(hud);
                hudSo.FindProperty("_combatMinimap").objectReferenceValue = minimap;
                hudSo.FindProperty("_rightPanelSwitcher").objectReferenceValue = switcher;
                hudSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CombatHudPrefabPath);
                Debug.Log("[MinimapSetupTools] Combat HUD wireado (minimapa + switcher Tab).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static MinimapSettingsSO RequireSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MinimapSettingsSO>(SettingsPath);
            if (settings == null)
                Debug.LogError("[MinimapSetupTools] Falta el settings — correr '1 - Create Settings' primero.");
            return settings;
        }

        // Rect del panel = el del carrusel (top-right). RectMask2D clipea las celdas
        // que quedan fuera al rotar.
        private static void ConfigurePanelRect(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = PanelAnchor;
            rect.pivot = PanelAnchor;
            rect.anchoredPosition = PanelPos;
            rect.sizeDelta = PanelSize;
            Ensure<RectMask2D>(rect.gameObject);
        }

        // Child "Cells" centrado (la sala actual cae en el centro del panel) + wiring
        // de _settings/_cellRoot por SerializedObject.
        private static RectTransform EnsureMinimapInternals(MinimapView minimap, MinimapSettingsSO settings)
        {
            var rect = (RectTransform)minimap.transform;
            var cellRoot = EnsureRect(rect, "Cells");
            cellRoot.anchorMin = cellRoot.anchorMax = new Vector2(0.5f, 0.5f);
            cellRoot.pivot = new Vector2(0.5f, 0.5f);
            cellRoot.anchoredPosition = Vector2.zero;
            cellRoot.sizeDelta = Vector2.zero;

            var so = new SerializedObject(minimap);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_cellRoot").objectReferenceValue = cellRoot;
            so.ApplyModifiedPropertiesWithoutUndo();
            return cellRoot;
        }

        private static RectTransform EnsureRect(RectTransform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, worldPositionStays: false);
            return rect;
        }

        private static T Ensure<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

        private static Sprite LoadSpriteOrError(string assetPath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogError($"[MinimapSetupTools] Slice '{spriteName}' no encontrado en '{assetPath}'.");
            return sprite;
        }
    }
}
