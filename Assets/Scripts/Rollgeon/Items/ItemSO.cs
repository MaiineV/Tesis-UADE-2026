using System.Collections.Generic;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Items.Active;
using Rollgeon.Shop;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Items
{
    [CreateAssetMenu(menuName = "Rollgeon/Items/Item")]
    public class ItemSO : SerializedScriptableObject, IShopRewardEntry
    {
        [Title("Identity")]
        public string ItemId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public ItemRarity Rarity;

        [Title("Type")]
        [EnumToggleButtons]
        public ItemType Type;

        [Title("Passive Effects")]
        [InfoBox("Se aplican automaticamente al obtener el item. Se remueven si el item se pierde.")]
        [ShowIf("@Type == ItemType.Passive")]
        [ListDrawerSettings(ShowFoldout = false)]
        [OdinSerialize]
        public List<PassiveItemHook> PassiveHooks = new();

        // ==================================================================
        // Modelo nuevo (GDD "Ítems Activos"): slot unico, dado propio y bandas.
        // ==================================================================

        [Title("Active — Dado y familia")]
        [InfoBox("El item se activa pagando 1 roll y tirando SU dado. El resultado cae " +
                 "en una de tres bandas y esa banda decide que efecto corre.")]
        [ShowIf("@Type == ItemType.Active")]
        [Tooltip("Dado propio del item (D4 a D20). Se tira dentro del slot del HUD, " +
                 "nunca junto a los 5 dados de combate.")]
        public DiceType ActiveDie = DiceType.D6;

        [ShowIf("@Type == ItemType.Active")]
        [Tooltip("Que busca el jugador en la tirada. Define cual banda es el mejor " +
                 "resultado de este item. Precision y Control tienen mecanismo propio y " +
                 "todavia no estan implementadas — caen en el reparto por tercios.")]
        public ActiveItemFamily Family = ActiveItemFamily.Potencia;

        [Title("Active — Efectos por banda")]
        [InfoBox("Las tres bandas tienen que tener efecto: el GDD prohibe la rama de " +
                 "'no pasa nada'. Lo que cambia entre bandas es la calidad, no si ocurre.")]
        [ShowIf("@Type == ItemType.Active")]
        [OdinSerialize]
        public EffectData OnNegativeBand = new();

        [ShowIf("@Type == ItemType.Active")]
        [OdinSerialize]
        public EffectData OnMixedBand = new();

        [ShowIf("@Type == ItemType.Active")]
        [OdinSerialize]
        public EffectData OnPositiveBand = new();

        /// <summary>
        /// Grupo de efectos que corresponde a <paramref name="band"/>. Nunca null: un
        /// item sin autorar esa banda devuelve un grupo vacio, que el pipeline trata como
        /// no-op en vez de romper.
        /// </summary>
        public EffectData GetBandEffects(ActiveItemBand band)
        {
            switch (band)
            {
                case ActiveItemBand.Negative: return OnNegativeBand ??= new EffectData();
                case ActiveItemBand.Mixed: return OnMixedBand ??= new EffectData();
                default: return OnPositiveBand ??= new EffectData();
            }
        }

        // ==================================================================
        // Modelo viejo — sigue vivo hasta que se migre el catalogo.
        // ==================================================================

        [Title("Active Effects (legacy)")]
        [InfoBox("Camino anterior al rework: un solo grupo de efectos, con cooldown y " +
                 "usos. Lo consume IInventoryService.ActivateItem. El catalogo todavia " +
                 "no esta migrado al modelo de bandas, asi que convive.")]
        [ShowIf("@Type == ItemType.Active")]
        [OdinSerialize]
        public EffectData OnActivate = new();

        [ShowIf("@Type == ItemType.Active")]
        [InfoBox("Cooldown en turnos. 0 = usable cada turno.")]
        [MinValue(0)]
        public int Cooldown = 0;

        [ShowIf("@Type == ItemType.Active")]
        [InfoBox("Si true, el slot se remueve del inventario tras un uso exitoso (consumibles tipo poción).")]
        public bool ConsumedOnUse = false;

        [Title("Action economy")]
        [ShowIf("@Type == ItemType.Active")]
        [InfoBox("Si true, usar este item activo consume un slot del turno.")]
        public bool ConsumesAction = true;

        [ShowIf("@Type == ItemType.Active && ConsumesAction")]
        [InfoBox("ActionId que se registra en action economy. Default: item.<ItemId>.")]
        public string ActionId;

        public string ResolvedActionId => string.IsNullOrEmpty(ActionId) ? $"item.{ItemId}" : ActionId;

        [Title("Visual")]
        [InfoBox("Prefab opcional para la representacion 3D del item en el mundo (pedestal, drop).")]
        public GameObject WorldPrefab;

        // ---- IShopRewardEntry (explicit impl — los fields publicos no satisfacen
        // properties de interface). EntryId mapea a ItemId; se persiste como
        // ShopItemState.ReservedItemId en re-entry de tienda. ----
        string IShopRewardEntry.EntryId => ItemId;
        string IShopRewardEntry.DisplayName => DisplayName;
        string IShopRewardEntry.Description => Description;
        Sprite IShopRewardEntry.Icon => Icon;
    }
}
