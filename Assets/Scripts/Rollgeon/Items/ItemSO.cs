using System;
using System.Collections.Generic;
using Rollgeon.Effects;
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

        [Title("Family")]
        [InfoBox("Agrupa las variantes de un mismo item (Botas, Coraza, Corona...). Vacio = item " +
                 "suelto. Tambien es el tag que la lista del Item Editor usa para filtrar por familia.")]
        public string FamilyId;

        // Deliberadamente NO es la rareza ni se deriva de ItemRarity: hoy las variantes de una
        // familia son tiers de rareza, pero cuando lleguen las plantillas <combo> del GDD (Corona
        // del Par, Corona del Trio...) van a ordenarse por combo, no por tier. Atar el orden a
        // ItemRarity hoy obligaria a migrar todas las familias existentes cuando eso pase.
        // docs/tools/item-editor-spec.md D1/§2.
        [InfoBox("Posicion dentro de la familia. No es la rareza — ver comentario en el codigo.")]
        [MinValue(0)]
        public int VariantIndex;

        [Title("Type")]
        [EnumToggleButtons]
        public ItemType Type;

        [Title("Pools")]
        [InfoBox("Estilo Isaac: si está activo y el jugador YA lo tiene (inventario o innato " +
                 "de su clase — ClassHeroSO.InnateItemIds), el item deja de salir en tiendas " +
                 "y loot de esta run. Para items que el GDD marca \"no stackea\".")]
        public bool UniquePerRun;

        [Title("Passive Effects")]
        [InfoBox("Se aplican automaticamente al obtener el item. Se remueven si el item se pierde.")]
        [ShowIf("@Type == ItemType.Passive")]
        [ListDrawerSettings(ShowFoldout = false)]
        // [NonSerialized] apaga la serializacion nativa de Unity sobre este campo publico. Es la
        // fuente del warning de doble serializacion de Odin en el inspector: siendo
        // SerializedScriptableObject, OnAfterDeserialize repuebla todo miembro [OdinSerialize]
        // desde serializationData DESPUES del paso nativo de Unity (ver el remark de
        // PolymorphicAuthoringContext sobre el mismo mecanismo), asi que la copia nativa del YAML
        // queda pisada en cada load — solo infla el .asset y dispara el warning. Verificado leyendo
        // Item_AmuletoReflejo.asset: los hooks reales viven en serializationData.SerializationNodes;
        // el bloque "PassiveHooks:" plano del YAML es la copia muerta que Odin ya avisa que sobra.
        [NonSerialized, OdinSerialize]
        public List<PassiveItemHook> PassiveHooks = new();

        [Title("Base Damage Override")]
        [InfoBox("SOLO para la categoría de items que redefinen el daño base (Furia Contenida, " +
                 "Egoísta — excluyentes entre sí por GDD). Mientras el item esté en el inventario, " +
                 "dmg_base_PJ de la fórmula N×M lo resuelve el reader en cada golpe.")]
        [ShowIf("@Type == ItemType.Passive")]
        // Mismo mecanismo que PassiveHooks: [NonSerialized] apaga la copia nativa de Unity,
        // el dato real vive en serializationData (SerializedScriptableObject).
        [NonSerialized, OdinSerialize]
        public BaseDamageOverrideDef BaseDamageOverride = new();

        [Title("Roll Pool")]
        [InfoBox("Bonus PERMANENTE al pool de rolls mientras el item esté en el inventario " +
                 "(sube el máximo y los rolls de arranque de cada combate). Llamado de " +
                 "Emergencia: 1. Se revierte al perder el item.")]
        [ShowIf("@Type == ItemType.Passive")]
        [MinValue(0)]
        public int RollPoolBonus;

        [Title("Active Slots")]
        [InfoBox("Slots de items activos extra mientras el item esté en el inventario. " +
                 "Mochila Grande: 1. Se revierte al perder el item.")]
        [ShowIf("@Type == ItemType.Passive")]
        [MinValue(0)]
        public int ActiveSlotBonus;

        [Title("Enchantment Altar")]
        [InfoBox("Multiplicador del costo del altar de encantamiento mientras el item esté " +
                 "en el inventario. Moneda Maldita: 0.5. 1 = sin efecto. El costo final " +
                 "nunca baja de 1.")]
        [ShowIf("@Type == ItemType.Passive")]
        [MinValue(0.01f)]
        public float EnchantmentCostMultiplier = 1f;

        [Title("Second Wind")]
        [InfoBox("Si está activo, la primera vez que el jugador llegaría a 0 HP queda con " +
                 "SecondWindRemainingHp en vez de morir, y el item SE CONSUME (se remueve " +
                 "del inventario) — eso implementa la carga única por run y su persistencia.")]
        [ShowIf("@Type == ItemType.Passive")]
        public bool SecondWind;

        [ShowIf("@Type == ItemType.Passive && SecondWind")]
        [MinValue(1)]
        public int SecondWindRemainingHp = 1;

        [Title("Active Effects")]
        [InfoBox("Se ejecutan cuando el jugador activa el item. Pueden tener cooldown.")]
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
