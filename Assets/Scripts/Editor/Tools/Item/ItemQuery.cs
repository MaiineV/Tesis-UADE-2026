using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Items;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Read-only query layer over the item catalog on disk. item-editor-spec.md draws a hard line
    /// between authoring (<c>ItemAuthoring*</c>) and reading (this file): nothing here calls
    /// <c>Undo.RecordObject</c> or <c>EditorUtility.SetDirty</c>, and nothing here should ever need
    /// to. Feeds the list filters (spec §6.1) and the metrics tab (spec §6.6).
    /// </summary>
    /// <remarks>
    /// Split into partials by concern, same convention as <c>ItemEditorWindow</c>'s per-tab files:
    /// this file (instances, raw items, families), <c>ItemQuery.Effects.cs</c> (effect-type
    /// walking), <c>ItemQuery.Metrics.cs</c> (per-item/aggregate data), <c>ItemQuery.Health.cs</c>
    /// (catalog findings).
    /// <para>
    /// <b>Disk-scan vs. pure overloads.</b> Every query that can operate on an arbitrary item list
    /// exposes two forms: a zero-arg one that scans the project via <c>AssetDatabase</c>, and an
    /// <c>IEnumerable&lt;ItemSO&gt;</c> overload that does the actual (pure, disk-free) logic. The
    /// disk-free overload is what makes this testable with <c>ScriptableObject.CreateInstance</c>
    /// instead of committed test fixture assets, and it is also what Fase 3 should call if it ever
    /// wants to re-query a cached/filtered list instead of re-scanning the project on every draw.
    /// </para>
    /// </remarks>
    public static partial class ItemQuery
    {
        /// <summary>
        /// One entry the catalog exposes to a player at runtime. Today this is a 1:1 projection of
        /// an <see cref="ItemSO"/> asset — <see cref="InstanceId"/> is just <c>Asset.ItemId</c>.
        /// </summary>
        /// <remarks>
        /// This indirection is the point of the type, not accidental complexity. Once the GDD's
        /// <c>&lt;combo&gt;</c> templates land (item-editor-spec.md §8.F), one template asset will
        /// stand in for 5 rarities × 5 combos = 25 instances, each with its own id, rarity and
        /// price — none of which the asset itself will carry directly anymore at that point. Every
        /// consumer that counts, filters or aggregates "items" (list filters §6.1, metrics §6.6)
        /// needs to operate on <see cref="ItemCatalogInstance"/>, not on <c>ItemSO</c>, so that
        /// migration only changes how <see cref="GetAllInstances()"/> is built — nothing downstream
        /// has to be rewritten. It costs one extra type today; the alternative costs a rewrite of
        /// two Fase 3 surfaces later.
        /// </remarks>
        public readonly struct ItemCatalogInstance
        {
            public ItemSO Asset { get; }
            public string InstanceId { get; }
            public string DisplayName { get; }
            public ItemRarity Rarity { get; }
            public string FamilyId { get; }
            public int VariantIndex { get; }

            public ItemCatalogInstance(ItemSO asset)
            {
                Asset = asset;
                InstanceId = asset != null ? asset.ItemId : null;
                DisplayName = asset != null ? asset.DisplayName : null;
                Rarity = asset != null ? asset.Rarity : default;
                FamilyId = asset != null ? asset.FamilyId : null;
                VariantIndex = asset != null ? asset.VariantIndex : 0;
            }
        }

        /// <summary>
        /// All <see cref="ItemSO"/> assets in the project, project-wide — not just
        /// <c>ItemEditorWindow.DefaultFolder</c> (spec §0: two of the 24 live elsewhere). Sorted by
        /// asset path so callers get a deterministic order without needing to sort themselves.
        /// </summary>
        public static IReadOnlyList<ItemSO> GetAllItems()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(ItemSO));
            var items = new List<ItemSO>(guids.Length);
            foreach (var guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) items.Add(asset);
            }
            items.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
            return items;
        }

        /// <summary>
        /// Catalog instances for every project item, one per asset today (see
        /// <see cref="ItemCatalogInstance"/>). This is the entry point counting/aggregating
        /// consumers should call instead of <see cref="GetAllItems()"/>.
        /// </summary>
        public static IReadOnlyList<ItemCatalogInstance> GetAllInstances() => GetAllInstances(GetAllItems());

        /// <summary>Pure form of <see cref="GetAllInstances()"/> — projects an arbitrary item list instead of scanning disk.</summary>
        public static IReadOnlyList<ItemCatalogInstance> GetAllInstances(IEnumerable<ItemSO> items) =>
            (items ?? Enumerable.Empty<ItemSO>())
                .Where(i => i != null)
                .Select(i => new ItemCatalogInstance(i))
                .ToList();

        /// <summary>One group of items sharing a non-empty <see cref="ItemSO.FamilyId"/>, ordered by <see cref="ItemSO.VariantIndex"/>.</summary>
        public sealed class ItemFamily
        {
            public string FamilyId { get; }
            public IReadOnlyList<ItemSO> Variants { get; }

            public ItemFamily(string familyId, IReadOnlyList<ItemSO> variants)
            {
                FamilyId = familyId;
                Variants = variants;
            }
        }

        /// <summary>Project items grouped by <see cref="ItemSO.FamilyId"/>, each ordered by <see cref="ItemSO.VariantIndex"/>. Loose items (empty FamilyId) are excluded — see <see cref="GetLooseItems()"/>.</summary>
        public static IReadOnlyList<ItemFamily> GetFamilies() => GetFamilies(GetAllItems());

        /// <summary>Pure form of <see cref="GetFamilies()"/> — groups an arbitrary item list instead of scanning disk.</summary>
        public static IReadOnlyList<ItemFamily> GetFamilies(IEnumerable<ItemSO> items) =>
            (items ?? Enumerable.Empty<ItemSO>())
                .Where(i => i != null && !string.IsNullOrEmpty(i.FamilyId))
                .GroupBy(i => i.FamilyId, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new ItemFamily(g.Key, g.OrderBy(i => i.VariantIndex).ToList()))
                .ToList();

        /// <summary>Project items with an empty <see cref="ItemSO.FamilyId"/> — standalone items, not part of a family.</summary>
        public static IReadOnlyList<ItemSO> GetLooseItems() => GetLooseItems(GetAllItems());

        /// <summary>Pure form of <see cref="GetLooseItems()"/>.</summary>
        public static IReadOnlyList<ItemSO> GetLooseItems(IEnumerable<ItemSO> items) =>
            (items ?? Enumerable.Empty<ItemSO>()).Where(i => i != null && string.IsNullOrEmpty(i.FamilyId)).ToList();

        /// <summary>Display label for messages/findings — <c>DisplayName</c> falling back to the asset's file name, same convention as <c>ItemEditorWindow.LabelOf</c>.</summary>
        static string LabelOf(ItemSO item) =>
            item == null ? "(null)" : string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName;
    }
}
