using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.Menu
{
    /// <summary>
    /// Hace responsive el menú principal (01_MainMenu) sin re-layoutear pantallas:
    /// todos los elementos interactivos ya caben en la grilla de referencia
    /// 1920×1080 (extents máx. 940×515), así que con
    /// <see cref="CanvasScaler.ScreenMatchMode.Expand"/> el canvas contiene el rect
    /// de referencia completo en cualquier aspect (21:9, 16:10 del WebGL de Itch,
    /// 4:3) y nada queda fuera de pantalla.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Idempotente y re-ejecutable. NO usa LayoutGroups (<c>JuicyMenuButton</c>
    /// escribe <c>anchoredPosition.x</c> cada frame) y NO toca NINGÚN rect
    /// autorado: una primera versión estiraba las capas "full-bleed" (Backgrounds,
    /// overlays de la intro) y rompió el arte de la intro — <c>IntroBurnOverlay</c>
    /// es el logo de Sunken Grand (850.5×512 centrado en pantalla, colgado del
    /// contenedor chico <c>IntroTitulo</c>) y estirarlo al padre lo dejó de
    /// 400×100. Los fondos quedan como los autoró el arte; en aspects ≠16:9
    /// pueden pillarboxear/estirar unos px — cosmético y aceptado.
    /// </para>
    /// <para>
    /// Política de guardado: si la escena está dirty (cambios del usuario sin
    /// guardar) se aplica y se marca dirty pero NO se guarda (regla no-revert).
    /// </para>
    /// </remarks>
    public static class ResponsiveMenuSetupTools
    {
        private const string MainMenuScenePath = "Assets/Scenes/01_MainMenu.unity";

        [MenuItem("Rollgeon/Responsive Menu/Setup All")]
        public static void SetupAll() => SetCanvasScalerExpand();

        [MenuItem("Rollgeon/Responsive Menu/1 - Canvas Scaler Expand")]
        public static void SetCanvasScalerExpand()
        {
            RunOnMainMenuScene(scene =>
            {
                int changes = ApplyCanvasScalerExpand(scene);
                Debug.Log($"[ResponsiveMenu] Canvas Scaler — {changes} cambio(s).");
                return changes > 0;
            });
        }

        // ==================================================================
        // Paso único: scaler
        // ==================================================================

        private static int ApplyCanvasScalerExpand(Scene scene)
        {
            int changes = 0;
            foreach (var scaler in FindAllInScene<CanvasScaler>(scene))
            {
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                if (scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand) continue;

                Undo.RecordObject(scaler, "Responsive Menu — Canvas Scaler Expand");
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                EditorUtility.SetDirty(scaler);
                changes++;
                Debug.Log($"[ResponsiveMenu] CanvasScaler de '{scaler.gameObject.name}' → Expand.", scaler);
            }
            if (changes == 0) Debug.Log("[ResponsiveMenu] CanvasScaler ya estaba en Expand — sin cambios.");
            return changes;
        }

        // ==================================================================
        // Infra
        // ==================================================================

        private static void RunOnMainMenuScene(System.Func<Scene, bool> apply)
        {
            var scene = SceneManager.GetSceneByPath(MainMenuScenePath);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            bool wasDirty = wasOpen && scene.isDirty;

            if (!wasOpen)
            {
                scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
            }

            bool changed;
            try
            {
                changed = apply(scene);
            }
            finally
            {
                if (!wasOpen)
                {
                    // Escena abierta por el tool: guardar y cerrar siempre.
                    EditorSceneManager.SaveScene(scene);
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                    scene = default;
                }
            }

            if (!scene.IsValid()) return;

            if (changed) EditorSceneManager.MarkSceneDirty(scene);

            if (wasDirty)
            {
                // Cambios del usuario sin guardar: no pisamos nada — guarda a mano.
                Debug.LogWarning("[ResponsiveMenu] 01_MainMenu estaba dirty (cambios sin guardar): " +
                                 "los ajustes se aplicaron pero NO se guardó la escena. Guardala vos.");
            }
            else if (changed)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ResponsiveMenu] 01_MainMenu guardada.");
            }
        }

        private static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(includeInactive: true));
            }
            return results;
        }
    }
}
