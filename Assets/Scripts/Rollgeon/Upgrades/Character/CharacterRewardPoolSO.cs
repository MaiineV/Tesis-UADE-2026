using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Character
{
    /// <summary>
    /// Pool pesado de Character Rewards. Una sala de boss-clear rolea 3 entries
    /// (sin repetidos) y los spawnea en pedestales para que el player elija.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Character/Character Reward Pool",
        fileName = "CharacterRewardPool")]
    public sealed class CharacterRewardPoolSO : SerializedScriptableObject
    {
        [Title("Entries")]
        [InfoBox("Pool pesado de rewards. Al clearear un boss, se rolea 3 entries distintas.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        public List<WeightedCharacterReward> Entries = new List<WeightedCharacterReward>();

        /// <summary>
        /// Rolea una entry del pool, opcionalmente excluyendo las ya rolleadas
        /// (para asegurar 3 opciones distintas por sala).
        /// </summary>
        public CharacterRewardSO Roll(
            System.Random rng,
            int floorDepth,
            IReadOnlyCollection<CharacterRewardSO> exclude = null)
        {
            if (Entries == null || Entries.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!IsEligible(Entries[i], floorDepth, exclude)) continue;
                total += Entries[i].Weight;
            }
            if (total <= 0f) return null;

            float pick = (float)rng.NextDouble() * total;
            float cursor = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!IsEligible(Entries[i], floorDepth, exclude)) continue;
                cursor += Entries[i].Weight;
                if (pick <= cursor) return Entries[i].Reward;
            }

            // Floating point drift — fallback al último elegible.
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (IsEligible(Entries[i], floorDepth, exclude)) return Entries[i].Reward;
            }
            return null;
        }

        private static bool IsEligible(
            WeightedCharacterReward entry,
            int floorDepth,
            IReadOnlyCollection<CharacterRewardSO> exclude)
        {
            if (entry == null || entry.Reward == null) return false;
            if (entry.Weight <= 0f) return false;
            if (entry.MinFloorDepth > floorDepth) return false;
            if (exclude != null && exclude.Contains(entry.Reward)) return false;
            return true;
        }
    }
}
