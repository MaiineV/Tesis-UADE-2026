using Rollgeon.Items;
using Rollgeon.Shop;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Precio del ítem en el panel raíz (spec §2).
    /// </summary>
    /// <remarks>
    /// El precio no es un campo del <c>ItemSO</c>: vive en el <c>BasePrice</c> del
    /// <c>WeightedShopItem</c> dentro de <c>ShopPool.asset</c>. Por eso Odin no lo dibuja con el
    /// resto y, hasta acá, solo se podía tocar al crear el ítem o desde la tab Family — o sea que
    /// para los 24 ítems que no están en una familia no había forma de editarlo sin salir de la
    /// ventana, que es exactamente lo que la spec §2 quería eliminar.
    /// </remarks>
    public sealed partial class ItemEditorWindow
    {
        ShopPoolSO _priceePool;

        /// <summary>La referencia al pool, cacheada. Buscarla en cada repaint es un escaneo de disco por frame.</summary>
        ShopPoolSO PricePool => _priceePool != null
            ? _priceePool
            : (_priceePool = ItemShopPriceBridge.LoadDefaultPool());

        partial void OnPriceAssetsRefreshed() => _priceePool = null;

        protected override void DrawRootExtras(ItemSO asset)
        {
            if (asset == null) return;

            var pool = PricePool;
            if (pool == null)
            {
                EditorGUILayout.HelpBox(
                    $"No hay ShopPool en '{ItemShopPriceBridge.DefaultShopPoolPath}' — el precio vive ahí.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Shop", EditorStyles.miniBoldLabel);

            int gddPrice = RarityPricing.BasePriceFor(asset.Rarity);

            if (ItemShopPriceBridge.TryGetPrice(pool, asset, out int price))
            {
                int next = EditorGUILayout.IntField("Precio", price);
                if (next != price) ItemShopPriceBridge.SetPrice(pool, asset, Mathf.Max(0, next));

                // El precio del GDD al lado del real: es la comparación que el diseñador hace igual,
                // y tenerla acá evita ir a la tab de métricas para responder "¿esto está caro?".
                EditorGUILayout.LabelField(
                    " ",
                    next == gddPrice
                        ? $"= el que dicta {RarityPalette.DisplayName(asset.Rarity)} en el GDD"
                        : $"el GDD dicta {gddPrice} para {RarityPalette.DisplayName(asset.Rarity)}",
                    EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.HelpBox(
                "No está en el ShopPool: no cuesta nada y no aparece en tienda.",
                MessageType.Info);

            if (GUILayout.Button($"Agregar a la tienda por {gddPrice} oro"))
                ItemShopPriceBridge.AddToPool(pool, asset, gddPrice);
        }
    }
}
