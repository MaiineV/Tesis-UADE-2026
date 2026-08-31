using System.IO;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Editor.Tools.Enemy.Templates;
using Rollgeon.Entities;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>Crear, duplicar y plantillar fichas desde el editor, sin pasar por el Project window.</summary>
    public static class EnemyAssetOps
    {
        public const string DefaultFolder = "Assets/Rollgeon/Enemies";

        public static EnemyDataSO CreateNew(string folder = DefaultFolder)
        {
            EnsureFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/ED_NewEnemy.asset");
            var so = ScriptableObject.CreateInstance<EnemyDataSO>();
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            return so;
        }

        /// <summary>Asset nuevo con la ficha, stats y árbol del arquetipo ya cargados.</summary>
        public static EnemyDataSO CreateFromTemplate(EnemyTemplate template, string folder = DefaultFolder)
        {
            if (template == null) return null;
            EnsureFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/ED_{template.Name}.asset");
            var so = ScriptableObject.CreateInstance<EnemyDataSO>();
            EnemyArchetypeTemplates.ApplyTo(template, so);
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            return so;
        }

        /// <summary>
        /// Copia el asset entero (CopyAsset preserva el blob Odin: árbol, behaviors, tiers) y le
        /// da identidad propia: EntityId y nombre con sufijo, más el sidecar de layout del canvas
        /// copiado al id nuevo para que el árbol se abra igual que el original.
        /// </summary>
        public static EnemyDataSO Duplicate(EnemyDataSO source, string layoutsDir = AITreeLayoutSidecar.LayoutsDir)
        {
            if (source == null) return null;
            string srcPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(srcPath)) return null;
            return CopyTo(source, AssetDatabase.GenerateUniqueAssetPath(srcPath),
                string.IsNullOrEmpty(source.EntityId) ? null : source.EntityId + "_copia",
                string.IsNullOrEmpty(source.DisplayName) ? null : source.DisplayName + " (copia)",
                layoutsDir);
        }

        /// <summary>
        /// Guarda una copia bajo <see cref="EnemyTemplateCatalog.TemplatesFolder"/> con EntityId
        /// <c>tpl.*</c>: desde ahí "Nuevo enemigo ▾" la ofrece como plantilla del designer.
        /// </summary>
        public static EnemyDataSO SaveAsTemplate(EnemyDataSO source, string templatesFolder = EnemyTemplateCatalog.TemplatesFolder,
                                                 string layoutsDir = AITreeLayoutSidecar.LayoutsDir)
        {
            if (source == null) return null;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(source))) return null;
            EnsureFolder(templatesFolder);
            string file = "ET_" + StripPrefix(source.name, "ED_");
            string dst = AssetDatabase.GenerateUniqueAssetPath($"{templatesFolder}/{file}.asset");
            string id = string.IsNullOrEmpty(source.EntityId) ? source.name : StripPrefix(source.EntityId, EnemyTemplateCatalog.EntityIdPrefix);
            return CopyTo(source, dst, EnemyTemplateCatalog.EntityIdPrefix + id, source.DisplayName, layoutsDir);
        }

        /// <summary>Enemigo jugable nuevo a partir de una plantilla del designer (copia sin sufijos).</summary>
        public static EnemyDataSO CreateFromAsset(EnemyDataSO template, string folder = DefaultFolder,
                                                  string layoutsDir = AITreeLayoutSidecar.LayoutsDir)
        {
            if (template == null) return null;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(template))) return null;
            EnsureFolder(folder);
            string file = "ED_" + StripPrefix(template.name, "ET_");
            string dst = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{file}.asset");
            string id = string.IsNullOrEmpty(template.EntityId) ? null : "enemy." + StripPrefix(template.EntityId, EnemyTemplateCatalog.EntityIdPrefix);
            return CopyTo(template, dst, id, template.DisplayName, layoutsDir);
        }

        static EnemyDataSO CopyTo(EnemyDataSO source, string dstPath, string entityIdOrNull, string displayNameOrNull, string layoutsDir)
        {
            string srcPath = AssetDatabase.GetAssetPath(source);
            if (!AssetDatabase.CopyAsset(srcPath, dstPath)) return null;

            var copy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(dstPath);
            if (copy == null) return null;

            string srcId = string.IsNullOrEmpty(source.EntityId) ? source.name : source.EntityId;
            if (entityIdOrNull != null) copy.EntityId = entityIdOrNull;
            if (displayNameOrNull != null) copy.DisplayName = displayNameOrNull;
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();

            string newId = string.IsNullOrEmpty(copy.EntityId) ? copy.name : copy.EntityId;
            string srcLayout = AITreeLayoutSidecar.PathForId(srcId, layoutsDir);
            string dstLayout = AITreeLayoutSidecar.PathForId(newId, layoutsDir);
            // Mismo criterio que el sidecar: archivo en disco, sin Refresh en medio de la edición.
            if (File.Exists(srcLayout) && !File.Exists(dstLayout)) File.Copy(srcLayout, dstLayout);

            return copy;
        }

        static string StripPrefix(string s, string prefix)
            => !string.IsNullOrEmpty(s) && s.StartsWith(prefix) ? s.Substring(prefix.Length) : s;

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
