using Rollgeon.Editor.Tools.Polymorphic;
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

        partial void OnPriceAssetsRefreshed()
        {
            _priceePool = null;
            ItemPoolMembership.InvalidateCache();
        }

        protected override void DrawRootExtras(ItemSO asset)
        {
            if (asset == null) return;

            EditorGUILayout.Space(2);
            if (DrawExtrasSection("Shop")) DrawShopPrice(asset);
            if (DrawExtrasSection("Pools")) DrawPoolsSection(asset);
        }

        /// <summary>
        /// Cabecera de Shop y Pools, con el mismo aspecto y la misma persistencia que las categorías
        /// que vienen del <c>[Title]</c> de Odin.
        /// </summary>
        /// <remarks>
        /// Estas dos no tienen <c>[Title]</c> del que colgarse — no son campos del asset —, así que
        /// dibujan su propio título y su línea. Pasan por el mismo helper que las otras para que no se
        /// note de dónde salió cada una.
        /// </remarks>
        static bool DrawExtrasSection(string title)
        {
            var key = PolymorphicBlockDrawer.SectionKeyOf(nameof(ItemSO), title);
            bool expanded = EditorPrefs.GetBool(key, true);
            bool next = PolymorphicBlockDrawer.SectionToggle(title, expanded, drawOwnTitle: true);
            if (next != expanded) EditorPrefs.SetBool(key, next);
            return next;
        }

        void DrawShopPrice(ItemSO asset)
        {
            var pool = PricePool;
            if (pool == null)
            {
                EditorGUILayout.HelpBox(
                    $"No hay ShopPool en '{ItemShopPriceBridge.DefaultShopPoolPath}' — el precio vive ahí.",
                    MessageType.Warning);
                return;
            }

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

        /// <summary>
        /// De qué pools sale el ítem, con casillas para entrarlo o sacarlo.
        /// </summary>
        /// <remarks>
        /// Un ítem se consigue por varias vías — tienda y las pools de cofre por rareza — y no había
        /// ninguna vista que lo dijera: para saberlo había que abrir los `.asset` de cada pool y
        /// buscar el GUID a mano. La tienda ya se editaba arriba porque lleva precio; el resto es
        /// pertenencia pelada y entra acá.
        /// </remarks>
        void DrawPoolsSection(ItemSO asset)
        {
            var pools = ItemPoolMembership.GetPools(asset);
            if (pools.Count == 0) return;

            int inPools = 0;
            foreach (var p in pools) if (p.Contains) inPools++;

            if (inPools == 0)
                EditorGUILayout.HelpBox(
                    "No está en ninguna pool — no se puede conseguir jugando.", MessageType.Warning);

            foreach (var entry in pools)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool next = EditorGUILayout.ToggleLeft(entry.Name, entry.Contains);
                    if (next != entry.Contains)
                        ItemPoolMembership.Set(
                            asset, entry.Pool, next, RarityPricing.BasePriceFor(asset.Rarity));

                    if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40)))
                        EditorGUIUtility.PingObject(entry.Pool);
                }
            }
        }
    }
}
