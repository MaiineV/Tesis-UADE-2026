using System.Collections.Generic;
using Rollgeon.Shop;
using Rollgeon.Upgrades.Combos;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Shop
{
    /// <summary>
    /// Cablea el <c>ShopPool.asset</c> al modo dinámico: poción como entry
    /// garantizada (slot 0), <c>ComboPassivePool</c> como fuente rotativa de los
    /// demás slots, y vacía las entries manuales (las pasivas ya no se listan a
    /// mano — agregar una nueva al ComboPassivePool la hace aparecer sola).
    /// Idempotente.
    /// </summary>
    public static class ShopSetupTools
    {
        private const string ShopPoolPath = "Assets/Rollgeon/Rooms/Shop/ShopPool.asset";
        private const string PotionDefPath = "Assets/Rollgeon/Rooms/Shop/Items/ShopItem_HealingPotion.asset";
        private const string PassivePoolPath = "Assets/Rollgeon/Upgrades/Combos/ComboPassivePool.asset";
        private const int PotionBasePrice = 8; // precio que tenía la entry manual

        [MenuItem("Rollgeon/Shop/Wire Dynamic Pool")]
        public static void WireDynamicPool()
        {
            var pool = AssetDatabase.LoadAssetAtPath<ShopPoolSO>(ShopPoolPath);
            var potion = AssetDatabase.LoadAssetAtPath<ShopItemDef>(PotionDefPath);
            var passives = AssetDatabase.LoadAssetAtPath<ComboPassivePoolSO>(PassivePoolPath);

            if (pool == null || potion == null || passives == null)
            {
                Debug.LogError("[ShopSetup] Falta ShopPool, ShopItem_HealingPotion o ComboPassivePool — abortando.");
                return;
            }

            pool.Guaranteed = new WeightedShopItem
            {
                Item = potion,
                Weight = 1f,
                BasePrice = PotionBasePrice,
                MinFloorDepth = 0,
            };
            pool.PassivePool = passives;
            pool.Items = new List<WeightedShopItem>();

            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShopSetup] ShopPool dinámico: poción garantizada + ComboPassivePool como fuente rotativa.");
        }
    }
}
