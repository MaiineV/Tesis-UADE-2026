using System;
using System.Collections.Generic;
using Rollgeon.Items;
using Rollgeon.Loot;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Chests
{
    /// <summary>Resultado del roll de loot de un cofre abierto.</summary>
    public readonly struct ChestLootResult
    {
        public ItemSO Item { get; }
        public int GoldAmount { get; }
        public bool IsGold => Item == null;

        private ChestLootResult(ItemSO item, int goldAmount)
        {
            Item = item;
            GoldAmount = goldAmount;
        }

        public static ChestLootResult ForItem(ItemSO item) => new ChestLootResult(item, 0);
        public static ChestLootResult ForGold(int amount) => new ChestLootResult(null, amount);
    }

    /// <summary>Bucket de recompensas de un tier de cofre (TBD-01 del GDD como data).</summary>
    [Serializable]
    public class ChestLootBucket
    {
        public ItemRarity Tier;

        [Required]
        [Tooltip("Pool genérico del que rolean los ítems de este tier — define qué " +
                 "objetos puede integrar y el porcentaje por categoría de rareza.")]
        public LootPoolSO Pool;

        [Range(0f, 1f)]
        [Tooltip("Probabilidad de que el open dé oro en vez de ítem. Antes era un slot " +
                 "uniforme implícito 1/(N+1).")]
        public float GoldChance = 0.15f;

        [MinValue(0)]
        [Tooltip("Oro mínimo del slot de oro del pool.")]
        public int GoldMin = 10;

        [MinValue(0)]
        [Tooltip("Oro máximo (inclusive) del slot de oro del pool.")]
        public int GoldMax = 20;
    }

    /// <summary>
    /// Pool de recompensas del cofre, por tier: cada bucket referencia un
    /// <see cref="LootPoolSO"/> genérico (ítems + pesos por rareza, editable y
    /// reutilizable) más su slot de oro. La API que consume <c>ChestService</c>
    /// (<see cref="Roll"/> / <see cref="GetPoolPreview"/>) no cambió con la
    /// migración desde los buckets con lista inline.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Chests/Chest Loot Pool", fileName = "ChestLootPool")]
    public sealed class ChestLootPoolSO : SerializedScriptableObject
    {
        [Title("Buckets por tier")]
        [InfoBox("Una entry por ItemRarity: LootPool del tier + probabilidad y rango " +
                 "del slot de oro.")]
        [ListDrawerSettings(ShowFoldout = false)]
        [OdinSerialize]
        public List<ChestLootBucket> Buckets = new List<ChestLootBucket>();

        /// <summary>
        /// Rolea la recompensa del tier. Orden de rolls FIJO para determinismo por
        /// seed (patrón <c>ChestService.RollChest</c>): 1) chance de oro, 2) ítem del
        /// pool. Bucket inexistente, pool null o vacío ⇒ oro (degradación).
        /// </summary>
        public ChestLootResult Roll(System.Random rng, ItemRarity tier)
        {
            var bucket = GetBucket(tier);
            if (bucket == null) return ChestLootResult.ForGold(0);

            if (rng.NextDouble() < bucket.GoldChance)
                return ChestLootResult.ForGold(RollGold(rng, bucket));

            var item = bucket.Pool != null ? bucket.Pool.RollItem(rng) : null;
            return item != null
                ? ChestLootResult.ForItem(item)
                : ChestLootResult.ForGold(RollGold(rng, bucket));
        }

        /// <summary>
        /// Ítems del pool del tier, para el relleno del reel gacha. Nunca <c>null</c>;
        /// puede ser vacío (la UI degrada).
        /// </summary>
        public IReadOnlyList<ItemSO> GetPoolPreview(ItemRarity tier)
        {
            var bucket = GetBucket(tier);
            if (bucket == null || bucket.Pool == null) return Array.Empty<ItemSO>();
            return bucket.Pool.GetPreview();
        }

        public ChestLootBucket GetBucket(ItemRarity tier)
        {
            for (int i = 0; i < Buckets.Count; i++)
            {
                if (Buckets[i] != null && Buckets[i].Tier == tier) return Buckets[i];
            }
            return null;
        }

        private static int RollGold(System.Random rng, ChestLootBucket bucket)
        {
            int min = Math.Max(0, bucket.GoldMin);
            int max = Math.Max(min, bucket.GoldMax);
            return rng.Next(min, max + 1);
        }
    }
}
