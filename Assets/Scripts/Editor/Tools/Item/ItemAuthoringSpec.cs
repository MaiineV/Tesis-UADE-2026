using System.Collections.Generic;
using Rollgeon.Items;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Plain data describing a single item to create (item-editor-spec.md §2/§3/§6.2). No UI types
    /// on purpose: this is the wire format both the Fase 3 creation wizard and the Fase 4 MCP skill
    /// build and hand to <see cref="ItemAuthoring.CreateItem"/> — it has to survive a JSON round trip.
    /// </summary>
    public struct ItemCreationSpec
    {
        public string DisplayName;
        public string Description;
        public Sprite Icon;
        public ItemRarity Rarity;
        public ItemType Type;

        /// <summary>Empty/null = loose item, not part of a family.</summary>
        public string FamilyId;

        /// <summary>Only meaningful when <see cref="FamilyId"/> is set. Null = 0.</summary>
        public int? VariantIndex;

        /// <summary>Null = derive from <see cref="Rarity"/> via <c>RarityPricing.BasePriceFor</c>.</summary>
        public int? BasePrice;

        /// <summary>Null/empty = <see cref="ItemAuthoring.DefaultFolder"/>.</summary>
        public string TargetFolder;
    }

    /// <summary>One variant inside a <see cref="ItemFamilyCreationSpec"/>.</summary>
    public struct ItemFamilyVariantSpec
    {
        public string DisplayName;

        /// <summary>Null/empty falls back to <see cref="ItemFamilyCreationSpec.DefaultDescription"/>.</summary>
        public string Description;

        /// <summary>Null falls back to <see cref="ItemFamilyCreationSpec.DefaultIcon"/>.</summary>
        public Sprite Icon;

        public ItemRarity Rarity;

        /// <summary>Null = derive from <see cref="Rarity"/>.</summary>
        public int? BasePrice;

        /// <summary>Null = this variant's position within <see cref="ItemFamilyCreationSpec.Variants"/>.</summary>
        public int? VariantIndex;
    }

    /// <summary>
    /// Specification for creating a whole family of variants (e.g. Botas Ligeras / del Viento / del
    /// Rayo / Alas de Hermes) in a single undo step (spec §6.2, §3 rule 4 "Agregar variante a la
    /// familia"). Shared fields apply to every variant; per-variant fields override them.
    /// </summary>
    public struct ItemFamilyCreationSpec
    {
        /// <summary>Shared FamilyId every created variant is stamped with. Required.</summary>
        public string FamilyId;

        public ItemType Type;

        /// <summary>Fallback description for variants that don't set their own.</summary>
        public string DefaultDescription;

        /// <summary>Fallback icon for variants that don't set their own.</summary>
        public Sprite DefaultIcon;

        /// <summary>Null/empty = <see cref="ItemAuthoring.DefaultFolder"/>.</summary>
        public string TargetFolder;

        public IReadOnlyList<ItemFamilyVariantSpec> Variants;
    }
}
