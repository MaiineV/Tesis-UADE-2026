using System;
using System.Collections.Generic;
using Patterns.Save;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Estado run-scoped de los encantamientos del bag. Una instancia fresh
    /// se crea en <c>OnRunStart</c> a partir del <see cref="DiceBagSO"/> del player
    /// y se libera en <c>OnRunEnd</c> via <c>ClearScope(Run)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Layout.</b> Para cada slot del bag (5 dados), una lista SIN TECHO de
    /// <see cref="EnchantmentSO"/> aplicados, en orden de append. Los
    /// encantamientos solo se suman (<see cref="AddEnchantment"/>) — nunca se
    /// reemplazan. Remover (triggers tipo Explode) deja un <b>tombstone</b>
    /// (null en su índice) en vez de compactar: eso mantiene estables los
    /// índices de <see cref="EnchantmentSlotRef"/>, las keys de counters y la
    /// iteración durante un dispatch. <b>Los <see cref="EnchantmentSO"/> son
    /// punteros al catálogo</b> — no se clonan; mutación va exclusivamente via
    /// los métodos de esta clase.
    /// </para>
    /// <para>
    /// <b>Counters.</b> El diccionario per <c>(bagIndex, enchSlotIndex, key)</c>
    /// es la fuente de verdad de counters para triggers stateful (ej.
    /// <c>ExplodeIfUnusedForTurns</c>). Cuando se quita un encantamiento, sus
    /// counters se purgan via <see cref="ClearCountersForSlot"/>. Los
    /// <b>die counters</b> (<c>(bagIndex, key)</c>) son estado per-dado — hoy
    /// el contador de rolls del altar que escala el costo.
    /// </para>
    /// </remarks>
    public sealed class RuntimeDiceBag : ISaveable, IDisposable
    {
        public const string SaveKeyConst = "run.dice_enchantments";
        private const string LogPrefix = "[RuntimeDiceBag] ";

        private readonly DiceType[] _dice;
        private readonly List<EnchantmentSO>[] _enchantments;
        // Carril del dado de Movimiento (§6.6): misma semántica append + tombstones que
        // los slots del bag, indexado por EnchantmentSlotRef.MovementDieSlot.
        private readonly List<EnchantmentSO> _movementEnchantments = new List<EnchantmentSO>();
        private readonly Dictionary<(int bag, int slot, string key), int> _counters
            = new Dictionary<(int bag, int slot, string key), int>();
        private readonly Dictionary<(int bag, string key), int> _dieCounters
            = new Dictionary<(int bag, string key), int>();
        private readonly Func<string, EnchantmentSO> _resolveById;

        /// <summary>Tipos de dado en el bag, en orden de slot. NO incluye el dado de Movimiento.</summary>
        public IReadOnlyList<DiceType> Dice => _dice;

        /// <summary>
        /// Caras extra que el dado de Movimiento sumó en la run (GDD Dice Builder: "un d4 puede
        /// terminar con 6 caras sin cambiar de tipo"). El tipo base vive en la clase
        /// (<c>ClassHeroSO.StartingMovementDie</c>); acá solo el delta, que persiste con el bag.
        /// </summary>
        public int MovementExtraFaces { get; private set; }

        /// <summary>Suma (o resta) caras al dado de Movimiento. Nunca baja de 0. Devuelve el total.</summary>
        public int AddMovementExtraFaces(int delta)
        {
            MovementExtraFaces = Math.Max(0, MovementExtraFaces + delta);
            return MovementExtraFaces;
        }

        /// <summary><c>true</c> si el índice es el carril del dado de Movimiento.</summary>
        public static bool IsMovementDie(int bagIndex) => bagIndex == EnchantmentSlotRef.MovementDieSlot;

        /// <summary>Índice con lista de encantamientos: un slot del bag o el carril de Movimiento.</summary>
        public bool IsValidIndex(int bagIndex)
            => IsMovementDie(bagIndex) || (bagIndex >= 0 && bagIndex < _enchantments.Length);

        private List<EnchantmentSO> ResolveList(int bagIndex)
        {
            if (IsMovementDie(bagIndex)) return _movementEnchantments;
            if (bagIndex < 0 || bagIndex >= _enchantments.Length) return null;
            return _enchantments[bagIndex];
        }

        /// <param name="resolveById">
        /// Resolver UpgradeId → <see cref="EnchantmentSO"/> para rehidratar un save
        /// (§15). Null = el restore descarta enchantments con warning.
        /// </param>
        public RuntimeDiceBag(IReadOnlyList<DiceType> dice, Func<string, EnchantmentSO> resolveById = null)
        {
            if (dice == null) throw new ArgumentNullException(nameof(dice));
            _resolveById = resolveById;
            _dice = new DiceType[dice.Count];
            _enchantments = new List<EnchantmentSO>[dice.Count];
            for (int i = 0; i < dice.Count; i++)
            {
                _dice[i] = dice[i];
                _enchantments[i] = new List<EnchantmentSO>();
            }
        }

        // ---- Enchantment list ------------------------------------------------

        /// <summary>
        /// Largo de la lista de encantamientos del dado, tombstones incluidos.
        /// NO es un techo — la lista crece con cada <see cref="AddEnchantment"/>.
        /// </summary>
        public int GetEnchantmentCount(int bagIndex)
        {
            var list = ResolveList(bagIndex);
            return list?.Count ?? 0;
        }

        /// <summary>Lectura de los encantamientos del dado. Puede contener nulls (tombstones de removes).</summary>
        public IReadOnlyList<EnchantmentSO> GetEnchantments(int bagIndex)
        {
            var list = ResolveList(bagIndex);
            return list ?? (IReadOnlyList<EnchantmentSO>)Array.Empty<EnchantmentSO>();
        }

        /// <summary>
        /// Lee un slot específico. Devuelve null si el slot está tombstoneado o el
        /// índice es inválido.
        /// </summary>
        public EnchantmentSO GetEnchantmentAt(int bagIndex, int enchSlotIndex)
        {
            var list = ResolveList(bagIndex);
            if (list == null) return null;
            if (enchSlotIndex < 0 || enchSlotIndex >= list.Count) return null;
            return list[enchSlotIndex];
        }

        /// <summary>
        /// Suma un encantamiento a la lista del dado. Devuelve el índice asignado
        /// (identidad estable para counters/triggers), o -1 si el índice de bag es
        /// inválido o el encantamiento es null. No dispara triggers — el caller
        /// (<c>DiceEnchantmentService</c>) coordina los hooks <c>OnEnchantmentApplied</c>.
        /// </summary>
        public int AddEnchantment(int bagIndex, EnchantmentSO ench)
        {
            if (ench == null) return -1;
            var list = ResolveList(bagIndex);
            if (list == null) return -1;
            list.Add(ench);
            return list.Count - 1;
        }

        /// <summary>
        /// Escribe un slot existente. <c>null</c> tombstonea (camino de Remove);
        /// no puede crecer la lista — para eso está <see cref="AddEnchantment"/>.
        /// </summary>
        public bool SetEnchantmentAt(int bagIndex, int enchSlotIndex, EnchantmentSO ench)
        {
            var list = ResolveList(bagIndex);
            if (list == null) return false;
            if (enchSlotIndex < 0 || enchSlotIndex >= list.Count) return false;
            list[enchSlotIndex] = ench;
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

        // ---- Die counters ----------------------------------------------------

        /// <summary>Counter per-dado (no per-slot) — ej. rolls acumulados del altar.</summary>
        public int GetDieCounter(int bagIndex, string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            return _dieCounters.TryGetValue((bagIndex, key), out var v) ? v : 0;
        }

        public int IncrementDieCounter(int bagIndex, string key, int delta = 1)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            var k = (bagIndex, key);
            int prev = _dieCounters.TryGetValue(k, out var v) ? v : 0;
            int next = prev + delta;
            _dieCounters[k] = next;
            return next;
        }

        // ---- ISaveable (§15) ---------------------------------------------------

        public string SaveKey => SaveKeyConst;

        public object CaptureState()
        {
            var snapshot = new RuntimeDiceBagSnapshot();
            for (int bag = 0; bag < _enchantments.Length; bag++)
            {
                var list = _enchantments[bag];
                for (int slot = 0; slot < list.Count; slot++)
                {
                    if (list[slot] == null || string.IsNullOrEmpty(list[slot].UpgradeId)) continue;
                    snapshot.Enchantments.Add(new EnchantmentSlotSnapshot
                    {
                        BagIndex = bag,
                        SlotIndex = slot,
                        EnchantmentId = list[slot].UpgradeId,
                    });
                }
            }
            snapshot.MovementExtraFaces = MovementExtraFaces;
            for (int slot = 0; slot < _movementEnchantments.Count; slot++)
            {
                var ench = _movementEnchantments[slot];
                if (ench == null || string.IsNullOrEmpty(ench.UpgradeId)) continue;
                snapshot.MovementEnchantments.Add(new EnchantmentSlotSnapshot
                {
                    BagIndex = EnchantmentSlotRef.MovementDieSlot,
                    SlotIndex = slot,
                    EnchantmentId = ench.UpgradeId,
                });
            }
            foreach (var kv in _counters)
            {
                snapshot.Counters.Add(new EnchantmentCounterSnapshot
                {
                    BagIndex = kv.Key.bag,
                    SlotIndex = kv.Key.slot,
                    Key = kv.Key.key,
                    Value = kv.Value,
                });
            }
            foreach (var kv in _dieCounters)
            {
                snapshot.DieCounters.Add(new DieCounterSnapshot
                {
                    BagIndex = kv.Key.bag,
                    Key = kv.Key.key,
                    Value = kv.Value,
                });
            }
            return snapshot;
        }

        public void RestoreState(object state)
        {
            for (int i = 0; i < _enchantments.Length; i++)
                _enchantments[i].Clear();
            _movementEnchantments.Clear();
            MovementExtraFaces = 0;
            _counters.Clear();
            _dieCounters.Clear();

            if (state is not RuntimeDiceBagSnapshot snapshot) return;

            MovementExtraFaces = Math.Max(0, snapshot.MovementExtraFaces);

            // Saves anteriores al dado de Movimiento no traen la lista — queda vacía.
            RestoreEnchantmentSlots(snapshot.Enchantments, movementLane: false);
            if (snapshot.MovementEnchantments != null)
                RestoreEnchantmentSlots(snapshot.MovementEnchantments, movementLane: true);

            foreach (var c in snapshot.Counters)
            {
                if (string.IsNullOrEmpty(c.Key)) continue;
                _counters[(c.BagIndex, c.SlotIndex, c.Key)] = c.Value;
            }

            // Saves anteriores al contador per-dado no traen la lista — queda vacía.
            if (snapshot.DieCounters != null)
            {
                foreach (var c in snapshot.DieCounters)
                {
                    if (string.IsNullOrEmpty(c.Key)) continue;
                    _dieCounters[(c.BagIndex, c.Key)] = c.Value;
                }
            }
        }

        private void RestoreEnchantmentSlots(List<EnchantmentSlotSnapshot> slots, bool movementLane)
        {
            if (slots == null || slots.Count == 0) return;
            if (_resolveById == null)
            {
                Debug.LogWarning(LogPrefix + "Save con enchantments pero sin resolver " +
                                 "(EnchantmentCatalogSO ausente) — se descartan.");
                return;
            }

            foreach (var e in slots)
            {
                var ench = _resolveById(e.EnchantmentId);
                if (ench == null)
                {
                    Debug.LogWarning(LogPrefix + $"Enchantment '{e.EnchantmentId}' del save " +
                                     "no existe en el catálogo — se descarta.");
                    continue;
                }
                // El carril de Movimiento se guarda en su propia lista: el BagIndex del
                // snapshot es redundante ahí y no se valida contra el bag.
                var list = movementLane ? _movementEnchantments : ResolveList(e.BagIndex);
                if (list == null || (!movementLane && IsMovementDie(e.BagIndex)) || e.SlotIndex < 0)
                {
                    Debug.LogWarning(LogPrefix + $"Snapshot con índice inválido ({e.BagIndex},{e.SlotIndex}) — se descarta.");
                    continue;
                }
                // Padding con tombstones hasta SlotIndex — los counters del save
                // apuntan a índices de append, que deben restaurarse idénticos.
                while (list.Count <= e.SlotIndex) list.Add(null);
                list[e.SlotIndex] = ench;
            }
        }

        // ---- IDisposable -------------------------------------------------------

        /// <summary>
        /// Invocado por <c>ClearScope(Run)</c>. El Unregister explícito evita que dos
        /// instancias con la misma SaveKey convivan en el registry entre runs.
        /// </summary>
        public void Dispose()
        {
            SaveSystem.Unregister(this);
        }
    }

    /// <summary>DTO serializable de <see cref="RuntimeDiceBag"/> (§15).</summary>
    [Serializable]
    public class RuntimeDiceBagSnapshot
    {
        public List<EnchantmentSlotSnapshot> Enchantments = new List<EnchantmentSlotSnapshot>();
        public List<EnchantmentCounterSnapshot> Counters = new List<EnchantmentCounterSnapshot>();
        public List<DieCounterSnapshot> DieCounters = new List<DieCounterSnapshot>();
        // Dado de Movimiento (§6.6) — lista aparte para que saves previos restauren igual.
        public int MovementExtraFaces;
        public List<EnchantmentSlotSnapshot> MovementEnchantments = new List<EnchantmentSlotSnapshot>();
    }

    [Serializable]
    public class EnchantmentSlotSnapshot
    {
        public int BagIndex;
        public int SlotIndex;
        public string EnchantmentId;
    }

    [Serializable]
    public class EnchantmentCounterSnapshot
    {
        public int BagIndex;
        public int SlotIndex;
        public string Key;
        public int Value;
    }

    [Serializable]
    public class DieCounterSnapshot
    {
        public int BagIndex;
        public string Key;
        public int Value;
    }
}
