using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Patterns.Catalogs;
using UnityEngine;

namespace Rollgeon.Items
{
    [CreateAssetMenu(menuName = "Rollgeon/Items/Item Catalog")]
    public class ItemCatalogSO : BaseCatalogSO<ItemSO>
    {
        protected override string GetIdOf(ItemSO entry) => entry.ItemId;

        public IEnumerable<ItemSO> GetByType(ItemType type) =>
            _entries.Where(e => e != null && e.Type == type);

        public IEnumerable<ItemSO> GetByRarity(ItemRarity rarity) =>
            _entries.Where(e => e != null && e.Rarity == rarity);

#if UNITY_EDITOR
        /// <summary>
        /// Helper para los <c>[ValueDropdown]</c> de los efectos / preconditions / SO de
        /// items: en EditMode el <c>ServiceLocator</c> no tiene los catálogos registrados
        /// (sólo se cablean al correr <c>00_Bootstrap</c>), así que cuando el lookup falla
        /// se busca el catálogo via <c>AssetDatabase</c>. Devuelve los IDs del primer
        /// <see cref="ItemCatalogSO"/> que encuentre en el proyecto.
        /// </summary>
        public static IEnumerable<string> GetEditorAllIds()
        {
            if (ServiceLocator.TryGetService<ItemCatalogSO>(out var registered) && registered != null)
                return registered.AllIds;

            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ItemCatalogSO));
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(path);
                if (asset != null) return asset.AllIds;
            }
            return Array.Empty<string>();
        }
#endif
    }
}
