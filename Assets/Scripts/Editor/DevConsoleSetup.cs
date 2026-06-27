#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Rollgeon.Heroes;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Patterns.Catalogs;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Setup one-click de los catálogos que necesita la DevConsole (encantamientos y clases):
    /// los crea si faltan, los puebla desde el AssetDatabase y los agrega a ServiceBootstrap.asset
    /// (edición de objeto vivo, no YAML). Menú: Rollgeon → DevConsole → Setup Catalogs.
    /// </summary>
    public static class DevConsoleSetup
    {
        private const string EnchCatalogPath = "Assets/Rollgeon/Upgrades/Dice/EnchantmentCatalog.asset";
        private const string HeroCatalogPath = "Assets/Rollgeon/Heroes/HeroCatalog.asset";

        [MenuItem("Rollgeon/DevConsole/Setup Catalogs")]
        public static void SetupCatalogs()
        {
            var ench = GetOrCreate<EnchantmentCatalogSO>(EnchCatalogPath);
            ench.Repopulate();
            EditorUtility.SetDirty(ench);

            var hero = GetOrCreate<HeroCatalogSO>(HeroCatalogPath);
            hero.Repopulate();
            EditorUtility.SetDirty(hero);

            int added = AddToBootstrap(ench, hero);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DevConsoleSetup] Catálogos listos. " +
                      $"Encantamientos='{EnchCatalogPath}', Clases='{HeroCatalogPath}'. " +
                      $"Agregados a ServiceBootstrap.Catalogs: {added}.");
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            EnsureFolder(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var inst = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(inst, path);
            return inst;
        }

        private static void EnsureFolder(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;

            var parts = dir.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static int AddToBootstrap(params BaseCatalogSO[] catalogs)
        {
            var guids = AssetDatabase.FindAssets("t:ServiceBootstrapSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[DevConsoleSetup] No se encontró ServiceBootstrap.asset. " +
                                 "Agregá los catálogos a mano en su lista Catalogs.");
                return 0;
            }

            var bootstrap = AssetDatabase.LoadAssetAtPath<ServiceBootstrapSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (bootstrap == null) return 0;
            if (bootstrap.Catalogs == null) bootstrap.Catalogs = new List<BaseCatalogSO>();

            int added = 0;
            foreach (var c in catalogs)
            {
                if (c != null && !bootstrap.Catalogs.Contains(c))
                {
                    bootstrap.Catalogs.Add(c);
                    added++;
                }
            }
            EditorUtility.SetDirty(bootstrap);
            return added;
        }
    }
}
#endif
