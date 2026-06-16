using System.Collections.Generic;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Per-spawn-point enemy configuration. Each entry is one enemy set;
    /// all spawn points in a room share the same set index chosen at room start.
    /// </summary>
    public sealed class SpawnPointConfig : MonoBehaviour
    {
        [Tooltip("Each entry is one enemy set. All spawn points use the same set index chosen at room start.")]
        public List<EnemyDataSO> EnemySets = new List<EnemyDataSO>();

        [Tooltip("Distribucion de tier por set, alineada por indice con EnemySets (#158). " +
                 "Sin entry / vacio ⇒ Tier 1.")]
        public List<EnemyTierWeights> SetTierWeights = new List<EnemyTierWeights>();

        public int SetCount => EnemySets?.Count ?? 0;

        public EnemyDataSO GetEnemyForSet(int setIndex)
        {
            if (EnemySets == null || setIndex < 0 || setIndex >= EnemySets.Count) return null;
            return EnemySets[setIndex];
        }

        /// <summary>Pesos de tier del set, o <c>null</c> si no hay (⇒ Tier 1).</summary>
        public EnemyTierWeights GetTierWeightsForSet(int setIndex)
        {
            if (SetTierWeights == null || setIndex < 0 || setIndex >= SetTierWeights.Count) return null;
            return SetTierWeights[setIndex];
        }
    }
}
