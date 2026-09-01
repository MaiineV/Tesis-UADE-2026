using System;
using System.Collections.Generic;
using Patterns;
using Patterns.Save;
using UnityEngine;

namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Slot unico de item activo. Reemplazo directo: equipar descarta lo anterior sin
    /// preguntar y sin devolverlo al inventario.
    /// </summary>
    /// <remarks>
    /// El descartado se pierde de verdad, y su encantamiento con el: "el encantamiento se
    /// queda con el ítem descartado, no se transfiere al nuevo ítem equipado".
    /// </remarks>
    public sealed class EquippedActiveItemService : IEquippedActiveItemService, ISaveable, IDisposable
    {
        private const string LogPrefix = "[EquippedActiveItemService] ";

        private readonly ItemCatalogSO _catalog;
        private readonly IReadOnlyList<ActiveItemEnchantmentSO> _enchantments;
        private readonly EventManager.EventReceiver _onCombatStartHandler;

        private int _enchantmentUsesLeft;

        public EquippedActiveItemService(ItemCatalogSO catalog,
            IReadOnlyList<ActiveItemEnchantmentSO> enchantments = null)
        {
            _catalog = catalog;
            _enchantments = enchantments;

            // El GDD pide que los usos limitados reseteen entre combates, no que se
            // agoten para toda la run.
            _onCombatStartHandler = _ => ResetEnchantmentUses();
            EventManager.Subscribe(EventName.OnCombatStart, _onCombatStartHandler);
        }

        public ItemSO Current { get; private set; }

        public bool HasItem => Current != null;

        public ActiveItemEnchantmentSO Enchantment { get; private set; }

        public int EnchantmentUsesLeft =>
            Enchantment == null ? 0 : (Enchantment.IsLimited ? _enchantmentUsesLeft : int.MaxValue);

        public event Action<ItemSO, ItemSO> OnEquippedChanged;

        public ItemSO Equip(ItemSO item)
        {
            if (item != null && item.Type != ItemType.Active)
            {
                Debug.LogWarning(LogPrefix + $"'{item.ItemId}' no es ItemType.Active — el slot no cambia.");
                return null;
            }

            // Reequipar lo mismo no dispara un descarte fantasma del propio item, y no
            // tiene por que tirar su encantamiento.
            if (ReferenceEquals(item, Current)) return null;

            var discarded = Current;
            Current = item;

            // El encantamiento se va con el item descartado.
            Enchantment = null;
            _enchantmentUsesLeft = 0;

            OnEquippedChanged?.Invoke(Current, discarded);
            return discarded;
        }

        public ItemSO Clear() => Equip(null);

        // ======================================================================
        // Encantamiento (§25): maximo 1, se pisa
        // ======================================================================

        public bool ApplyEnchantment(ActiveItemEnchantmentSO enchantment)
        {
            if (!HasItem)
            {
                Debug.LogWarning(LogPrefix + "no hay item equipado — el encantamiento no tiene donde vivir.");
                return false;
            }

            Enchantment = enchantment;
            ResetEnchantmentUses();
            OnEquippedChanged?.Invoke(Current, null);
            return true;
        }

        public void ConsumeEnchantmentUse()
        {
            if (Enchantment == null || !Enchantment.IsLimited) return;
            if (_enchantmentUsesLeft > 0) _enchantmentUsesLeft--;
        }

        private void ResetEnchantmentUses()
            => _enchantmentUsesLeft = Enchantment != null ? Enchantment.UsesPerCombat : 0;

        public void Dispose()
            => EventManager.UnSubscribe(EventName.OnCombatStart, _onCombatStartHandler);

        // ======================================================================
        // Save / Restore
        // ======================================================================

        /// <summary>
        /// Se persiste el item, su encantamiento y los usos que le quedan — el GDD §34
        /// pide los tres. El dado y la familia viven en el <see cref="ItemSO"/> del
        /// catalogo, asi que no hace falta guardarlos.
        /// </summary>
        public string SaveKey => "run.active_item";

        public object CaptureState()
        {
            if (Current == null) return null;
            return new Dictionary<string, object>
            {
                { "itemId", Current.ItemId },
                { "enchantmentId", Enchantment != null ? Enchantment.EnchantmentId : null },
                { "enchantmentUsesLeft", _enchantmentUsesLeft },
            };
        }

        public void RestoreState(object state)
        {
            Current = null;
            Enchantment = null;
            _enchantmentUsesLeft = 0;

            // Los saves viejos guardaban solo el ItemId como string plano.
            string itemId = state as string;
            string enchantmentId = null;
            int usesLeft = 0;

            if (state is Dictionary<string, object> dict)
            {
                itemId = dict.TryGetValue("itemId", out var i) ? i as string : null;
                enchantmentId = dict.TryGetValue("enchantmentId", out var e) ? e as string : null;
                if (dict.TryGetValue("enchantmentUsesLeft", out var u) && u != null)
                    int.TryParse(u.ToString(), out usesLeft);
            }

            if (string.IsNullOrEmpty(itemId))
            {
                OnEquippedChanged?.Invoke(null, null);
                return;
            }

            if (_catalog == null)
            {
                Debug.LogWarning(LogPrefix + "sin catalogo — no se puede restaurar el item equipado.");
                return;
            }

            var item = _catalog.GetById(itemId);
            if (item == null)
            {
                // Un id que ya no existe en el catalogo (item renombrado o borrado entre
                // versiones) deja el slot vacio en vez de romper la carga de la run.
                Debug.LogWarning(LogPrefix + $"'{itemId}' no esta en el catalogo — el slot queda vacio.");
            }
            else
            {
                Current = item;
                Enchantment = FindEnchantment(enchantmentId);
                _enchantmentUsesLeft = usesLeft;
            }

            OnEquippedChanged?.Invoke(Current, null);
        }

        private ActiveItemEnchantmentSO FindEnchantment(string id)
        {
            if (string.IsNullOrEmpty(id) || _enchantments == null) return null;
            for (int i = 0; i < _enchantments.Count; i++)
            {
                var e = _enchantments[i];
                if (e != null && string.Equals(e.EnchantmentId, id, StringComparison.Ordinal)) return e;
            }

            Debug.LogWarning(LogPrefix + $"encantamiento '{id}' no esta en el pool — el item queda sin el.");
            return null;
        }
    }
}
