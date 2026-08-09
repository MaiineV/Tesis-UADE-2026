using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Resuelve el sprite de una fuente del journal (<c>ScratchContribution.SourceAsset</c>).
    /// Hoy solo <c>ItemSO</c> tiene icono autorable (y los 22 items lo tienen vacío);
    /// <c>EnchantmentSO</c> no tiene sprite — la vista aplica su fallback en ambos casos.
    /// </summary>
    public static class BreakdownIconResolver
    {
        public static Sprite Resolve(Object sourceAsset)
            => sourceAsset is Rollgeon.Items.ItemSO item ? item.Icon : null;
    }
}
