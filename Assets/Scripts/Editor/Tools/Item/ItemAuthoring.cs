using System;
using System.Collections.Generic;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.EditorTools.Localization;
using Rollgeon.Items;
using Rollgeon.Localization;
using Rollgeon.Shop;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Single entry point that authors <see cref="ItemSO"/> assets from a data specification (spec
    /// §6.7 "un unico punto de entrada en C#"). Service only — no UI. Two clients are expected: the
    /// Fase 3 creation wizard and the Fase 4 MCP skill; both build a spec and call this.
    /// </summary>
    public static class ItemAuthoring
    {
        /// <summary>Default folder for new item assets — same one <c>ItemEditorWindow</c> uses.</summary>
        public const string DefaultFolder = "Assets/Rollgeon/Items";

        // ---- creation ---------------------------------------------------------------------------

        /// <summary>
        /// Creates one item (spec §6.2, operation 1). Validates everything up front — id derivation,
        /// global uniqueness, catalog/pool availability — before writing a single asset: a failed
        /// validation never leaves a half-created item. The four writes (asset, catalog, ES/EN
        /// localization, shop price) land in one undo step.
        /// </summary>
        public static ItemCreationResult CreateItem(ItemCreationSpec spec)
        {
            var errors = new List<string>();
            var catalog = LoadCatalog();
            if (catalog == null) errors.Add("ItemCatalogSO asset not found in the project.");
            var pool = ItemShopPriceBridge.LoadDefaultPool();
            if (pool == null) errors.Add($"ShopPoolSO not found at '{ItemShopPriceBridge.DefaultShopPoolPath}'.");

            var claimed = new HashSet<string>();
            bool ok = TryPrepare(
                spec.DisplayName, spec.Description, spec.Icon, spec.Rarity, spec.Type,
                spec.FamilyId, spec.VariantIndex ?? 0, spec.BasePrice, spec.TargetFolder,
                claimed, errors, out var prepared);

            if (!ok || errors.Count > 0) return new ItemCreationResult(errors);

            using (PolymorphicAuthoringContext.UndoGroup("Create Item"))
            {
                var item = WriteItem(prepared, catalog, pool);
                return new ItemCreationResult(item, item.ItemId, AssetDatabase.GetAssetPath(item));
            }
        }

        /// <summary>
        /// Creates a whole family of variants (spec §6.2, operation 2; §3 rule 4 "Agregar variante a
        /// la familia"). All variants share <see cref="ItemFamilyCreationSpec.FamilyId"/> and land in
        /// one undo step. Every variant is validated — including collisions between variants in the
        /// same request — before any asset is written.
        /// </summary>
        public static ItemFamilyCreationResult CreateFamily(ItemFamilyCreationSpec spec)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(spec.FamilyId))
                errors.Add("FamilyId is required for a family creation.");
            if (spec.Variants == null || spec.Variants.Count == 0)
                errors.Add("At least one variant is required.");

            var catalog = LoadCatalog();
            if (catalog == null) errors.Add("ItemCatalogSO asset not found in the project.");
            var pool = ItemShopPriceBridge.LoadDefaultPool();
            if (pool == null) errors.Add($"ShopPoolSO not found at '{ItemShopPriceBridge.DefaultShopPoolPath}'.");

            if (errors.Count > 0) return new ItemFamilyCreationResult(errors);

            var claimed = new HashSet<string>();
            var prepared = new List<PreparedItem>(spec.Variants.Count);
            for (int i = 0; i < spec.Variants.Count; i++)
            {
                var v = spec.Variants[i];
                var description = string.IsNullOrEmpty(v.Description) ? spec.DefaultDescription : v.Description;
                var icon = v.Icon != null ? v.Icon : spec.DefaultIcon;
                var variantIndex = v.VariantIndex ?? i;

                bool ok = TryPrepare(
                    v.DisplayName, description, icon, v.Rarity, spec.Type,
                    spec.FamilyId, variantIndex, v.BasePrice, spec.TargetFolder,
                    claimed, errors, out var p);
                if (ok) prepared.Add(p);
            }

            if (errors.Count > 0) return new ItemFamilyCreationResult(errors);

            using (PolymorphicAuthoringContext.UndoGroup("Create Item Family"))
            {
                var items = new List<ItemSO>(prepared.Count);
                foreach (var p in prepared)
                    items.Add(WriteItem(p, catalog, pool));
                return new ItemFamilyCreationResult(items);
            }
        }

        // ---- rename -------------------------------------------------------------------------------

        /// <summary>
        /// Renames <paramref name="item"/>'s id (spec §3 rule 3). Explicit action, separate from
        /// editing Display Name: also renames the two localization keys
        /// (<c>&lt;oldId&gt;.name</c>/<c>.desc</c> → <c>&lt;newId&gt;.name</c>/<c>.desc</c>). The
        /// result's <see cref="ItemRenameResult.BreaksSaveCompatibility"/> is always true on success —
        /// ItemId is a save key (TECHNICAL.md §18) and this call never migrates saves; the caller (the
        /// Fase 3 UI) is responsible for warning the author before committing to it.
        /// </summary>
        public static ItemRenameResult RenameItemId(ItemSO item, string newItemId)
        {
            if (item == null) return new ItemRenameResult("Item is null.");
            if (string.IsNullOrWhiteSpace(newItemId)) return new ItemRenameResult("New id is required.");
            if (newItemId == item.ItemId) return new ItemRenameResult($"'{newItemId}' is already this item's id.");

            if (!IsIdAvailable(newItemId, out var owner))
            {
                var ownerPath = owner != null ? AssetDatabase.GetAssetPath(owner) : "<unknown>";
                return new ItemRenameResult($"Id '{newItemId}' is already used by '{ownerPath}'.");
            }

            var oldId = item.ItemId;

            using (PolymorphicAuthoringContext.UndoGroup("Rename Item Id"))
            {
                Undo.RecordObject(item, "Rename Item Id");
                item.ItemId = newItemId;
                EditorUtility.SetDirty(item);

                MoveLocalizationKeys(oldId, newItemId);
            }

            return new ItemRenameResult(oldId, newItemId);
        }

        // ---- uniqueness ---------------------------------------------------------------------------

        /// <summary>
        /// True if no <see cref="ItemSO"/> in the project already uses <paramref name="candidateId"/>.
        /// Global check (spec §3 rule 1) — scans every <c>ItemSO</c> asset via
        /// <c>AssetDatabase.FindAssets</c>, not just the ones registered in <c>ItemCatalog</c>, since
        /// items don't all live in one folder and a stray/unregistered asset still owns its id.
        /// Public: the Fase 3 list uses this to flag duplicate ids on the fly.
        /// </summary>
        public static bool IsIdAvailable(string candidateId, out ItemSO owner)
        {
            owner = null;
            if (string.IsNullOrEmpty(candidateId)) return false;

            foreach (var so in EnumerateAllItemAssets())
            {
                if (so.ItemId != candidateId) continue;
                owner = so;
                return false;
            }
            return true;
        }

        static IEnumerable<ItemSO> EnumerateAllItemAssets()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(ItemSO));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
                if (so != null) yield return so;
            }
        }

        // ---- validation + write ---------------------------------------------------------------------

        /// <summary>Fully-validated, ready-to-write item. Only <see cref="TryPrepare"/> constructs one.</summary>
        readonly struct PreparedItem
        {
            public readonly string ItemId;
            public readonly string DisplayName;
            public readonly string Description;
            public readonly Sprite Icon;
            public readonly ItemRarity Rarity;
            public readonly ItemType Type;
            public readonly string FamilyId;
            public readonly int VariantIndex;
            public readonly int BasePrice;
            public readonly string TargetFolder;

            public PreparedItem(
                string itemId, string displayName, string description, Sprite icon, ItemRarity rarity,
                ItemType type, string familyId, int variantIndex, int basePrice, string targetFolder)
            {
                ItemId = itemId;
                DisplayName = displayName;
                Description = description;
                Icon = icon;
                Rarity = rarity;
                Type = type;
                FamilyId = familyId;
                VariantIndex = variantIndex;
                BasePrice = basePrice;
                TargetFolder = targetFolder;
            }
        }

        /// <summary>
        /// Validates one candidate item and, if valid, appends its id to <paramref name="claimedIds"/>
        /// — so a family batch catches two variants deriving the same id before either is written, not
        /// just collisions against disk. Errors accumulate in <paramref name="errors"/>.
        /// </summary>
        static bool TryPrepare(
            string displayName, string description, Sprite icon, ItemRarity rarity, ItemType type,
            string familyId, int variantIndex, int? basePrice, string targetFolder,
            HashSet<string> claimedIds, List<string> errors, out PreparedItem prepared)
        {
            prepared = default;
            bool valid = true;

            if (string.IsNullOrWhiteSpace(displayName))
            {
                errors.Add("DisplayName is required.");
                valid = false;
            }

            if (!Enum.IsDefined(typeof(ItemRarity), rarity))
            {
                errors.Add($"'{rarity}' is not a valid ItemRarity.");
                valid = false;
            }

            if (!Enum.IsDefined(typeof(ItemType), type))
            {
                errors.Add($"'{type}' is not a valid ItemType.");
                valid = false;
            }

            var folder = string.IsNullOrEmpty(targetFolder) ? DefaultFolder : targetFolder;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                errors.Add($"Target folder '{folder}' does not exist.");
                valid = false;
            }

            if (!valid) return false; // don't bother deriving an id off a bad display name

            var itemId = ItemIdSlug.FromDisplayName(displayName);
            if (string.IsNullOrEmpty(itemId))
            {
                errors.Add($"DisplayName '{displayName}' does not derive a usable id (only separators/symbols).");
                return false;
            }

            if (claimedIds.Contains(itemId))
            {
                errors.Add($"Id '{itemId}' collides with another variant in this same request.");
                return false;
            }

            if (!IsIdAvailable(itemId, out var owner))
            {
                var ownerPath = owner != null ? AssetDatabase.GetAssetPath(owner) : "<unknown>";
                errors.Add($"Id '{itemId}' is already used by '{ownerPath}'.");
                return false;
            }

            claimedIds.Add(itemId);

            var resolvedPrice = basePrice ?? RarityPricing.BasePriceFor(rarity);

            prepared = new PreparedItem(
                itemId, displayName, description ?? string.Empty, icon, rarity, type,
                familyId ?? string.Empty, variantIndex, resolvedPrice, folder);
            return true;
        }

        /// <summary>
        /// The four writes (spec §6.2/§7.2): asset + catalog + ES/EN localization + shop price. Caller
        /// wraps this in a <see cref="PolymorphicAuthoringContext.UndoGroup"/>; every write here does
        /// its own Undo/SetDirty (rule 3), so nesting several calls inside one group composes fine.
        /// </summary>
        static ItemSO WriteItem(PreparedItem p, ItemCatalogSO catalog, ShopPoolSO pool)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = p.ItemId;
            item.DisplayName = p.DisplayName;
            item.Description = p.Description;
            item.Icon = p.Icon;
            item.Rarity = p.Rarity;
            item.Type = p.Type;
            item.FamilyId = p.FamilyId;
            item.VariantIndex = p.VariantIndex;

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{p.TargetFolder}/Item_{AssetNaming.PascalCaseId(p.ItemId)}.asset");
            AssetDatabase.CreateAsset(item, assetPath);
            Undo.RegisterCreatedObjectUndo(item, "Create Item");

            catalog.EditorAdd(item);

            // No translation service here — both locales seed with the authored text; the Fase 3
            // language dropdown (spec §4) is where EN gets its real translation later.
            UpsertLocalizationEntryWithUndo(p.ItemId + LocalizedContent.NameSuffix, p.DisplayName, p.DisplayName);
            UpsertLocalizationEntryWithUndo(p.ItemId + LocalizedContent.DescSuffix, p.Description, p.Description);

            ItemShopPriceBridge.AddToPool(pool, item, p.BasePrice);

            return item;
        }

        static ItemCatalogSO LoadCatalog()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(ItemCatalogSO));
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ---- localization plumbing -----------------------------------------------------------------

        /// <summary>
        /// Wraps <c>LocalizationSetupTools.UpsertEntry</c> with the <c>Undo.RecordObject</c> it skips
        /// (spec §4/§7 rule 3) — on the shared table data and on each locale's table — before mutating.
        /// </summary>
        static void UpsertLocalizationEntryWithUndo(string key, string es, string en)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            if (collection == null)
                throw new Exception($"[ItemAuthoring] String Table Collection '{LocalizedContent.ContentTable}' not found.");

            Undo.RecordObject(collection.SharedData, "Edit Item Localization");
            if (collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EsCode)) is StringTable esTable)
                Undo.RecordObject(esTable, "Edit Item Localization");
            if (collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EnCode)) is StringTable enTable)
                Undo.RecordObject(enTable, "Edit Item Localization");

            LocalizationSetupTools.UpsertEntry(LocalizedContent.ContentTable, key, es, en);
        }

        /// <summary>Moves both localization keys (name+desc) from <paramref name="oldId"/> to <paramref name="newId"/>.</summary>
        static void MoveLocalizationKeys(string oldId, string newId)
        {
            MoveLocalizationKey(oldId + LocalizedContent.NameSuffix, newId + LocalizedContent.NameSuffix);
            MoveLocalizationKey(oldId + LocalizedContent.DescSuffix, newId + LocalizedContent.DescSuffix);
        }

        /// <summary>
        /// Reads <paramref name="oldKey"/>'s ES/EN values, writes them under <paramref name="newKey"/>,
        /// then removes the old shared-table key. No-op if the old key never had localized text.
        /// </summary>
        static void MoveLocalizationKey(string oldKey, string newKey)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            if (collection == null) return; // missing table is a setup problem, not this call's job

            var esTable = collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EsCode)) as StringTable;
            var enTable = collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EnCode)) as StringTable;

            var esValue = esTable != null ? esTable.GetEntry(oldKey)?.Value : null;
            var enValue = enTable != null ? enTable.GetEntry(oldKey)?.Value : null;
            if (esValue == null && enValue == null) return;

            UpsertLocalizationEntryWithUndo(newKey, esValue, enValue);

            Undo.RecordObject(collection.SharedData, "Rename Item Id");
            collection.SharedData.RemoveKey(oldKey);
            EditorUtility.SetDirty(collection.SharedData);
        }
    }
}
