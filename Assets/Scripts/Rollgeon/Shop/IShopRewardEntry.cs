using UnityEngine;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Superficie común de todo lo que se puede ofrecer en una tienda:
    /// <see cref="ShopItemDef"/> (items consumibles activos) y
    /// <see cref="Rollgeon.Upgrades.Combos.ComboPassiveSO"/> (pasivas de combo
    /// del Sistema de Mejoras In-Run). El <c>ShopPoolSO</c> y el
    /// <c>ShopManagerService</c> operan sobre esta abstracción; el dispatch de
    /// "qué hacer al comprar" se hace por tipo concreto en
    /// <see cref="ShopItemPedestalInteractable"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué solo metadata.</b> La interface expone únicamente
    /// identidad + display (id, nombre, descripción, ícono). La <i>aplicación</i>
    /// del reward es responsabilidad del consumer downstream (inventory para
    /// items, <c>ComboPassiveService</c> para pasivas, etc.). Mantener esto
    /// mínimo facilita agregar nuevos tipos de reward sin cambiar la interface.
    /// </para>
    /// </remarks>
    public interface IShopRewardEntry
    {
        /// <summary>String-id estable. ShopItemDef.ItemId / ComboPassiveSO.UpgradeId.</summary>
        string EntryId { get; }

        /// <summary>Nombre legible para UI / tooltip.</summary>
        string DisplayName { get; }

        /// <summary>Descripción para tooltip / item inspect.</summary>
        string Description { get; }

        /// <summary>Icono opcional.</summary>
        Sprite Icon { get; }
    }
}
