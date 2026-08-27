using System.IO;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Entities;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>Crear y duplicar fichas desde el editor, sin pasar por el Project window.</summary>
    public static class EnemyAssetOps
    {
        public const string DefaultFolder = "Assets/Rollgeon/Enemies";

        public static EnemyDataSO CreateNew(string folder = DefaultFolder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/ED_NewEnemy.asset");
            var so = ScriptableObject.CreateInstance<EnemyDataSO>();
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

            string dstPath = AssetDatabase.GenerateUniqueAssetPath(srcPath);
            if (!AssetDatabase.CopyAsset(srcPath, dstPath)) return null;

            var copy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(dstPath);
            if (copy == null) return null;

            string srcId = string.IsNullOrEmpty(source.EntityId) ? source.name : source.EntityId;
            if (!string.IsNullOrEmpty(source.EntityId)) copy.EntityId = source.EntityId + "_copia";
            if (!string.IsNullOrEmpty(source.DisplayName)) copy.DisplayName = source.DisplayName + " (copia)";
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();

            string newId = string.IsNullOrEmpty(copy.EntityId) ? copy.name : copy.EntityId;
            string srcLayout = AITreeLayoutSidecar.PathForId(srcId, layoutsDir);
            string dstLayout = AITreeLayoutSidecar.PathForId(newId, layoutsDir);
            // Mismo criterio que el sidecar: archivo en disco, sin Refresh en medio de la edición.
            if (File.Exists(srcLayout) && !File.Exists(dstLayout)) File.Copy(srcLayout, dstLayout);

            return copy;
        }
    }
}
