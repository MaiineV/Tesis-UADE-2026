using System.Collections.Generic;
using Rollgeon.Dice;
using Rollgeon.Items;
using Rollgeon.Items.Active;
using Rollgeon.Editor.Tools.Item.ActiveItemBuilders;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Las 7 fichas definitivas de <c>Items_Activos_Redisenados.md</c> (Feature#0084) como
    /// <see cref="ActiveItemCreationSpec"/>, listas para <see cref="ActiveItemAuthoring.CreateAll"/>.
    /// </summary>
    /// <remarks>
    /// <b>Sin <c>[MenuItem]</c> a propósito</b> — regla del proyecto (CLAUDE.md "Sin MenuItems de
    /// un solo uso"): se corre una vez vía Unity MCP <c>execute_code</c> llamando a
    /// <see cref="Run"/>, no desde un botón de menú que quedaría muerto en el repo.
    /// <para>
    /// <b>Rareza/precio placeholder.</b> Los 7 items van con <c>Rare</c> / 60 oro — balance real
    /// queda fuera de alcance de Feature#0084 (ver el plan, "Fuera de alcance").
    /// </para>
    /// <para>
    /// <b><c>BuildEffects</c> delega en <c>ActiveItemBuilders/*</c>.</b> Esta clase fija
    /// identidad, dado y estructura de resolución; cada builder arma los grupos de banda con
    /// los <c>Eff*</c> concretos del item.
    /// </para>
    /// </remarks>
    public static class ActiveItemCatalogSeed
    {
        /// <summary>Las 7 specs, en el orden del documento de diseño.</summary>
        public static IReadOnlyList<ActiveItemCreationSpec> Specs() => new List<ActiveItemCreationSpec>
        {
            new ActiveItemCreationSpec
            {
                ItemId = "blood.transfusion",
                DisplayName = "Blood Transfusion",
                DescriptionEs = "Tira un D10 propio: en 1-3 redistribuye la vida de los " +
                                 "enemigos de la sala, y en 4-10 drena al enemigo con más vida " +
                                 "y te cura por el daño infligido.",
                DescriptionEn = "Roll your own D10: on 1-3 it redistributes the room's enemy " +
                                 "health, and on 4-10 it drains the enemy with the most HP and " +
                                 "heals you for the damage dealt.",
                Rarity = ItemRarity.Rare,
                BasePrice = 60,
                Die = DiceType.D10,
                Resolution = ActiveItemResolution.Bands,
                // Los tercios de D10 darian 4-6 mixta; el doc pide 4-7.
                NegativeMaxFace = 3,
                MixedMaxFace = 7,
                BuildEffects = BloodTransfusionBuilder.Build,
            },
            new ActiveItemCreationSpec
            {
                ItemId = "coin.shield",
                DisplayName = "Coin Shield",
                DescriptionEs = "Tira un D4 como si fuera una moneda: par conserva tu escudo " +
                                 "actual hasta tu próximo turno, impar reparte escudo entre vos " +
                                 "y todos los enemigos vivos.",
                DescriptionEn = "Roll a D4 like a coin flip: even keeps your current shield " +
                                 "until your next turn, odd hands out shield to you and every " +
                                 "living enemy.",
                Rarity = ItemRarity.Rare,
                BasePrice = 60,
                Die = DiceType.D4,
                Resolution = ActiveItemResolution.Binary,
                BinaryPositiveParity = ActiveItemParity.Even,
                BuildEffects = CoinShieldBuilder.Build,
            },
            new ActiveItemCreationSpec
            {
                ItemId = "grapple.claw",
                DisplayName = "Grapple Claw",
                DescriptionEs = "Elegí una dirección y tirá un D6: te acercás o atraés al " +
                                 "objetivo enganchado esa cantidad de casillas, y con 1-2 la " +
                                 "cadena arrastra también a un enemigo cercano.",
                DescriptionEn = "Pick a direction and roll a D6: you pull yourself or your " +
                                 "hooked target that many tiles, and on 1-2 the chain also " +
                                 "drags a nearby enemy.",
                Rarity = ItemRarity.Rare,
                BasePrice = 60,
                Die = DiceType.D6,
                Resolution = ActiveItemResolution.Gradient,
                BuildEffects = GrappleClawBuilder.Build,
            },
            new ActiveItemCreationSpec
            {
                ItemId = "justa.de.justicia",
                DisplayName = "Justa de Justicia",
                DescriptionEs = "Elegí una dirección y tirá un D12: cargás esa cantidad de " +
                                 "casillas, el primer enemigo golpeado recibe ese daño y se " +
                                 "empuja según la banda del resultado.",
                DescriptionEn = "Pick a direction and roll a D12: you charge that many tiles, " +
                                 "the first enemy hit takes that much damage, and gets shoved " +
                                 "according to the roll's band.",
                Rarity = ItemRarity.Rare,
                BasePrice = 60,
                Die = DiceType.D12,
                Resolution = ActiveItemResolution.Bands,
                BuildEffects = JustaDeJusticiaBuilder.Build,
            },
            new ActiveItemCreationSpec
            {
                ItemId = "probability.drive",
                DisplayName = "Probability Drive",
                DescriptionEs = "Elegí una casilla central y tirá un D4: te teletransportás a " +
                                 "un destino seguro cercano, desde un salto errático en 1 hasta " +
                                 "elegir entre tres opciones en 4.",
                DescriptionEn = "Pick a center tile and roll a D4: you teleport to a nearby " +
                                 "safe spot, ranging from an erratic jump on 1 to choosing " +
                                 "between three options on 4.",
                Rarity = ItemRarity.Rare,
                BasePrice = 60,
                Die = DiceType.D4,
                Resolution = ActiveItemResolution.Bands,
                NegativeMaxFace = 1,
                MixedMaxFace = 3,
                BuildEffects = ProbabilityDriveBuilder.Build,
            },
            new ActiveItemCreationSpec
            {
                ItemId = "blood.d6",
                DisplayName = "Blood D6",
                DescriptionEs = "Tirá un D6 antes de tu próximo combo válido: agrega un bonus " +
                                 "de daño que en caras bajas se dispersa entre varios enemigos " +
                                 "y en caras altas se concentra en el objetivo principal.",
                DescriptionEn = "Roll a D6 before your next valid combo: it adds bonus damage " +
                                 "that spreads across several enemies on low faces and " +
                                 "concentrates on the main target on high faces.",
                Rarity = ItemRarity.Rare,
                BasePrice = 60,
                Die = DiceType.D6,
                Resolution = ActiveItemResolution.Gradient,
                BuildEffects = BloodD6Builder.Build,
            },
            new ActiveItemCreationSpec
            {
                ItemId = "bottle.o.thunder",
                DisplayName = "Bottle'o Thunder",
                DescriptionEs = "Elegí un enemigo y tirá un D4: aturde entre 1 y 4 objetivos " +
                                 "encadenados y siempre deja 2 Charcos Eléctricos en el terreno.",
                DescriptionEn = "Pick an enemy and roll a D4: it stuns between 1 and 4 chained " +
                                 "targets and always leaves 2 Electric Puddles on the ground.",
                Rarity = ItemRarity.Rare,
                BasePrice = 60,
                Die = DiceType.D4,
                Resolution = ActiveItemResolution.Hierarchy,
                BuildEffects = BottleOThunderBuilder.Build,
            },
        };

        /// <summary>Path del catálogo canónico de items del proyecto.</summary>
        public const string CatalogPath = "Assets/Rollgeon/Items/ItemCatalog.asset";

        /// <summary>
        /// Carga <see cref="CatalogPath"/> y <see cref="ItemShopPriceBridge.DefaultShopPoolPath"/> y
        /// da de alta los 7 items (idempotente). Pensado para invocarse una sola vez vía Unity MCP
        /// <c>execute_code</c> — ver el remark de la clase.
        /// </summary>
        public static string Run()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(CatalogPath);
            if (catalog == null)
                return $"ActiveItemCatalogSeed: no se encontró el catálogo en '{CatalogPath}'.";

            var shopPool = ItemShopPriceBridge.LoadDefaultPool();
            if (shopPool == null)
            {
                return "ActiveItemCatalogSeed: no se encontró el ShopPool en "
                       + $"'{ItemShopPriceBridge.DefaultShopPoolPath}'.";
            }

            var (created, skipped) = ActiveItemAuthoring.CreateAll(Specs(), catalog, shopPool, out var report);

            // Las tablas de localizacion se editan via Undo: si despues corre un test que
            // llama Undo.PerformUndo (AITreeTopologyTests, PolymorphicAuthoringContextTests)
            // las keys sembradas se revierten y el siguiente SaveAssets las borra del disco.
            Undo.ClearAll();
            AssetDatabase.SaveAssets();

            return $"ActiveItemCatalogSeed: {created} creados, {skipped} salteados.\n{report}";
        }
    }
}
