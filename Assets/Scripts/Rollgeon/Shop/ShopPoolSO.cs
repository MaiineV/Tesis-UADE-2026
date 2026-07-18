using System.Collections.Generic;
using System.Linq;
using Rollgeon.Meta;
using Rollgeon.Upgrades.Combos;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Pool pesado de ítems elegible en shops de un piso / tema. TECHNICAL.md §17.F.2.
    /// El primer slot de la tienda lo ocupa siempre <see cref="Guaranteed"/> (la
    /// poción); el resto rolea del pool dinámico de combo passives
    /// (<see cref="PassivePool"/>, precio = <c>ShopCost</c> de cada pasiva) con
    /// las entries manuales de <see cref="Items"/> como extras/fallback.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Rollgeon/Shop/Shop Pool",
        fileName = "ShopPool")]
    public sealed class ShopPoolSO : SerializedScriptableObject
    {
        [Title("Slot garantizado")]
        [InfoBox("Entry que ocupa SIEMPRE el primer slot de la tienda (la poción). " +
                 "Item en null = sin garantizado, el slot rolea normal.")]
        [OdinSerialize]
        public WeightedShopItem Guaranteed;

        [Title("Pool dinámico — combo passives")]
        [InfoBox("Fuente dinámica de los slots restantes: el mismo ComboPassivePool que " +
                 "mantiene Upgrades. Agregar una pasiva nueva ALLÁ la hace rotar en la " +
                 "tienda sin tocar este asset. Precio = ShopCost de cada pasiva.")]
        [OdinSerialize]
        public ComboPassivePoolSO PassivePool;

        [Title("Items disponibles (extras / fallback)")]
        [InfoBox("Pool pesado manual. Se rolea cuando el pool dinámico no tiene candidatos " +
                 "elegibles (o no está cableado). Los pesos son relativos. Weight = 0 se saltea.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        public List<WeightedShopItem> Items = new List<WeightedShopItem>();

        /// <summary>
        /// Rolea un ítem del pool filtrando por <paramref name="floorDepth"/>, peso
        /// y <paramref name="exclude"/> (entries ya rolleadas en slots previos).
        /// Devuelve <c>default</c> si no hay entries elegibles. Si todos los
        /// compatibles están excluidos, hace fallback ignorando el exclude.
        /// </summary>
        public ShopRollResult Roll(
            System.Random rng,
            int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> exclude = null)
        {
            if (Items == null || Items.Count == 0) return default;

            var picked = TryRollFiltered(rng, floorDepth, exclude);
            if (picked.Item != null) return picked;

            // Fallback: ignorar el exclude — mejor un duplicado que un slot vacío.
            return TryRollFiltered(rng, floorDepth, exclude: null);
        }

        /// <summary>
        /// Entry del slot garantizado, si está cableada. El precio base sale de
        /// <see cref="Guaranteed"/>.<c>BasePrice</c> como cualquier entry manual.
        /// </summary>
        public bool TryGetGuaranteed(out ShopRollResult result)
        {
            var entry = Guaranteed.GetEntry();
            if (entry == null)
            {
                result = default;
                return false;
            }
            result = new ShopRollResult { Item = entry, BasePrice = Guaranteed.BasePrice };
            return true;
        }

        /// <summary>
        /// Roll para los slots no-garantizados: primero el pool dinámico de
        /// pasivas (precio = ShopCost), después las entries manuales como
        /// fallback. Mismo contrato de exclude que <see cref="Roll"/>.
        /// </summary>
        public ShopRollResult RollDynamic(
            System.Random rng,
            int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> exclude = null)
        {
            var fromPassives = RollPassive(rng, floorDepth, exclude);
            if (fromPassives.Item != null) return fromPassives;
            return Roll(rng, floorDepth, exclude);
        }

        private ShopRollResult RollPassive(
            System.Random rng,
            int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> exclude)
        {
            if (PassivePool == null || PassivePool.Entries == null || PassivePool.Entries.Count == 0)
                return default;

            // Exclude compuesto: pasivas ya roleadas en esta tienda + gateadas por
            // meta-unlock (paridad con IsEligible de las entries manuales — el
            // ComboPassivePoolSO no conoce el gate).
            var excluded = new List<ComboPassiveSO>();
            foreach (var entry in PassivePool.Entries)
            {
                var passive = entry?.Passive;
                if (passive == null) continue;
                if (!MetaUnlockGate.IsAvailable(UnlockableCategory.ShopItem, passive.UpgradeId))
                    excluded.Add(passive);
            }
            if (exclude != null)
            {
                foreach (var e in exclude)
                {
                    if (e is ComboPassiveSO p && !excluded.Contains(p)) excluded.Add(p);
                }
            }

            var rolled = PassivePool.Roll(rng, floorDepth, excluded);
            if (rolled == null) return default;

            return new ShopRollResult
            {
                Item = rolled,
                BasePrice = Mathf.Max(1, rolled.ShopCost),
            };
        }

        private ShopRollResult TryRollFiltered(
            System.Random rng,
            int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> exclude)
        {
            float total = 0f;
            for (int i = 0; i < Items.Count; i++)
            {
                if (!IsEligible(Items[i], floorDepth, exclude)) continue;
                total += Items[i].Weight;
            }

            if (total <= 0f) return default;

            float pick = (float)rng.NextDouble() * total;
            float cursor = 0f;

            for (int i = 0; i < Items.Count; i++)
            {
                if (!IsEligible(Items[i], floorDepth, exclude)) continue;
                cursor += Items[i].Weight;
                if (pick <= cursor)
                {
                    return new ShopRollResult
                    {
                        Item = Items[i].GetEntry(),
                        BasePrice = Items[i].BasePrice,
                    };
                }
            }

            // Floating point drift — fallback al último eligible.
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (!IsEligible(Items[i], floorDepth, exclude)) continue;
                return new ShopRollResult
                {
                    Item = Items[i].GetEntry(),
                    BasePrice = Items[i].BasePrice,
                };
            }
            return default;
        }

        private static bool IsEligible(
            WeightedShopItem entry,
            int floorDepth,
            IReadOnlyCollection<IShopRewardEntry> exclude = null)
        {
            var rewardEntry = entry.GetEntry();
            if (rewardEntry == null) return false; // descarta SOs que no implementan IShopRewardEntry
            if (entry.Weight <= 0f) return false;
            if (entry.MinFloorDepth > floorDepth) return false;
            if (exclude != null && exclude.Contains(rewardEntry)) return false;
            // Meta-progresión (#164): ítems gateados quedan fuera hasta desbloquearse.
            if (!MetaUnlockGate.IsAvailable(UnlockableCategory.ShopItem, rewardEntry.EntryId)) return false;
            return true;
        }
    }
}
