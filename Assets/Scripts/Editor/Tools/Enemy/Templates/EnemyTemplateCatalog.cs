using System;
using System.Collections.Generic;
using Rollgeon.Entities;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Enemy.Templates
{
    /// <summary>
    /// Plantillas del designer: cualquier <see cref="EnemyDataSO"/> guardado bajo
    /// <see cref="TemplatesFolder"/>. Son assets comunes (se editan con la misma tool); la
    /// carpeta es lo único que los distingue de un enemigo jugable.
    /// </summary>
    public static class EnemyTemplateCatalog
    {
        public const string TemplatesFolder = "Assets/Rollgeon/Enemies/Templates";
        public const string EntityIdPrefix = "tpl.";

        public static bool IsTemplatePath(string assetPath)
            => !string.IsNullOrEmpty(assetPath)
               && assetPath.StartsWith(TemplatesFolder + "/", StringComparison.OrdinalIgnoreCase);

        public static bool IsTemplate(EnemyDataSO so)
            => so != null && IsTemplatePath(AssetDatabase.GetAssetPath(so));

        public static List<EnemyDataSO> UserTemplates()
        {
            var list = new List<EnemyDataSO>();
            if (!AssetDatabase.IsValidFolder(TemplatesFolder)) return list;
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDataSO", new[] { TemplatesFolder }))
            {
                var so = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null) list.Add(so);
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }
    }
}
