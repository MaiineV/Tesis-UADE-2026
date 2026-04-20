using System.Collections.Generic;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    [CreateAssetMenu(menuName = "Rollgeon/Dungeon/Enemy Pool", fileName = "EnemyPool")]
    public class EnemyPoolSO : ScriptableObject
    {
        public List<WeightedEntry<EnemyDataSO>> Entries = new();

        public List<EnemyDataSO> RollForSpawns(int count, System.Random rng)
        {
            var result = new List<EnemyDataSO>();

            if (count <= 0 || Entries == null || Entries.Count == 0)
                return result;

            float totalWeight = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Weight > 0f)
                    totalWeight += Entries[i].Weight;
            }

            if (totalWeight <= 0f)
                return result;

            for (int r = 0; r < count; r++)
            {
                double roll = rng.NextDouble() * totalWeight;
                double accumulated = 0.0;
                EnemyDataSO picked = null;

                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].Weight <= 0f)
                        continue;

                    accumulated += Entries[i].Weight;
                    if (accumulated >= roll)
                    {
                        picked = Entries[i].Item;
                        break;
                    }
                }

                // Fallback: last valid entry (rounding edge case)
                if (picked == null)
                {
                    for (int i = Entries.Count - 1; i >= 0; i--)
                    {
                        if (Entries[i].Weight > 0f)
                        {
                            picked = Entries[i].Item;
                            break;
                        }
                    }
                }

                result.Add(picked);
            }

            return result;
        }
    }
}
