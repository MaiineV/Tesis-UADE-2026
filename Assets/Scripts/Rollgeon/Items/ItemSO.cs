using System;
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

        [InfoBox("Opt-in: tener CUALQUIER item de esta familia en el inventario bloquea este " +
                 "item en tiendas y loot (incluye duplicados de si mismo). Para pares " +
                 "excluyentes por GDD (Corazon/Tesoro de la Fortuna). NO activar en familias " +
                 "de variantes que deben convivir (corona, botas, coraza...).")]
        [ShowIf("@!string.IsNullOrEmpty(FamilyId)")]
        public bool FamilyExclusive;

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

        [Title("Combo Rules")]
        [InfoBox("Mientras el item esté en el inventario, la Escalera también acepta progresiones " +
                 "con un valor omitido (paso 2, cualquier paridad: 3-5-7-9-11, 2-4-6-8-10). " +
                 "Sigue siendo combo.ladder. Compás Salteado. Se revierte al perder el item.")]
        [ShowIf("@Type == ItemType.Passive")]
        public bool LadderSkippedStep;

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

        [InfoBox("Multiplica el peso de los encantamientos malditos (CapCursed / categoría " +
                 "Maldición) en el pool del altar mientras el item esté en el inventario. " +
                 "Moneda Maldita: 3. 1 = sin efecto.")]
        [ShowIf("@Type == ItemType.Passive")]
        [MinValue(0.01f)]
        public float CursedEnchantmentWeightMultiplier = 1f;

        [Title("Second Wind")]
        [InfoBox("Si está activo, la primera vez que el jugador llegaría a 0 HP queda con " +
                 "SecondWindRemainingHp en vez de morir, y el item SE CONSUME (se remueve " +
                 "del inventario) — eso implementa la carga única por run y su persistencia.")]
        [ShowIf("@Type == ItemType.Passive")]
        public bool SecondWind;

        [ShowIf("@Type == ItemType.Passive && SecondWind")]
        [MinValue(1)]
        public int SecondWindRemainingHp = 1;

        // ==================================================================
        // Modelo nuevo (GDD "Ítems Activos"): slot unico, dado propio y bandas.
        // ==================================================================

        [Title("Active — Modelo")]
        [InfoBox("Flag de MIGRACION. En true el item vive en el slot unico del HUD " +
                 "(modelo del rework: dado propio, bandas, 1 roll por uso) y conseguirlo " +
                 "descarta el que tuvieras. En false sigue el camino viejo: entra a " +
                 "IInventoryService.ActiveItems y se usa por OnActivate.\n\n" +
                 "Arranca en false a proposito. El GDD dice que el catalogo todavia no " +
                 "esta migrado, y la pocion depende del camino viejo — prenderlo por " +
                 "default la sacaria del inventario y romperia el boton Heal.")]
        [ShowIf("@Type == ItemType.Active")]
        public bool UsesActiveSlot;

        [Title("Active — Dado y familia")]
        [InfoBox("El item se activa pagando 1 roll y tirando SU dado. El resultado cae " +
                 "en una de tres bandas y esa banda decide que efecto corre.")]
        [ShowIf("@Type == ItemType.Active && UsesActiveSlot")]
        [Tooltip("Dado propio del item (D4 a D20). Se tira dentro del slot del HUD, " +
                 "nunca junto a los 5 dados de combate.")]
        public DiceType ActiveDie = DiceType.D6;

        [ShowIf("@Type == ItemType.Active")]
        [Tooltip("Que busca el jugador en la tirada. Define cual banda es el mejor " +
                 "resultado de este item. Precision y Control tienen mecanismo propio y " +
                 "todavia no estan implementadas — caen en el reparto por tercios.")]
        public ActiveItemFamily ActiveFamily = ActiveItemFamily.Potencia;

        [ShowIf("@Type == ItemType.Active && ActiveFamily == Rollgeon.Items.Active.ActiveItemFamily.Precision")]
        [InfoBox("Cara exacta que el item busca. Acertarla es banda positiva, quedar a 1 " +
                 "es mixta, a 2 o mas es negativa.")]
        [MinValue(1)]
        public int PrecisionTarget = 1;

        [ShowIf("@Type == ItemType.Active && ActiveFamily == Rollgeon.Items.Active.ActiveItemFamily.Control")]
        [InfoBox("Paridad que el item busca. La banda cruza dos condiciones: coincidir la " +
                 "paridad y caer en la mitad superior del dado.")]
        public ActiveItemParity ControlParity = ActiveItemParity.Even;

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
