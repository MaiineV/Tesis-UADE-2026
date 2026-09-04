using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>Familia de una fuente de proc — elige el sonido del popup.</summary>
    public enum BreakdownProcFamily
    {
        Item,
        Enchantment,
        Passive,
    }

    /// <summary>
    /// Resuelve el sprite de una fuente del journal (<c>ScratchContribution.SourceAsset</c>):
    /// <c>ItemSO.Icon</c> o <c>UpgradeSO.Icon</c> (encantamientos). Null si el asset no tiene
    /// icono autorado — la vista aplica su fallback.
    /// </summary>
    public static class BreakdownIconResolver
    {
        public static Sprite Resolve(Object sourceAsset)
        {
            switch (sourceAsset)
            {
                case Rollgeon.Items.ItemSO item: return item.Icon;
                case Rollgeon.Upgrades.UpgradeSO upgrade: return upgrade.Icon;
                default: return null;
            }
        }

        /// <summary>Familia por tipo del asset (mismo criterio de resolución que el sprite).</summary>
        public static BreakdownProcFamily ResolveFamily(Object sourceAsset)
        {
            if (sourceAsset is Rollgeon.Items.ItemSO) return BreakdownProcFamily.Item;
            if (sourceAsset is Rollgeon.Upgrades.Dice.EnchantmentSO) return BreakdownProcFamily.Enchantment;
            return BreakdownProcFamily.Passive;
        }

        /// <summary>Nombre legible (localizado si hay tabla) de la fuente del aporte.</summary>
        public static string ResolveDisplayName(Object sourceAsset)
        {
            switch (sourceAsset)
            {
                case Rollgeon.Items.ItemSO item:
                    return Rollgeon.Localization.LocalizedContent.Name(item.ItemId, item.DisplayName);
                case Rollgeon.Upgrades.UpgradeSO upgrade:
                    return Rollgeon.Localization.LocalizedContent.Name(upgrade.UpgradeId, upgrade.DisplayName);
                default:
                    return sourceAsset != null ? sourceAsset.name : string.Empty;
            }
        }
    }
}
