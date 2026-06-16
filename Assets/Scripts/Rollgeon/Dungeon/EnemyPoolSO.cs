using System.Collections.Generic;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    [CreateAssetMenu(menuName = "Rollgeon/Dungeon/Enemy Pool", fileName = "EnemyPool")]
    public class EnemyPoolSO : ScriptableObject
    {
        public List<WeightedEntry<EnemyDataSO>> Entries = new();

        [Tooltip("Distribucion de tier opcional por entry, alineada por indice con Entries (#158). " +
                 "Vacio ⇒ Tier 1.")]
        public List<EnemyTierWeights> EntryTierWeights = new();

        /// <summary>Pesos de tier de un entry, o <c>null</c> si no hay (⇒ Tier 1).</summary>
        public EnemyTierWeights GetTierWeightsForEntry(int entryIndex)
        {
            if (EntryTierWeights == null || entryIndex < 0 || entryIndex >= EntryTierWeights.Count) return null;
            return EntryTierWeights[entryIndex];
        }

        public List<EnemyDataSO> RollForSpawns(int count, System.Random rng)
        {
            var indices = RollForSpawnIndices(count, rng);
            var result = new List<EnemyDataSO>(indices.Count);
            foreach (var i in indices)
                result.Add(i >= 0 ? Entries[i].Item : null);
            return result;
        }

        /// <summary>
        /// Como <see cref="RollForSpawns"/> pero devuelve los <b>indices</b> elegidos
        /// dentro de <see cref="Entries"/>, para que el caller pueda mapear los pesos de
        /// tier (<see cref="GetTierWeightsForEntry"/>) del entry rolleado. #158.
        /// </summary>
        public List<int> RollForSpawnIndices(int count, System.Random rng)
        {
            var result = new List<int>();

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
                int pickedIndex = -1;

                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].Weight <= 0f)
                        continue;

                    accumulated += Entries[i].Weight;
                    if (accumulated >= roll)
                    {
                        pickedIndex = i;
                        break;
                    }
                }

                // Fallback: last valid entry (rounding edge case)
                if (pickedIndex < 0)
                {
                    for (int i = Entries.Count - 1; i >= 0; i--)
                    {
                        if (Entries[i].Weight > 0f)
                        {
                            pickedIndex = i;
                            break;
                        }
                    }
                }

                result.Add(pickedIndex);
            }

            return result;
        }
    }
}
