using System;
using System.Collections.Generic;
using Rollgeon.Items;
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

        [Tooltip("Ítems del pool de este tier. El GDD confirma que el pool escala en " +
                 "tamaño y calidad con el tier; el contenido exacto es TBD-01.")]
        [ListDrawerSettings(ShowFoldout = false)]
        public List<ItemSO> Items = new List<ItemSO>();

        [MinValue(0)]
        [Tooltip("Oro mínimo del slot de oro del pool.")]
        public int GoldMin = 10;

        [MinValue(0)]
        [Tooltip("Oro máximo (inclusive) del slot de oro del pool.")]
        public int GoldMax = 20;
    }

    /// <summary>
    /// Pool de recompensas del cofre, por tier. El roll es <b>uniforme</b> entre los
    /// ítems del bucket + un slot de oro — placeholder explícito del GDD (§20 TBD-01)
    /// hasta que Balance defina pesos por rareza; la animación gacha sugiere que a
    /// futuro será ponderado, y en ese momento solo cambia <see cref="Roll"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Chests/Chest Loot Pool", fileName = "ChestLootPool")]
    public sealed class ChestLootPoolSO : SerializedScriptableObject
    {
        [Title("Buckets por tier")]
        [InfoBox("Una entry por ItemRarity. Cada bucket tiene N ítems + 1 slot de oro; " +
                 "el roll placeholder es uniforme entre los N+1 slots.")]
        [ListDrawerSettings(ShowFoldout = false)]
        [OdinSerialize]
        public List<ChestLootBucket> Buckets = new List<ChestLootBucket>();

        /// <summary>
        /// Rolea la recompensa del tier: uniforme entre <c>Items.Count + 1</c> slots
        /// (el +1 es el slot de oro). Bucket vacío o inexistente ⇒ oro.
        /// </summary>
        public ChestLootResult Roll(System.Random rng, ItemRarity tier)
        {
            var bucket = GetBucket(tier);
            if (bucket == null) return ChestLootResult.ForGold(0);

            int itemCount = CountValidItems(bucket);
            if (itemCount == 0) return ChestLootResult.ForGold(RollGold(rng, bucket));

            int slot = rng.Next(itemCount + 1);
            if (slot == itemCount) return ChestLootResult.ForGold(RollGold(rng, bucket));

            int cursor = 0;
            for (int i = 0; i < bucket.Items.Count; i++)
            {
                if (bucket.Items[i] == null) continue;
                if (cursor == slot) return ChestLootResult.ForItem(bucket.Items[i]);
                cursor++;
            }
            return ChestLootResult.ForGold(RollGold(rng, bucket));
        }

        /// <summary>
        /// Ítems del pool del tier, para el relleno del reel gacha. Nunca <c>null</c>;
        /// puede ser vacío (la UI degrada).
        /// </summary>
        public IReadOnlyList<ItemSO> GetPoolPreview(ItemRarity tier)
        {
            var bucket = GetBucket(tier);
            if (bucket == null) return Array.Empty<ItemSO>();

            var preview = new List<ItemSO>(bucket.Items.Count);
            for (int i = 0; i < bucket.Items.Count; i++)
            {
                if (bucket.Items[i] != null) preview.Add(bucket.Items[i]);
            }
            return preview;
        }

        public ChestLootBucket GetBucket(ItemRarity tier)
        {
            for (int i = 0; i < Buckets.Count; i++)
            {
                if (Buckets[i] != null && Buckets[i].Tier == tier) return Buckets[i];
            }
            return null;
        }

        private static int CountValidItems(ChestLootBucket bucket)
        {
            int count = 0;
            for (int i = 0; i < bucket.Items.Count; i++)
            {
                if (bucket.Items[i] != null) count++;
            }
            return count;
        }

        private static int RollGold(System.Random rng, ChestLootBucket bucket)
        {
            int min = Math.Max(0, bucket.GoldMin);
            int max = Math.Max(min, bucket.GoldMax);
            return rng.Next(min, max + 1);
        }
    }
}
