using System;
using System.Collections.Generic;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>Peso de aparición de un tier (1-based) en un spawner. Ticket #158.</summary>
    [Serializable]
    public struct TierWeight
    {
        [Min(1)] public int Tier;
        [Min(0f)] public float Weight;
    }

    /// <summary>
    /// Distribución de probabilidad de tier para un enemigo en un spawner concreto.
    /// Reusa el patrón acumulador de <see cref="EnemyPoolSO.RollForSpawns"/>.
    /// Vacío o todo-cero ⇒ Tier 1 (backward-compatible).
    /// </summary>
    [Serializable]
    public class EnemyTierWeights
    {
        public List<TierWeight> Weights = new List<TierWeight>();

        /// <summary>Rollea un tier 1-based según los pesos. Vacío/zero ⇒ 1.</summary>
        public int RollTier(System.Random rng)
        {
            if (Weights == null || Weights.Count == 0) return 1;

            float total = 0f;
            for (int i = 0; i < Weights.Count; i++)
                if (Weights[i].Weight > 0f) total += Weights[i].Weight;

            if (total <= 0f) return 1;

            double roll = (rng != null ? rng.NextDouble() : UnityEngine.Random.value) * total;
            double accumulated = 0.0;
            for (int i = 0; i < Weights.Count; i++)
            {
                if (Weights[i].Weight <= 0f) continue;
                accumulated += Weights[i].Weight;
                if (accumulated >= roll) return Mathf.Max(1, Weights[i].Tier);
            }

            // Fallback por redondeo: último peso positivo.
            for (int i = Weights.Count - 1; i >= 0; i--)
                if (Weights[i].Weight > 0f) return Mathf.Max(1, Weights[i].Tier);

            return 1;
        }
    }

    /// <summary>
    /// Helper central que rollea un tier y lo clampea contra los tiers que el
    /// enemigo realmente define — evita tiers OOB si un diseñador pone probabilidad
    /// de T3 en un enemigo que solo tiene 2 tiers.
    /// </summary>
    public static class EnemyTierRoll
    {
        public static int Roll(EnemyTierWeights weights, EnemyDataSO enemy, System.Random rng)
        {
            int tier = weights != null ? weights.RollTier(rng) : 1;
            return enemy != null ? enemy.ClampTier(tier) : Mathf.Max(1, tier);
        }
    }
}
