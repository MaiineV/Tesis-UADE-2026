using System.Collections.Generic;
using System.Linq;
using Rollgeon.Items;
using Rollgeon.Shop;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Shop
{
    /// <summary>
    /// Cablea los pools de tienda al modelo item-first: la poción (un
    /// <see cref="ItemSO"/>) como entry garantizada (slot 0) y todos los items
    /// passive del <c>ItemCatalog</c> como fuente de los demás slots. Ya no hay
    /// <c>ComboPassivePool</c> — las pasivas SON items. Precio/peso/MinFloorDepth
    /// derivan de la rareza. Idempotente.
    /// </summary>
    public static class ShopSetupTools
    {
        private const string ShopPoolPath = "Assets/Rollgeon/Rooms/Shop/ShopPool.asset";
        private const string TutorialPoolPath = "Assets/Rollgeon/Tutorial/SP_Tutorial.asset";
        private const string ShopConfigPath = "Assets/Rollgeon/Rooms/Shop/ShopConfig.asset";
        private const string TutorialConfigPath = "Assets/Rollgeon/Tutorial/SC_Tutorial.asset";
        private const string PotionItemPath = "Assets/Rollgeon/Rooms/Shop/Items/Item_HealingPotion.asset";
        private const string CatalogPath = "Assets/Rollgeon/Items/ItemCatalog.asset";
        private const string TutorialItemPath = "Assets/Rollgeon/Items/Item_GuantesApostadorPar.asset";

        private const int PotionBasePrice = 8;
        private const int TutorialItemPrice = 20;

        [MenuItem("Rollgeon/Shop/Wire Item Pool")]
        public static void WireItemPool()
        {
            var pool = AssetDatabase.LoadAssetAtPath<ShopPoolSO>(ShopPoolPath);
            var potion = AssetDatabase.LoadAssetAtPath<ItemSO>(PotionItemPath);
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(CatalogPath);

            if (pool == null || potion == null || catalog == null)
            {
                Debug.LogError("[ShopSetup] Falta ShopPool, Item_HealingPotion o ItemCatalog — abortando.");
                return;
            }

            pool.Guaranteed = new WeightedShopItem
            {
                Item = potion,
                Weight = 1f,
                BasePrice = PotionBasePrice,
                MinFloorDepth = 0,
            };

            // Todos los items passive del catálogo (GetByType excluye la poción, que es Active).
            var items = catalog.GetByType(ItemType.Passive)
                .Where(i => i != null)
                .OrderBy(i => i.ItemId)
                .Select(ToWeightedEntry)
                .ToList();

            pool.Items = items;

            EditorUtility.SetDirty(pool);
            WireDefaultVisual(ShopConfigPath, potion);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShopSetup] ShopPool item-first: poción garantizada + {items.Count} items passive del catálogo.");
        }

        [MenuItem("Rollgeon/Shop/Wire Tutorial Pool")]
        public static void WireTutorialPool()
        {
            var pool = AssetDatabase.LoadAssetAtPath<ShopPoolSO>(TutorialPoolPath);
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(TutorialItemPath);
            var potion = AssetDatabase.LoadAssetAtPath<ItemSO>(PotionItemPath);

            if (pool == null || item == null)
            {
                Debug.LogError("[ShopSetup] Falta SP_Tutorial o Item_GuantesApostadorPar — abortando.");
                return;
            }

            // Tutorial: 1 sola entry (Guantes del apostador = Par +20 dmg, lo que enseña
            // el tutorial). Sin garantizado — el slot 0 degrada a este único item.
            pool.Guaranteed = default;
            pool.Items = new List<WeightedShopItem>
            {
                new WeightedShopItem { Item = item, Weight = 1f, BasePrice = TutorialItemPrice, MinFloorDepth = 0 },
            };

            EditorUtility.SetDirty(pool);
            WireDefaultVisual(TutorialConfigPath, potion);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShopSetup] SP_Tutorial item-first: Guantes del apostador como único item.");
        }

        // Cablea el visual fallback del config al WorldPrefab de la poción (misma cápsula
        // que ya se usaba), para que los items sin visual propio muestren algo tinteado.
        private static void WireDefaultVisual(string configPath, ItemSO potion)
        {
            var config = AssetDatabase.LoadAssetAtPath<ShopConfigSO>(configPath);
            if (config == null || potion == null || potion.WorldPrefab == null) return;
            config.DefaultItemVisualPrefab = potion.WorldPrefab;
            EditorUtility.SetDirty(config);
        }

        private static WeightedShopItem ToWeightedEntry(ItemSO item)
        {
            return new WeightedShopItem
            {
                Item = item,
                Weight = WeightForRarity(item.Rarity),
                BasePrice = PriceForRarity(item.Rarity),
                MinFloorDepth = MinDepthForRarity(item.Rarity),
            };
        }

        // Los items más raros son más caros, más escasos en el roll y aparecen en pisos más profundos.
        //
        // NOTA (item-editor-spec.md §2/§5): esta escala (10/20/35/55) es PREVIA al
        // GDD de Ítems Pasivos y NO son los precios del GDD (15/35/60/100/120,
        // ver Rollgeon.Items.RarityPricing). Migrar esta tool a RarityPricing es
        // decisión de Fase 2 (el asistente de creación, D2 del spec) — acá el
        // alcance es solo que God no caiga en el `_` de estos switch expressions
        // y herede el valor de Common en silencio. Se extrapola la MISMA escala
        // legacy en vez de mezclarla con la del GDD a mitad de tabla.
        private static float WeightForRarity(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 1.0f,
            ItemRarity.Uncommon => 0.7f,
            ItemRarity.Rare => 0.4f,
            ItemRarity.Legendary => 0.2f,
            ItemRarity.God => 0.1f,
            _ => throw new System.ArgumentOutOfRangeException(
                nameof(rarity), rarity, "ItemRarity sin peso definido en ShopSetupTools."),
        };

        private static int PriceForRarity(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 10,
            ItemRarity.Uncommon => 20,
            ItemRarity.Rare => 35,
            ItemRarity.Legendary => 55,
            ItemRarity.God => 80,
            _ => throw new System.ArgumentOutOfRangeException(
                nameof(rarity), rarity, "ItemRarity sin precio definido en ShopSetupTools."),
        };

        private static int MinDepthForRarity(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 0,
            ItemRarity.Uncommon => 0,
            ItemRarity.Rare => 1,
            ItemRarity.Legendary => 2,
            ItemRarity.God => 3,
            _ => throw new System.ArgumentOutOfRangeException(
                nameof(rarity), rarity, "ItemRarity sin profundidad mínima definida en ShopSetupTools."),
        };
    }
}
