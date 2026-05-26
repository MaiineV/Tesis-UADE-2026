using System.Collections.Generic;
using System.Linq;
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
        /// Rolea un ítem del pool filtrando por <paramref name="floorDepth"/>, peso
        /// y <paramref name="exclude"/> (entries ya rolleadas en slots previos).
        /// Devuelve <c>default</c> si no hay entries elegibles. Si todos los
        /// compatibles están excluidos, hace fallback ignorando el exclude.
        /// </summary>
        public ShopRollResult Roll(
            System.Random rng,
            int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> exclude = null)
        {
            if (Items == null || Items.Count == 0) return default;

            var picked = TryRollFiltered(rng, floorDepth, exclude);
            if (picked.Item != null) return picked;

            // Fallback: ignorar el exclude — mejor un duplicado que un slot vacío.
            return TryRollFiltered(rng, floorDepth, exclude: null);
        }

        private ShopRollResult TryRollFiltered(
            System.Random rng,
            int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> exclude)
        {
            float total = 0f;
            for (int i = 0; i < Items.Count; i++)
            {
                if (!IsEligible(Items[i], floorDepth, exclude)) continue;
                total += Items[i].Weight;
            }

            if (total <= 0f) return default;

            float pick = (float)rng.NextDouble() * total;
            float cursor = 0f;

            for (int i = 0; i < Items.Count; i++)
            {
                if (!IsEligible(Items[i], floorDepth, exclude)) continue;
                cursor += Items[i].Weight;
                if (pick <= cursor)
                {
                    return new ShopRollResult
                    {
                        Item = Items[i].GetEntry(),
                        BasePrice = Items[i].BasePrice,
                    };
                }
            }

            // Floating point drift — fallback al último eligible.
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (!IsEligible(Items[i], floorDepth, exclude)) continue;
                return new ShopRollResult
                {
                    Item = Items[i].GetEntry(),
                    BasePrice = Items[i].BasePrice,
                };
            }
            return default;
        }

        private static bool IsEligible(
            WeightedShopItem entry,
            int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> exclude = null)
        {
            var rewardEntry = entry.GetEntry();
            if (rewardEntry == null) return false; // descarta SOs que no implementan IShopRewardEntry
            if (entry.Weight <= 0f) return false;
            if (entry.MinFloorDepth > floorDepth) return false;
            if (exclude != null && exclude.Contains(rewardEntry)) return false;
            return true;
        }
    }
}
