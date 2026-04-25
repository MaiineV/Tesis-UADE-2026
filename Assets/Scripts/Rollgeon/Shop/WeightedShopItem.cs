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
        public ShopItemDef Item;

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
    }
}
