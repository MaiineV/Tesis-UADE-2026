using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Items;
using Rollgeon.Shop;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Editor.Tools.Item
{
    public static partial class ItemQuery
    {
        /// <summary>
        /// Sentinel <see cref="ItemMetrics.ComboIds"/> entry for a <c>ComboPlayed</c> hook whose
        /// <see cref="ComboFilter"/> is <c>AnyCombo</c>/<c>None</c> — i.e. it fires for every combo,
        /// not a specific one. Not a real combo id, so it never collides with
        /// <c>BaseComboSO.GetKnownComboIds()</c> (all of which follow the <c>combo.*</c> convention).
        /// </summary>
        public const string AnyComboSentinel = "*";

        /// <summary>
        /// Per-item data for the metrics tab (spec §6.6): rarity, price, type, family, which events
        /// drive its hooks and which combos it's tied to. Enough to answer "does this Normal item
        /// out-damage that Épico" and to contrast against the GDD price table.
        /// </summary>
        public sealed class ItemMetrics
        {
            public ItemSO Asset { get; }
            public ItemRarity Rarity { get; }
            public string RarityLabel { get; }
            public ItemType Type { get; }
            public string FamilyId { get; }

            /// <summary>Effective price: the pool's <c>BasePrice</c> if the item is wired in, otherwise the GDD table's rarity price (see <see cref="PriceIsFallback"/>).</summary>
            public int Price { get; }

            /// <summary>True if <see cref="Price"/> did not come from the pool — the item isn't wired in, so this is a stand-in, not what a player would actually pay.</summary>
            public bool PriceIsFallback { get; }

            /// <summary>The GDD's rarity → price table value (spec §2), regardless of what the pool says — always populated, for the "contrast against GDD" view even when the item IS in the pool.</summary>
            public int GddBasePrice { get; }

            /// <summary>Distinct <see cref="EventName"/>s that drive this item's <c>EventBus</c> hooks.</summary>
            public IReadOnlyList<EventName> TriggerEvents { get; }

            /// <summary>Distinct combo ids this item's <c>ComboPlayed</c> hooks are tied to. <see cref="AnyComboSentinel"/> marks a hook that fires for any combo.</summary>
            public IReadOnlyList<string> ComboIds { get; }

            public ItemMetrics(
                ItemSO asset, ItemRarity rarity, string rarityLabel, ItemType type, string familyId,
                int price, bool priceIsFallback, int gddBasePrice,
                IReadOnlyList<EventName> triggerEvents, IReadOnlyList<string> comboIds)
            {
                Asset = asset;
                Rarity = rarity;
                RarityLabel = rarityLabel;
                Type = type;
                FamilyId = familyId;
                Price = price;
                PriceIsFallback = priceIsFallback;
                GddBasePrice = gddBasePrice;
                TriggerEvents = triggerEvents;
                ComboIds = comboIds;
            }
        }

        /// <summary>Metrics for every project item. <paramref name="pool"/> defaults to <see cref="ItemShopPriceBridge.LoadDefaultPool"/> when null.</summary>
        public static IReadOnlyList<ItemMetrics> GetMetrics(ShopPoolSO pool = null) => GetMetrics(GetAllItems(), pool);

        /// <summary>Pure form of <see cref="GetMetrics(ShopPoolSO)"/> — builds metrics for an arbitrary item list instead of scanning disk.</summary>
        public static IReadOnlyList<ItemMetrics> GetMetrics(IEnumerable<ItemSO> items, ShopPoolSO pool = null)
        {
            pool ??= ItemShopPriceBridge.LoadDefaultPool();
            return (items ?? Enumerable.Empty<ItemSO>())
                .Where(i => i != null)
                .Select(i => BuildMetrics(i, pool))
                .ToList();
        }

        /// <summary>Metrics for a single item. <paramref name="pool"/> defaults to <see cref="ItemShopPriceBridge.LoadDefaultPool"/> when null.</summary>
        public static ItemMetrics GetMetrics(ItemSO item, ShopPoolSO pool = null) =>
            BuildMetrics(item, pool ?? ItemShopPriceBridge.LoadDefaultPool());

        static ItemMetrics BuildMetrics(ItemSO item, ShopPoolSO pool)
        {
            var gddPrice = RarityPricing.BasePriceFor(item.Rarity);
            // Declarado afuera y no con `out var`: el && corta antes de llamar a TryGetPrice cuando
            // el pool es null, y ahí el compilador no puede probar que quedó asignado.
            int poolPrice = 0;
            var inPool = pool != null && ItemShopPriceBridge.TryGetPrice(pool, item, out poolPrice);
            var price = inPool ? poolPrice : gddPrice;

            var events = new List<EventName>();
            var comboIds = new List<string>();

            if (item.PassiveHooks != null)
            {
                foreach (var hook in item.PassiveHooks)
                {
                    if (hook == null) continue;

                    if (hook.Kind == PassiveHookKind.EventBus)
                    {
                        if (!events.Contains(hook.TriggerEvent)) events.Add(hook.TriggerEvent);
                        continue;
                    }

                    // ComboPlayed.
                    var filter = hook.ComboFilter;
                    if (filter == null || filter.Mode != ComboFilterMode.ComboIds)
                    {
                        if (!comboIds.Contains(AnyComboSentinel)) comboIds.Add(AnyComboSentinel);
                        continue;
                    }

                    if (filter.ComboIds == null) continue;
                    foreach (var id in filter.ComboIds)
                        if (!string.IsNullOrEmpty(id) && !comboIds.Contains(id)) comboIds.Add(id);
                }
            }

            return new ItemMetrics(
                item, item.Rarity, RarityPalette.DisplayName(item.Rarity), item.Type, item.FamilyId,
                price, !inPool, gddPrice, events, comboIds);
        }
    }
}
