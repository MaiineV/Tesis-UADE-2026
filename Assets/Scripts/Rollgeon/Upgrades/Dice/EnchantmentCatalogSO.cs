using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Catalogs;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Catálogo enumerable de encantamientos. Da <c>AllIds</c>/<c>GetById</c> para que la
    /// DevConsole autocomplete por <c>UpgradeId</c> ("ench.*") y resuelva el SO al aplicar.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Upgrades/Dice/Enchantment Catalog")]
    public class EnchantmentCatalogSO : BaseCatalogSO<EnchantmentSO>
    {
        protected override string GetIdOf(EnchantmentSO entry) => entry != null ? entry.UpgradeId : null;

#if UNITY_EDITOR
        [ContextMenu("Repopulate from AssetDatabase")]
        public void Repopulate()
        {
            _entries.Clear();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(EnchantmentSO));
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (asset != null && !_entries.Contains(asset)) _entries.Add(asset);
            }
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[EnchantmentCatalogSO] Repopulated: {_entries.Count} encantamientos.");
        }

        /// <summary>Fallback editor-time: el ServiceLocator no tiene catálogos fuera de Play.</summary>
        public static IEnumerable<string> GetEditorAllIds()
        {
            if (ServiceLocator.TryGetService<EnchantmentCatalogSO>(out var registered) && registered != null)
                return registered.AllIds;

            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(EnchantmentCatalogSO));
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<EnchantmentCatalogSO>(path);
                if (asset != null) return asset.AllIds;
            }
            return Array.Empty<string>();
        }
#endif
    }
}
