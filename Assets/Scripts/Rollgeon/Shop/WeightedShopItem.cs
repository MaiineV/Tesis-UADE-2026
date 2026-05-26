using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Entry pesada del <see cref="ShopPoolSO"/>. El rolling pondera
    /// <see cref="Weight"/> contra el total del pool. <see cref="MinFloorDepth"/>
    /// filtra ítems legendarios fuera de los primeros pisos. TECHNICAL.md §17.F.2.
    /// </summary>
    [Serializable]
    public struct WeightedShopItem
    {
        [Required]
        [InfoBox("Debe implementar IShopRewardEntry — ShopItemDef (consumibles) " +
                 "o ComboPassiveSO (pasivas de combo). Otros tipos se ignoran al rolear.")]
        [Tooltip("Asset del reward elegible. Polimórfico: ShopItemDef o ComboPassiveSO.")]
        [ValidateInput(nameof(ValidateImplementsRewardEntry),
                       "El asset asignado no implementa IShopRewardEntry — la entry se ignora al rolear.")]
        public ScriptableObject Item;

        [MinValue(0f)]
        [Tooltip("Peso relativo. 0 = deshabilitado. Los pesos son relativos al total del pool.")]
        public float Weight;

        [MinValue(1)]
        [Tooltip("Precio base antes del multiplicador + varianza del ShopConfigSO.")]
        public int BasePrice;

        [Title("Rarity filter")]
        [InfoBox("Si > 0, sólo rolea en pisos con depth >= este valor. 0 = siempre eligible.")]
        [MinValue(0)]
        public int MinFloorDepth;

        /// <summary>
        /// Cast tipado del <see cref="Item"/> a <see cref="IShopRewardEntry"/>.
        /// Devuelve null si el asset no implementa la interface.
        /// </summary>
        public IShopRewardEntry GetEntry() => Item as IShopRewardEntry;

        private static bool ValidateImplementsRewardEntry(ScriptableObject so)
            => so == null || so is IShopRewardEntry;
    }
}
