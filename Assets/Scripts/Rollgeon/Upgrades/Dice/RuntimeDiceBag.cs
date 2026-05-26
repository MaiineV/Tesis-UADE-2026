using System;
using System.Collections.Generic;
using Rollgeon.Dice;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Estado run-scoped de los encantamientos del bag. Una instancia fresh
    /// se crea en <c>OnRunStart</c> a partir del <see cref="DiceBagSO"/> del player
    /// y se libera en <c>OnRunEnd</c> via <c>ClearScope(Run)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Layout.</b> Para cada slot del bag (5 dados), un array de longitud
    /// <c>diceType.MaxEnchantmentSlots()</c> con los <see cref="EnchantmentSO"/>
    /// aplicados. Null = cupo vacío. <b>Los <see cref="EnchantmentSO"/> son
    /// punteros al catálogo</b> — no se clonan; mutación va exclusivamente via
    /// los métodos de esta clase.
    /// </para>
    /// <para>
    /// <b>Counters.</b> El diccionario per <c>(bagIndex, enchSlotIndex, key)</c>
    /// es la fuente de verdad de counters para triggers stateful (ej.
    /// <c>ExplodeIfUnusedForTurns</c>). Cuando se quita un encantamiento, sus
    /// counters se purgan via <see cref="ClearCountersForSlot"/>.
    /// </para>
    /// </remarks>
    public sealed class RuntimeDiceBag
    {
        private readonly DiceType[] _dice;
        private readonly EnchantmentSO[][] _enchantments;
        private readonly Dictionary<(int bag, int slot, string key), int> _counters
            = new Dictionary<(int bag, int slot, string key), int>();

        /// <summary>Tipos de dado en el bag, en orden de slot.</summary>
        public IReadOnlyList<DiceType> Dice => _dice;

        public RuntimeDiceBag(IReadOnlyList<DiceType> dice)
        {
            if (dice == null) throw new ArgumentNullException(nameof(dice));
            _dice = new DiceType[dice.Count];
            _enchantments = new EnchantmentSO[dice.Count][];
            for (int i = 0; i < dice.Count; i++)
            {
                _dice[i] = dice[i];
                _enchantments[i] = new EnchantmentSO[dice[i].MaxEnchantmentSlots()];
            }
        }

        // ---- Enchantment slots ------------------------------------------------

        /// <summary>Cupos disponibles del dado en <paramref name="bagIndex"/>.</summary>
        public int GetEnchantmentSlotCount(int bagIndex)
        {
            if (bagIndex < 0 || bagIndex >= _enchantments.Length) return 0;
            return _enchantments[bagIndex].Length;
        }

        /// <summary>Lectura de los encantamientos del dado. Puede contener nulls (cupos vacíos).</summary>
        public IReadOnlyList<EnchantmentSO> GetEnchantments(int bagIndex)
        {
            if (bagIndex < 0 || bagIndex >= _enchantments.Length) return Array.Empty<EnchantmentSO>();
            return _enchantments[bagIndex];
        }

        /// <summary>
        /// Lee un slot específico. Devuelve null si el slot está vacío o el
        /// índice es inválido.
        /// </summary>
        public EnchantmentSO GetEnchantmentAt(int bagIndex, int enchSlotIndex)
        {
            if (bagIndex < 0 || bagIndex >= _enchantments.Length) return null;
            var arr = _enchantments[bagIndex];
            if (enchSlotIndex < 0 || enchSlotIndex >= arr.Length) return null;
            return arr[enchSlotIndex];
        }

        /// <summary>
        /// Escribe un encantamiento en un slot. <c>null</c> vacía el slot. No
        /// dispara triggers — el caller (<c>DiceEnchantmentService</c>) coordina
        /// los hooks <c>OnEnchantmentApplied</c>.
        /// </summary>
        public bool SetEnchantmentAt(int bagIndex, int enchSlotIndex, EnchantmentSO ench)
        {
            if (bagIndex < 0 || bagIndex >= _enchantments.Length) return false;
            var arr = _enchantments[bagIndex];
            if (enchSlotIndex < 0 || enchSlotIndex >= arr.Length) return false;
            arr[enchSlotIndex] = ench;
            return true;
        }

        // ---- Counters --------------------------------------------------------

        public int GetCounter(EnchantmentSlotRef slot, string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            var k = (slot.BagSlotIndex, slot.EnchantmentSlotIndex, key);
            return _counters.TryGetValue(k, out var v) ? v : 0;
        }

        public int IncrementCounter(EnchantmentSlotRef slot, string key, int delta = 1)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            var k = (slot.BagSlotIndex, slot.EnchantmentSlotIndex, key);
            int prev = _counters.TryGetValue(k, out var v) ? v : 0;
            int next = prev + delta;
            _counters[k] = next;
            return next;
        }

        public void ResetCounter(EnchantmentSlotRef slot, string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _counters[(slot.BagSlotIndex, slot.EnchantmentSlotIndex, key)] = 0;
        }

        /// <summary>
        /// Purga todos los counters asociados al slot — invocado por el service
        /// al remover un encantamiento para no dejar state colgado.
        /// </summary>
        public void ClearCountersForSlot(EnchantmentSlotRef slot)
        {
            var toRemove = new List<(int, int, string)>();
            foreach (var k in _counters.Keys)
            {
                if (k.bag == slot.BagSlotIndex && k.slot == slot.EnchantmentSlotIndex)
                    toRemove.Add(k);
            }
            foreach (var k in toRemove) _counters.Remove(k);
        }
    }
}
