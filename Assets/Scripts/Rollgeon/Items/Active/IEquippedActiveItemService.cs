using System;

namespace Rollgeon.Items.Active
{
    /// <summary>
    /// El <b>slot unico</b> de item activo del jugador. GDD "Ítems Activos":
    /// <c>ActiveItemSlots = 1</c>, no configurable.
    /// </summary>
    /// <remarks>
    /// Vive aparte de <see cref="IInventoryService"/> a proposito: el item activo
    /// <i>no</i> ocupa un slot de inventario, es su propio espacio. Equipar otro descarta
    /// el que habia — "el ítem activo, su dado y su encantamiento se mantienen equipados
    /// hasta que el jugador lo reemplace por otro", y lo descartado no se recupera.
    /// </remarks>
    public interface IEquippedActiveItemService
    {
        /// <summary>El item equipado, o <c>null</c> si el slot esta vacio.</summary>
        ItemSO Current { get; }

        /// <summary><c>true</c> si hay algo equipado.</summary>
        bool HasItem { get; }

        /// <summary>
        /// Equipa <paramref name="item"/> y descarta el anterior.
        /// </summary>
        /// <param name="item">
        /// Item a equipar. <c>null</c> vacia el slot. Un item que no sea
        /// <see cref="ItemType.Active"/> se rechaza sin tocar el slot.
        /// </param>
        /// <returns>
        /// El item que estaba equipado y quedo descartado, o <c>null</c> si el slot
        /// estaba vacio o el equipado fue rechazado.
        /// </returns>
        ItemSO Equip(ItemSO item);

        /// <summary>
        /// Vacia el slot. Devuelve lo que estaba equipado, o <c>null</c>.
        /// </summary>
        ItemSO Clear();

        /// <summary>
        /// Disparado tras cada cambio del slot: <c>(equipado, descartado)</c>. Cualquiera
        /// de los dos puede ser <c>null</c>.
        /// </summary>
        event Action<ItemSO, ItemSO> OnEquippedChanged;
    }
}
