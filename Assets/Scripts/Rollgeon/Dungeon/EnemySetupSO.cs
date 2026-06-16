using System;
using System.Collections.Generic;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Setup fijo de enemigos para una sala: asignación explícita de
    /// <c>enemy → spawn point index</c>. TECHNICAL.md §13.2 / §13.4 (PossibleSetups).
    /// <para>
    /// <see cref="RoomSO.PossibleSetups"/> lista varios setups candidatos; el
    /// generator elige uno aleatorio al entrar. Si la lista está vacía, cae al
    /// fallback procedural <see cref="EnemyPoolSO.RollForSpawns"/>.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Dungeon/Enemy Setup", fileName = "EnemySetup")]
    public sealed class EnemySetupSO : ScriptableObject
    {
        /// <summary>Nombre legible para debug / editor; no identity.</summary>
        public string SetupName;

        [Tooltip("Slots del setup: cada entry fija un enemigo a un índice de spawn point del RoomLayout.")]
        public List<SetupSlot> Slots = new List<SetupSlot>();

        /// <summary>
        /// Resuelve el setup validando que cada <see cref="SetupSlot.SpawnPointIndex"/>
        /// sea válido contra <paramref name="spawnPointCount"/>. Retorna
        /// <c>false</c> si algún slot es OOB o no tiene <see cref="SetupSlot.Enemy"/>.
        /// </summary>
        public bool TryResolve(int spawnPointCount, out List<(int index, EnemyDataSO data)> mapping)
        {
            mapping = new List<(int, EnemyDataSO)>(Slots.Count);
            if (spawnPointCount <= 0) return false;

            for (int i = 0; i < Slots.Count; i++)
            {
                var slot = Slots[i];
                if (slot.Enemy == null) return false;
                if (slot.SpawnPointIndex < 0 || slot.SpawnPointIndex >= spawnPointCount) return false;
                mapping.Add((slot.SpawnPointIndex, slot.Enemy));
            }
            return true;
        }
    }

    /// <summary>Una asignación dentro de un <see cref="EnemySetupSO"/>.</summary>
    [Serializable]
    public struct SetupSlot
    {
        [Tooltip("Índice al array RoomLayout.EnemySpawnPoints.")]
        [Min(0)] public int SpawnPointIndex;

        public EnemyDataSO Enemy;

        [Tooltip("Distribucion de tier opcional para este enemigo (#158). Null/vacio ⇒ Tier 1.")]
        public EnemyTierWeights TierWeights;
    }
}