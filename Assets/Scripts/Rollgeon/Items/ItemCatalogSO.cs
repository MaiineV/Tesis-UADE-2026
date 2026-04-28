using System.Collections.Generic;
using System.Linq;
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
    }
}
