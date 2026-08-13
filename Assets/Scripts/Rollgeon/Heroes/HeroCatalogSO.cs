using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Catalogs;
using UnityEngine;

namespace Rollgeon.Heroes
{
    /// <summary>
    /// Catálogo enumerable de clases de héroe. Da <c>AllIds</c>/<c>GetById</c> para que la
    /// DevConsole autocomplete por <c>EntityId</c> ("hero.*") y resuelva el SO al cambiar de clase.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Heroes/Hero Catalog")]
    public class HeroCatalogSO : BaseCatalogSO<ClassHeroSO>
    {
        protected override string GetIdOf(ClassHeroSO entry) => entry != null ? entry.EntityId : null;

#if UNITY_EDITOR
        [ContextMenu("Repopulate from AssetDatabase")]
        public void Repopulate()
        {
            _entries.Clear();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ClassHeroSO));
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ClassHeroSO>(path);
                if (asset != null && !_entries.Contains(asset)) _entries.Add(asset);
            }
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[HeroCatalogSO] Repopulated: {_entries.Count} clases.");
        }

        public static IEnumerable<string> GetEditorAllIds()
        {
            if (ServiceLocator.TryGetService<HeroCatalogSO>(out var registered) && registered != null)
                return registered.AllIds;

            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(HeroCatalogSO));
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroCatalogSO>(path);
                if (asset != null) return asset.AllIds;
            }
            return Array.Empty<string>();
        }
#endif
    }
}
