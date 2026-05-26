using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Definición autoral de un ítem vendible en shops. Placeholder MVP hasta
    /// que aterrice <c>RewardEntrySO</c> (§19): mismo shape semántico —
    /// <see cref="ItemId"/> es stable-string (va a <c>ShopItemState.ReservedItemId</c>),
    /// <see cref="DisplayName"/> / <see cref="Description"/> / <see cref="Icon"/>
    /// los consume el <c>ItemInspectView</c> (§D.6b). Cuando §19 aterrice, este
    /// SO se deprecia y <see cref="WeightedShopItem"/> pasa a referenciar
    /// <c>RewardEntrySO</c> sin cambio de data (el <c>ReservedItemId</c> se
    /// preserva).
    /// </summary>
    [CreateAssetMenu(
        menuName = "Rollgeon/Shop/Shop Item Def",
        fileName = "ShopItem")]
    public sealed class ShopItemDef : ScriptableObject, IShopRewardEntry
    {
        [Title("Identity")]
        [Tooltip("String-id estable del ítem. Se persiste como ReservedItemId en ShopItemState (§13.6).")]
        [Required]
        public string ItemId;

        [Title("Display")]
        public string DisplayName;

        [TextArea(2, 5)]
        public string Description;

        [PreviewField(48, ObjectFieldAlignment.Left)]
        public Sprite Icon;

        // ---- IShopRewardEntry (explicit impl — los fields publicos no satisfacen properties de interface) -----
        string IShopRewardEntry.EntryId => ItemId;
        string IShopRewardEntry.DisplayName => DisplayName;
        string IShopRewardEntry.Description => Description;
        Sprite IShopRewardEntry.Icon => Icon;
    }
}
