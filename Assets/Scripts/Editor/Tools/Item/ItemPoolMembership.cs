using System.Collections.Generic;
using Rollgeon.Items;
using Rollgeon.Loot;
using Rollgeon.Shop;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// En qué pools de aparición está un ítem, y cómo entrarlo o sacarlo.
    /// </summary>
    /// <remarks>
    /// Un ítem se consigue por más de una vía y hasta acá la tool solo conocía una. La tienda usa
    /// <c>ShopPoolSO</c> con precio; los cofres usan <c>LootPoolSO</c>, que es una lista pelada. Sin
    /// juntarlas, cualquier respuesta a "¿esto se puede conseguir?" es parcial — y de hecho la salud
    /// del catálogo avisaba que cinco ítems no estaban en tienda dando a entender que eran
    /// inalcanzables, cuando estaban en las cuatro pools de cofre.
    /// </remarks>
    public static class ItemPoolMembership
    {
        /// <summary>Una pool y si el ítem consultado está adentro.</summary>
        public readonly struct Entry
        {
            /// <summary>Nombre legible para la UI.</summary>
            public string Name { get; }

            /// <summary>El asset de la pool. Sirve para Ping y para pasarlo de vuelta a <see cref="Set"/>.</summary>
            public Object Pool { get; }

            public bool Contains { get; }

            /// <summary>La tienda cobra; las de cofre no. Determina si hace falta un precio al dar de alta.</summary>
            public bool IsShop { get; }

            internal Entry(string name, Object pool, bool contains, bool isShop)
            {
                Name = name;
                Pool = pool;
                Contains = contains;
                IsShop = isShop;
            }
        }

        static ShopPoolSO _shop;
        static List<LootPoolSO> _lootPools;

        /// <summary>
        /// Suelta las pools cacheadas. La llama el host al rebuildear la lista.
        /// </summary>
        /// <remarks>
        /// Se cachean porque encontrarlas es <c>AssetDatabase.FindAssets</c>, y esto se consulta desde
        /// el panel, o sea en cada repaint. Ese patrón ya costó ~13 ms por frame en esta ventana.
        /// </remarks>
        public static void InvalidateCache()
        {
            _shop = null;
            _lootPools = null;
        }

        static ShopPoolSO Shop => _shop != null ? _shop : (_shop = ItemShopPriceBridge.LoadDefaultPool());

        static List<LootPoolSO> LootPools
        {
            get
            {
                if (_lootPools != null) return _lootPools;

                _lootPools = new List<LootPoolSO>();
                foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(LootPoolSO)))
                {
                    var so = AssetDatabase.LoadAssetAtPath<LootPoolSO>(AssetDatabase.GUIDToAssetPath(guid));
                    if (so != null) _lootPools.Add(so);
                }
                _lootPools.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                return _lootPools;
            }
        }

        /// <summary>Todas las pools de aparición, marcando en cuáles está <paramref name="item"/>.</summary>
        public static IReadOnlyList<Entry> GetPools(ItemSO item)
        {
            var result = new List<Entry>();
            if (item == null) return result;

            var shop = Shop;
            if (shop != null)
                result.Add(new Entry("Tienda", shop, ItemShopPriceBridge.IsInPool(shop, item), true));

            foreach (var loot in LootPools)
                result.Add(new Entry(loot.name, loot, loot.Items != null && loot.Items.Contains(item), false));

            return result;
        }

        /// <summary>
        /// <c>true</c> si el ítem se puede conseguir por alguna vía.
        /// </summary>
        /// <remarks>
        /// Lo que la salud del catálogo tendría que preguntar. "No está en la tienda" no implica
        /// inalcanzable: puede salir de un cofre.
        /// </remarks>
        public static bool IsInAnyPool(ItemSO item)
        {
            foreach (var e in GetPools(item))
                if (e.Contains) return true;
            return false;
        }

        /// <summary>
        /// Entra o saca <paramref name="item"/> de <paramref name="pool"/>. Devuelve si cambió algo.
        /// </summary>
        /// <param name="shopPrice">Precio para el alta en tienda. Se ignora en las de cofre, que no cobran.</param>
        public static bool Set(ItemSO item, Object pool, bool member, int shopPrice = 0)
        {
            if (item == null || pool == null) return false;

            if (pool is ShopPoolSO shop)
            {
                return member
                    ? ItemShopPriceBridge.AddToPool(shop, item, shopPrice)
                    : ItemShopPriceBridge.RemoveFromPool(shop, item);
            }

            if (!(pool is LootPoolSO loot)) return false;

            loot.Items ??= new List<ItemSO>();
            bool has = loot.Items.Contains(item);
            if (has == member) return false;

            Undo.RecordObject(loot, member ? "Add Item To Loot Pool" : "Remove Item From Loot Pool");
            if (member) loot.Items.Add(item);
            else loot.Items.Remove(item);
            EditorUtility.SetDirty(loot);
            return true;
        }
    }
}
