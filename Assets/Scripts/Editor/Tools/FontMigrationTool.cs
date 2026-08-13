using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Migración one-shot a la fuente única del juego
    /// (<c>Rollgeon → Tools → Apply m6x11plus Font</c>). Idempotente y re-ejecutable:
    /// regenera <c>Assets/Fonts/m6x11plus SDF.asset</c> desde el .ttf, lo setea como
    /// default de TMP Settings y re-apunta todos los <see cref="TMP_Text"/> de los
    /// prefabs propios y las escenas del juego. Correr de nuevo cada vez que se
    /// agreguen prefabs/escenas con texto que no hayan nacido con el default.
    /// </summary>
    public static class FontMigrationTool
    {
        private const string LogPrefix = "[FontMigrationTool] ";

        private const string SourceFontPath = "Assets/Fonts/m6x11plus.ttf";
        private const string TargetAssetPath = "Assets/Fonts/m6x11plus SDF.asset";
        private const string LiberationPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // Solo assets propios — Feel y demás third-party quedan afuera a propósito.
        private static readonly string[] PrefabFolders = { "Assets/Prefabs", "Assets/Rollgeon" };
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/00_Bootstrap.unity",
            "Assets/Scenes/01_MainMenu.unity",
            "Assets/Scenes/02_Gameplay.unity",
        };

        [MenuItem("Rollgeon/Tools/Apply m6x11plus Font")]
        public static void Apply()
        {
            // El rewire abre y guarda las escenas del juego — cambios abiertos sin
            // guardar deben resolverse antes (el diálogo permite cancelar todo).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning(LogPrefix + "Cancelado: hay escenas modificadas sin guardar.");
                return;
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath);
            if (sourceFont == null || liberation == null)
            {
                Debug.LogError(LogPrefix + $"Falta '{SourceFontPath}' o '{LiberationPath}' — abortado.");
                return;
            }

            var fontAsset = CreateOrReplaceFontAsset(sourceFont, liberation);
            if (fontAsset == null) return;

            SetTmpDefaultFont(fontAsset);

            // Prefabs primero: las instancias en escena heredan el cambio y el paso
            // de escenas las saltea sin crear overrides redundantes.
            int prefabsChanged = RewirePrefabs(fontAsset);
            int scenesChanged = RewireScenes(fontAsset);

            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + $"Migración completa. Font asset: {TargetAssetPath}. " +
                      $"Prefabs modificados: {prefabsChanged}. Escenas modificadas: {scenesChanged}.");
        }

        // ==================================================================
        // Font asset
        // ==================================================================

        /// <summary>
        /// Atlas dinámico + multi-atlas: los glifos se agregan on demand, sin listas
        /// de caracteres hardcodeadas. Fallback a LiberationSans para cualquier glifo
        /// que la pixel font no traiga.
        /// </summary>
        private static TMP_FontAsset CreateOrReplaceFontAsset(Font sourceFont, TMP_FontAsset fallback)
        {
            AssetDatabase.DeleteAsset(TargetAssetPath);

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            if (fontAsset == null)
            {
                Debug.LogError(LogPrefix + "TMP_FontAsset.CreateFontAsset devolvió null — abortado.");
                return null;
            }

            fontAsset.name = "m6x11plus SDF";
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

            AssetDatabase.CreateAsset(fontAsset, TargetAssetPath);

            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }

            // El GUID del .ttf fuente es lo que permite al atlas dinámico regenerar
            // glifos tras reimport — CreateFontAsset no siempre lo persiste.
            var so = new SerializedObject(fontAsset);
            var guidProp = so.FindProperty("m_SourceFontFileGUID");
            if (guidProp != null)
            {
                guidProp.stringValue = AssetDatabase.AssetPathToGUID(SourceFontPath);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        private static void SetTmpDefaultFont(TMP_FontAsset fontAsset)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogError(LogPrefix + $"No se encontró '{TmpSettingsPath}' — default no actualizado.");
                return;
            }

            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // ==================================================================
        // Rewire
        // ==================================================================

        private static int RewirePrefabs(TMP_FontAsset fontAsset)
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", PrefabFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (RetargetHierarchy(root, fontAsset) == 0) continue;

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return changed;
        }

        private static int RewireScenes(TMP_FontAsset fontAsset)
        {
            int changed = 0;
            foreach (var scenePath in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int retargeted = 0;
                foreach (var root in scene.GetRootGameObjects())
                    retargeted += RetargetHierarchy(root, fontAsset);

                if (retargeted == 0) continue;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changed++;
            }
            return changed;
        }

        private static int RetargetHierarchy(GameObject root, TMP_FontAsset fontAsset)
        {
            int retargeted = 0;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                if (Retarget(text, fontAsset)) retargeted++;
            }
            retargeted += StripLeakedMaterialOverrides(root);
            return retargeted;
        }

        /// <summary>
        /// Los prefab instances pueden arrastrar overrides huérfanos de
        /// <c>m_fontMaterial(s)</c> (ej. <c>data[0]</c> de un array que ya quedó
        /// vacío): Unity los conserva en <c>m_Modifications</c> aunque no apliquen,
        /// y mantienen vivos los materiales leakeados embebidos en la escena. Esos
        /// paths solo los serializa TMP y nunca son estado legítimo de instancia —
        /// se dropean enteros (el valor heredado del prefab ya quedó limpio).
        /// </summary>
        private static int StripLeakedMaterialOverrides(GameObject root)
        {
            int stripped = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                var go = transform.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;

                var mods = PrefabUtility.GetPropertyModifications(go);
                if (mods == null) continue;

                var kept = mods.Where(m => !IsFontMaterialPath(m.propertyPath)).ToArray();
                if (kept.Length == mods.Length) continue;

                PrefabUtility.SetPropertyModifications(go, kept);
                EditorUtility.SetDirty(go);
                stripped += mods.Length - kept.Length;
            }
            return stripped;
        }

        private static bool IsFontMaterialPath(string propertyPath)
        {
            return propertyPath == "m_fontMaterial"
                   || propertyPath.StartsWith("m_fontMaterials.Array")
                   || propertyPath.StartsWith("m_fontSharedMaterials.Array");
        }

        /// <summary>
        /// Se edita por SerializedObject y no por las properties de TMP_Text: los
        /// setters disparan lógica de runtime (rebuild de mesh, resolución de
        /// material) que no queremos sobre prefabs cargados fuera de escena, y la
        /// comparación por valor serializado evita crear overrides redundantes en
        /// instancias que ya heredan la fuente correcta.
        /// Además de font + sharedMaterial se limpian <c>m_fontMaterial</c> y los
        /// arrays <c>m_fontMaterials</c>/<c>m_fontSharedMaterials</c>: son instancias
        /// de material leakeadas que quedan serializadas en escenas/prefabs apuntando
        /// al atlas de la fuente vieja — TMP los reconstruye solo desde sharedMaterial.
        /// </summary>
        private static bool Retarget(TMP_Text text, TMP_FontAsset fontAsset)
        {
            var so = new SerializedObject(text);
            var fontProp = so.FindProperty("m_fontAsset");
            var matProp = so.FindProperty("m_sharedMaterial");
            if (fontProp == null || matProp == null)
            {
                Debug.LogWarning(LogPrefix + $"TMP_Text sin m_fontAsset/m_sharedMaterial serializados: {text.name}.");
                return false;
            }

            var instMatProp = so.FindProperty("m_fontMaterial");
            var instArrayProp = so.FindProperty("m_fontMaterials");
            var sharedArrayProp = so.FindProperty("m_fontSharedMaterials");

            bool alreadyTargeted = fontProp.objectReferenceValue == fontAsset
                                   && matProp.objectReferenceValue == fontAsset.material
                                   && (instMatProp == null || instMatProp.objectReferenceValue == null)
                                   && (instArrayProp == null || instArrayProp.arraySize == 0)
                                   && (sharedArrayProp == null || sharedArrayProp.arraySize == 0);
            if (alreadyTargeted) return false;

            fontProp.objectReferenceValue = fontAsset;
            matProp.objectReferenceValue = fontAsset.material;
            if (instMatProp != null) instMatProp.objectReferenceValue = null;
            ClearMaterialArray(instArrayProp);
            ClearMaterialArray(sharedArrayProp);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(text);
            return true;
        }

        /// <summary>
        /// Nullear cada elemento antes de achicar el array: en prefab instances,
        /// achicar directo puede dejar overrides huérfanos <c>data[N]</c> en
        /// m_Modifications que mantienen vivo el material leakeado.
        /// </summary>
        private static void ClearMaterialArray(SerializedProperty arrayProp)
        {
            if (arrayProp == null) return;
            for (int i = 0; i < arrayProp.arraySize; i++)
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
            arrayProp.arraySize = 0;
        }
    }
}
