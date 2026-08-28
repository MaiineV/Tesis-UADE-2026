using System.Collections.Generic;
using Rollgeon.Items;
using Rollgeon.Shop;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Editor-only bridge between an <see cref="ItemSO"/> and the price that actually sells it: the
    /// <see cref="WeightedShopItem.BasePrice"/> of its entry inside a <see cref="ShopPoolSO"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The item asset has no price field of its own (docs/tools/item-editor-spec.md §2/§3): it lives
    /// on the pool, in a separate asset the author has to remember to open. This bridge exists so the
    /// Item Editor can read and write it without that trip.
    /// </para>
    /// <para>
    /// <b>The price itself is a caller-supplied parameter — this bridge never computes one.</b> The
    /// rarity → base price table (spec §2) is authored elsewhere; coupling it here would fork it
    /// against whoever owns that table.
    /// </para>
    /// <para>
    /// <b>Undo/Dirty (spec §7):</b> every write here does <c>Undo.RecordObject(pool, ...)</c> before
    /// mutating and <c>EditorUtility.SetDirty(pool)</c> after. Nested calls compose fine inside an
    /// outer <c>PolymorphicAuthoringContext.UndoGroup</c> — Unity's undo stack just gets one more step
    /// to collapse — so callers batching a multi-asset operation (e.g. creating an item family) do not
    /// need to do anything special around these calls.
    /// </para>
    /// </remarks>
    public static class ItemShopPriceBridge
    {
        /// <summary>Same path <c>ShopSetupTools</c> uses for the live shop pool.</summary>
        public const string DefaultShopPoolPath = "Assets/Rollgeon/Rooms/Shop/ShopPool.asset";

        /// <summary>Loads the project's canonical shop pool. Null if the asset is missing.</summary>
        public static ShopPoolSO LoadDefaultPool() =>
            AssetDatabase.LoadAssetAtPath<ShopPoolSO>(DefaultShopPoolPath);

        /// <summary>
        /// True if <paramref name="item"/> has an entry in <paramref name="pool"/> — either the
        /// guaranteed slot or the weighted <c>Items</c> list.
        /// </summary>
        public static bool IsInPool(ShopPoolSO pool, ItemSO item) =>
            TryFindEntry(pool, item, out _, out _);

        /// <summary>
        /// Reads the current <c>BasePrice</c> of <paramref name="item"/>'s entry. Returns false —
        /// and leaves <paramref name="basePrice"/> at 0 — if the item is not wired into the pool.
        /// </summary>
        public static bool TryGetPrice(ShopPoolSO pool, ItemSO item, out int basePrice)
        {
            basePrice = 0;
            if (!TryFindEntry(pool, item, out var isGuaranteed, out var itemsIndex)) return false;
            basePrice = isGuaranteed ? pool.Guaranteed.BasePrice : pool.Items[itemsIndex].BasePrice;
            return true;
        }

        /// <summary>
        /// Writes <c>BasePrice</c> on <paramref name="item"/>'s existing entry. Returns false without
        /// touching the pool if the item is not wired in yet — use <see cref="AddToPool"/> for that.
        /// </summary>
        public static bool SetPrice(ShopPoolSO pool, ItemSO item, int basePrice)
        {
            if (!TryFindEntry(pool, item, out var isGuaranteed, out var itemsIndex)) return false;

            Undo.RecordObject(pool, "Set Item Shop Price");
            if (isGuaranteed)
            {
                var guaranteed = pool.Guaranteed;
                guaranteed.BasePrice = basePrice;
                pool.Guaranteed = guaranteed;
            }
            else
            {
                var entry = pool.Items[itemsIndex];
                entry.BasePrice = basePrice;
                pool.Items[itemsIndex] = entry;
            }
            EditorUtility.SetDirty(pool);
            return true;
        }

        /// <summary>
        /// Adds <paramref name="item"/> as a new <c>Items</c> entry with <paramref name="basePrice"/>,
        /// if it is not already in the pool. Idempotent: returns false and does nothing if the item is
        /// already wired in (as either the guaranteed slot or an <c>Items</c> entry).
        /// </summary>
        /// <remarks>
        /// Never touches <c>Guaranteed</c> — that slot is reserved for the healing potion and owned by
        /// <c>ShopSetupTools</c>. <paramref name="weight"/> and <paramref name="minFloorDepth"/> default
        /// to "always eligible, normal weight" so a bare call still produces a working entry; callers
        /// that want the GDD's rarity-derived weight/depth (spec §2 table) pass them explicitly.
        /// </remarks>
        public static bool AddToPool(
            ShopPoolSO pool,
            ItemSO item,
            int basePrice,
            float weight = 1f,
            int minFloorDepth = 0)
        {
            if (pool == null || item == null) return false;
            if (IsInPool(pool, item)) return false;

            Undo.RecordObject(pool, "Add Item To Shop Pool");
            pool.Items ??= new List<WeightedShopItem>();
            pool.Items.Add(new WeightedShopItem
            {
                Item = item,
                Weight = weight,
                BasePrice = basePrice,
                MinFloorDepth = minFloorDepth,
            });
            EditorUtility.SetDirty(pool);
            return true;
        }

        /// <summary>
        /// Locates <paramref name="item"/>'s entry. <paramref name="isGuaranteed"/> selects
        /// <see cref="ShopPoolSO.Guaranteed"/>; otherwise <paramref name="itemsIndex"/> indexes
        /// <see cref="ShopPoolSO.Items"/>. Kept private — callers only need the outcome, not where it
        /// lives, and a raw index would leak the two-slot-kinds shape of <see cref="ShopPoolSO"/> into
        /// every call site.
        /// </summary>
        private static bool TryFindEntry(ShopPoolSO pool, ItemSO item, out bool isGuaranteed, out int itemsIndex)
        {
            isGuaranteed = false;
            itemsIndex = -1;
            if (pool == null || item == null) return false;

            if (ReferenceEquals(pool.Guaranteed.Item, item))
            {
                isGuaranteed = true;
                return true;
            }

            if (pool.Items == null) return false;
            for (int i = 0; i < pool.Items.Count; i++)
            {
                if (!ReferenceEquals(pool.Items[i].Item, item)) continue;
                itemsIndex = i;
                return true;
            }
            return false;
        }
    }
}
