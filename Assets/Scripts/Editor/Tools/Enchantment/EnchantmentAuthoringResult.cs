using System.Collections.Generic;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>Resultado de <see cref="EnchantmentAuthoring.CreateEnchantment"/>.</summary>
    public readonly struct EnchantmentCreationResult
    {
        public bool Success { get; }

        /// <summary>Todos los problemas de validación juntos — nada se escribió si hay alguno.</summary>
        public IReadOnlyList<string> Errors { get; }

        public EnchantmentSO Enchantment { get; }
        public string UpgradeId { get; }
        public string AssetPath { get; }

        internal EnchantmentCreationResult(EnchantmentSO enchantment, string upgradeId, string assetPath)
        {
            Success = true;
            Errors = System.Array.Empty<string>();
            Enchantment = enchantment;
            UpgradeId = upgradeId;
            AssetPath = assetPath;
        }

        internal EnchantmentCreationResult(IReadOnlyList<string> errors)
        {
            Success = false;
            Errors = errors;
            Enchantment = null;
            UpgradeId = null;
            AssetPath = null;
        }
    }

    /// <summary>Resultado de <see cref="EnchantmentAuthoring.RenameEnchantmentId"/>.</summary>
    public readonly struct EnchantmentRenameResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public string OldId { get; }
        public string NewId { get; }

        /// <summary>
        /// Siempre true al triunfar: el UpgradeId es clave de save (los slots del
        /// <c>RuntimeDiceBag</c> se restauran por id y descartan los desconocidos) y esta
        /// llamada no migra saves. El caller avisa antes de comprometerse.
        /// </summary>
        public bool BreaksSaveCompatibility => Success;

        internal EnchantmentRenameResult(string oldId, string newId)
        {
            Success = true;
            ErrorMessage = null;
            OldId = oldId;
            NewId = newId;
        }

        internal EnchantmentRenameResult(string errorMessage)
        {
            Success = false;
            ErrorMessage = errorMessage;
            OldId = null;
            NewId = null;
        }
    }

    /// <summary>
    /// Resultado de <see cref="EnchantmentAuthoring.DeleteEnchantment"/>. Reporta cada
    /// limpieza por separado a propósito: el borrado no es atómico ni undoable, y el
    /// rastro dice qué quedó hecho si algo falla a mitad de camino.
    /// </summary>
    public readonly struct EnchantmentDeletionResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public string UpgradeId { get; }
        public string AssetPath { get; }
        public bool RemovedFromCatalog { get; }
        public bool RemovedFromPool { get; }
        public int RemovedLocalizationKeys { get; }

        EnchantmentDeletionResult(
            bool success, string errorMessage, string upgradeId, string assetPath,
            bool removedFromCatalog, bool removedFromPool, int removedLocalizationKeys)
        {
            Success = success;
            ErrorMessage = errorMessage;
            UpgradeId = upgradeId;
            AssetPath = assetPath;
            RemovedFromCatalog = removedFromCatalog;
            RemovedFromPool = removedFromPool;
            RemovedLocalizationKeys = removedLocalizationKeys;
        }

        internal static EnchantmentDeletionResult Ok(
            string upgradeId, string assetPath, bool removedFromCatalog, bool removedFromPool, int removedKeys)
            => new(true, null, upgradeId, assetPath, removedFromCatalog, removedFromPool, removedKeys);

        internal static EnchantmentDeletionResult Failed(string errorMessage)
            => new(false, errorMessage, null, null, false, false, 0);
    }
}
