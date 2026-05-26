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

        public int SetCount => EnemySets?.Count ?? 0;

        public EnemyDataSO GetEnemyForSet(int setIndex)
        {
            if (EnemySets == null || setIndex < 0 || setIndex >= EnemySets.Count) return null;
            return EnemySets[setIndex];
        }
    }
}
