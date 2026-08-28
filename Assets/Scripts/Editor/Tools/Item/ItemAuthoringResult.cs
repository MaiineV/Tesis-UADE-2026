using System.Collections.Generic;
using Rollgeon.Items;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>Outcome of <see cref="ItemAuthoring.CreateItem"/>.</summary>
    public readonly struct ItemCreationResult
    {
        public bool Success { get; }
        public IReadOnlyList<string> Errors { get; }
        public ItemSO Item { get; }
        public string ItemId { get; }
        public string AssetPath { get; }

        internal ItemCreationResult(ItemSO item, string itemId, string assetPath)
        {
            Success = true;
            Errors = System.Array.Empty<string>();
            Item = item;
            ItemId = itemId;
            AssetPath = assetPath;
        }

        internal ItemCreationResult(IReadOnlyList<string> errors)
        {
            Success = false;
            Errors = errors;
            Item = null;
            ItemId = null;
            AssetPath = null;
        }
    }

    /// <summary>Outcome of <see cref="ItemAuthoring.CreateFamily"/>.</summary>
    public readonly struct ItemFamilyCreationResult
    {
        public bool Success { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<ItemSO> Items { get; }

        internal ItemFamilyCreationResult(IReadOnlyList<ItemSO> items)
        {
            Success = true;
            Errors = System.Array.Empty<string>();
            Items = items;
        }

        internal ItemFamilyCreationResult(IReadOnlyList<string> errors)
        {
            Success = false;
            Errors = errors;
            Items = System.Array.Empty<ItemSO>();
        }
    }

    /// <summary>Outcome of <see cref="ItemAuthoring.RenameItemId"/>.</summary>
    public readonly struct ItemRenameResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public string OldId { get; }
        public string NewId { get; }

        /// <summary>
        /// Always true on success: ItemId is a save key (TECHNICAL.md §18 — InventorySlotSnapshot,
        /// PassiveItemIds, RoomObjectState.ReservedItemId). Every successful rename breaks saves that
        /// reference the old id; the service never silently migrates them. Callers surface this to the
        /// author before committing to the rename.
        /// </summary>
        public bool BreaksSaveCompatibility => Success;

        internal ItemRenameResult(string oldId, string newId)
        {
            Success = true;
            ErrorMessage = null;
            OldId = oldId;
            NewId = newId;
        }

        internal ItemRenameResult(string errorMessage)
        {
            Success = false;
            ErrorMessage = errorMessage;
            OldId = null;
            NewId = null;
        }
    }

    /// <summary>
    /// Resultado de <c>ItemAuthoring.DeleteItem</c>.
    /// </summary>
    /// <remarks>
    /// Informa cada limpieza por separado en vez de un solo booleano: un ítem puede estar fuera del
    /// catálogo o fuera del pool desde antes, y "no lo saqué del pool porque no estaba" no es lo
    /// mismo que "fallé al sacarlo". Quien llama lo reporta tal cual.
    /// </remarks>
    public readonly struct ItemDeletionResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public string ItemId { get; }
        public string AssetPath { get; }
        public bool RemovedFromCatalog { get; }
        public bool RemovedFromPool { get; }
        public int RemovedLocalizationKeys { get; }

        ItemDeletionResult(
            bool success, string errorMessage, string itemId, string assetPath,
            bool removedFromCatalog, bool removedFromPool, int removedLocalizationKeys)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ItemId = itemId;
            AssetPath = assetPath;
            RemovedFromCatalog = removedFromCatalog;
            RemovedFromPool = removedFromPool;
            RemovedLocalizationKeys = removedLocalizationKeys;
        }

        internal static ItemDeletionResult Ok(
            string itemId, string assetPath, bool removedFromCatalog, bool removedFromPool, int removedKeys) =>
            new ItemDeletionResult(true, null, itemId, assetPath, removedFromCatalog, removedFromPool, removedKeys);

        internal static ItemDeletionResult Failed(string errorMessage) =>
            new ItemDeletionResult(false, errorMessage, null, null, false, false, 0);
    }
}
