using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Pool pesado de ítems elegible en shops de un piso / tema. TECHNICAL.md §17.F.2.
    /// El diseñador edita este asset con los <see cref="ShopItemDef"/>, sus
    /// pesos y precios base. El <c>ShopManagerService</c> rolea N entries
    /// del pool al inicializar una shop room.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Rollgeon/Shop/Shop Pool",
        fileName = "ShopPool")]
    public sealed class ShopPoolSO : SerializedScriptableObject
    {
        [Title("Items disponibles")]
        [InfoBox("Pool pesado. Un entry se rolea por slot al entrar a la sala por primera vez. " +
                 "Los pesos son relativos. Entries con Weight = 0 se saltean.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        public List<WeightedShopItem> Items = new List<WeightedShopItem>();

        /// <summary>
        /// Rolea un ítem del pool filtrando por <paramref name="floorDepth"/> y
        /// peso. Devuelve <c>default</c> si no hay entries elegibles — el service
        /// loggea y salta el slot.
        /// </summary>
        public ShopRollResult Roll(System.Random rng, int floorDepth)
        {
            if (Items == null || Items.Count == 0) return default;

            float total = 0f;
            for (int i = 0; i < Items.Count; i++)
            {
                var entry = Items[i];
                if (entry.Item == null) continue;
                if (entry.Weight <= 0f) continue;
                if (entry.MinFloorDepth > floorDepth) continue;
                total += entry.Weight;
            }

            if (total <= 0f) return default;

            float pick = (float)rng.NextDouble() * total;
            float cursor = 0f;

            for (int i = 0; i < Items.Count; i++)
            {
                var entry = Items[i];
                if (entry.Item == null) continue;
                if (entry.Weight <= 0f) continue;
                if (entry.MinFloorDepth > floorDepth) continue;

                cursor += entry.Weight;
                if (pick <= cursor)
                {
                    return new ShopRollResult { Item = entry.Item, BasePrice = entry.BasePrice };
                }
            }

            // Floating point drift — fallback al último eligible.
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var entry = Items[i];
                if (entry.Item == null) continue;
                if (entry.Weight <= 0f) continue;
                if (entry.MinFloorDepth > floorDepth) continue;
                return new ShopRollResult { Item = entry.Item, BasePrice = entry.BasePrice };
            }
            return default;
        }
    }
}
