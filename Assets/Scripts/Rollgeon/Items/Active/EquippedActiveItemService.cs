using System;
using Patterns.Save;
using UnityEngine;

namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Slot unico de item activo. Reemplazo directo: equipar descarta lo anterior sin
    /// preguntar y sin devolverlo al inventario.
    /// </summary>
    /// <remarks>
    /// El descartado se pierde de verdad — el GDD lo dice para el encantamiento y vale
    /// para el item entero: "el encantamiento se queda con el ítem descartado, no se
    /// transfiere al nuevo ítem equipado".
    /// </remarks>
    public sealed class EquippedActiveItemService : IEquippedActiveItemService, ISaveable
    {
        private const string LogPrefix = "[EquippedActiveItemService] ";

        private readonly ItemCatalogSO _catalog;

        public EquippedActiveItemService(ItemCatalogSO catalog)
        {
            _catalog = catalog;
        }

        public ItemSO Current { get; private set; }

        public bool HasItem => Current != null;

        public event Action<ItemSO, ItemSO> OnEquippedChanged;

        public ItemSO Equip(ItemSO item)
        {
            if (item != null && item.Type != ItemType.Active)
            {
                Debug.LogWarning(LogPrefix + $"'{item.ItemId}' no es ItemType.Active — el slot no cambia.");
                return null;
            }

            // Reequipar lo mismo no dispara un descarte fantasma del propio item.
            if (ReferenceEquals(item, Current)) return null;

            var discarded = Current;
            Current = item;
            OnEquippedChanged?.Invoke(Current, discarded);
            return discarded;
        }

        public ItemSO Clear() => Equip(null);

        // ======================================================================
        // Save / Restore
        // ======================================================================

        /// <summary>
        /// Solo se persiste el <c>ItemId</c>: el dado y la familia viven en el
        /// <see cref="ItemSO"/> del catalogo, no en la instancia equipada. Cuando entre
        /// el encantamiento (fase posterior) hay que sumarlo aca — el GDD §34 lo pide
        /// explicitamente.
        /// </summary>
        public string SaveKey => "run.active_item";

        public object CaptureState() => Current != null ? Current.ItemId : null;

        public void RestoreState(object state)
        {
            var id = state as string;
            if (string.IsNullOrEmpty(id))
            {
                Current = null;
                OnEquippedChanged?.Invoke(null, null);
                return;
            }

            if (_catalog == null)
            {
                Debug.LogWarning(LogPrefix + "sin catalogo — no se puede restaurar el item equipado.");
                return;
            }

            var item = _catalog.GetById(id);
            if (item == null)
            {
                // Un id que ya no existe en el catalogo (item renombrado o borrado entre
                // versiones) deja el slot vacio en vez de romper la carga de la run.
                Debug.LogWarning(LogPrefix + $"'{id}' no esta en el catalogo — el slot queda vacio.");
                Current = null;
            }
            else
            {
                Current = item;
            }

            OnEquippedChanged?.Invoke(Current, null);
        }
    }
}
