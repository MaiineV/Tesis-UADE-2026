using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// Pool pesado de combo passives elegibles en la tienda. Mismo patrón que
    /// <c>ShopPoolSO</c> y <c>EnchantmentPoolSO</c>.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Combos/Combo Passive Pool",
        fileName = "ComboPassivePool")]
    public sealed class ComboPassivePoolSO : SerializedScriptableObject
    {
        [Title("Entries")]
        [InfoBox("Pool pesado. La tienda rolea N entries al stockear los slots de la sala.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        public List<WeightedComboPassive> Entries = new List<WeightedComboPassive>();

        /// <summary>Rolea una pasiva del pool. Devuelve null si no hay candidatos elegibles.</summary>
        public ComboPassiveSO Roll(
            System.Random rng,
            int floorDepth,
            IReadOnlyCollection<ComboPassiveSO> exclude = null)
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
                if (pick <= cursor) return Entries[i].Passive;
            }

            // Floating point drift — fallback al último elegible.
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (IsEligible(Entries[i], floorDepth, exclude)) return Entries[i].Passive;
            }
            return null;
        }

        private static bool IsEligible(
            WeightedComboPassive entry,
            int floorDepth,
            IReadOnlyCollection<ComboPassiveSO> exclude)
        {
            if (entry == null || entry.Passive == null) return false;
            if (entry.Weight <= 0f) return false;
            if (entry.MinFloorDepth > floorDepth) return false;
            if (exclude != null && exclude.Contains(entry.Passive)) return false;
            return true;
        }
    }
}
