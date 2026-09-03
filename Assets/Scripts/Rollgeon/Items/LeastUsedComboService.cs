using System;
using System.Collections.Generic;
using Patterns;
using Patterns.Save;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos.Counters;
using Rollgeon.Combos.Play;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// Rezagado (<see cref="ItemSO.LeastUsedComboBonus"/>): al adquirir el item elige UNA vez
    /// el combo con menos matches de la run y, desde ahí, cada ataque con ese combo suma
    /// <c>MultiplierBonus</c> al canal aditivo de M. Mismo patrón que
    /// <see cref="DecayingMultiplierService"/>: estado por item id, persistido en el save.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Desempate.</b> Con varios combos empatados en el mínimo gana el primero en el orden
    /// de la hoja del héroe (<c>ContractSheet.Combos</c>): determinista y sin RNG.
    /// </para>
    /// <para>
    /// <b>Resume.</b> Durante un resume el inventario re-dispara <c>OnItemObtained</c>; la
    /// asignación viene del save, así que ahí no se recalcula (sería otro combo si los
    /// contadores cambiaron). Si el save no la trae (item de una versión vieja), se asigna
    /// perezosamente en el primer combo jugado.
    /// </para>
    /// </remarks>
    public sealed class LeastUsedComboService : ILeastUsedComboService, IPreloadableService, ISaveable, IDisposable
    {
        public const string SaveKeyConst = "run.item_least_used_combo";
        public const int DefaultPriority = 90;
        private const string LogPrefix = "[LeastUsedComboService] ";

        private readonly Dictionary<string, string> _assigned = new();
        private readonly List<ItemSO> _scratchItems = new();

        // Seam de tests: de dónde salen los ids de combo candidatos (default: hoja del héroe).
        private Func<IReadOnlyList<string>> _comboIdsSource;

        private Action<ComboPlayedPayload> _onComboPlayed;
        private EventManager.EventReceiver _onItemObtained;
        private EventManager.EventReceiver _onItemRemoved;
        private EventManager.EventReceiver _onRunStart;
        private bool _subscribed;

        public int Priority => DefaultPriority;

        public void Register()
        {
            ServiceLocator.AddService<ILeastUsedComboService>(this, ServiceScope.Global);
            Subscribe();
            SaveSystem.Register(this);
        }

        /// <summary>Tests: suscribe eventos y save sin pasar por el ServiceLocator.</summary>
        public void SubscribeForTests(Func<IReadOnlyList<string>> comboIdsSource = null)
        {
            _comboIdsSource = comboIdsSource;
            Subscribe();
            SaveSystem.Register(this);
        }

        public void Dispose()
        {
            SaveSystem.Unregister(this);
            Unsubscribe();
            _assigned.Clear();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _onComboPlayed = HandleComboPlayed;
            TypedEvent<ComboPlayedPayload>.Subscribe(_onComboPlayed);
            _onItemObtained = HandleItemObtained;
            EventManager.Subscribe(EventName.OnItemObtained, _onItemObtained);
            _onItemRemoved = HandleItemRemoved;
            EventManager.Subscribe(EventName.OnItemRemoved, _onItemRemoved);
            _onRunStart = _ => _assigned.Clear();
            EventManager.Subscribe(EventName.OnRunStart, _onRunStart);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            TypedEvent<ComboPlayedPayload>.Unsubscribe(_onComboPlayed);
            EventManager.UnSubscribe(EventName.OnItemObtained, _onItemObtained);
            EventManager.UnSubscribe(EventName.OnItemRemoved, _onItemRemoved);
            EventManager.UnSubscribe(EventName.OnRunStart, _onRunStart);
            _onComboPlayed = null;
            _onItemObtained = null;
            _onItemRemoved = null;
            _onRunStart = null;
            _subscribed = false;
        }

        // ---- ILeastUsedComboService --------------------------------------------

        public string GetAssignedCombo(string itemId)
            => !string.IsNullOrEmpty(itemId) && _assigned.TryGetValue(itemId, out var combo) ? combo : null;

        // ---- asignación ---------------------------------------------------------

        /// <summary>
        /// Elige el combo con menos matches en la run entre <paramref name="comboIds"/>;
        /// empate → el primero de la lista. <c>null</c> si no hay candidatos.
        /// </summary>
        public static string PickLeastUsed(IReadOnlyList<string> comboIds, Func<string, int> countOf)
        {
            if (comboIds == null || comboIds.Count == 0) return null;
            string best = null;
            int bestCount = int.MaxValue;
            for (int i = 0; i < comboIds.Count; i++)
            {
                var id = comboIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                int count = countOf != null ? countOf(id) : 0;
                if (count < bestCount)
                {
                    bestCount = count;
                    best = id;
                }
            }
            return best;
        }

        // Schema OnItemObtained: [Guid playerGuid, string itemId]
        private void HandleItemObtained(params object[] args)
        {
            if (args == null || args.Length < 2 || !(args[1] is string itemId)) return;
            // En resume la asignación viene del save (ver remarks de la clase).
            if (Rollgeon.Run.RunBootstrapper.IsResuming) return;
            TryAssign(itemId, announce: true);
        }

        private bool TryAssign(string itemId, bool announce)
        {
            if (string.IsNullOrEmpty(itemId) || _assigned.ContainsKey(itemId)) return false;
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory) || inventory == null) return false;
            var item = inventory.GetItem(itemId);
            if (item == null || item.LeastUsedComboBonus == null || !item.LeastUsedComboBonus.Enabled) return false;

            var candidates = ResolveComboIds();
            var counters = ServiceLocator.TryGetService<IComboCountersService>(out var c) ? c : null;
            var combo = PickLeastUsed(candidates, id => counters != null ? counters.GetCount(id) : 0);
            if (string.IsNullOrEmpty(combo))
            {
                Debug.LogWarning(LogPrefix + $"'{itemId}' no pudo elegir combo — la hoja del héroe no tiene combos.");
                return false;
            }

            _assigned[itemId] = combo;
            Debug.Log(LogPrefix + $"'{itemId}' → combo menos usado: '{combo}'.");
            if (announce)
                EventManager.Trigger(EventName.OnLeastUsedComboAssigned, GetPlayerGuid(), item, combo);
            return true;
        }

        private IReadOnlyList<string> ResolveComboIds()
        {
            if (_comboIdsSource != null) return _comboIdsSource();

            var sheet = ServiceLocator.TryGetService<IPlayerService>(out var ps) ? ps?.CurrentHero?.Sheet : null;
            if (sheet?.Combos == null) return Array.Empty<string>();

            var ids = new List<string>(sheet.Combos.Count);
            for (int i = 0; i < sheet.Combos.Count; i++)
            {
                var combo = sheet.Combos[i];
                if (combo != null && !string.IsNullOrEmpty(combo.ComboId)) ids.Add(combo.ComboId);
            }
            return ids;
        }

        // ---- bono en el play scratch ---------------------------------------------

        private void HandleComboPlayed(ComboPlayedPayload payload)
        {
            var playerGuid = GetPlayerGuid();
            if (playerGuid == Guid.Empty || payload.SourceGuid != playerGuid) return;
            if (payload.ActionKind != RollActionKind.Attack) return;
            if (string.IsNullOrEmpty(payload.ComboId)) return;
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory) || inventory == null) return;

            _scratchItems.Clear();
            var passives = inventory.PassiveItems;
            for (int i = 0; i < passives.Count; i++)
            {
                var item = passives[i]?.Item;
                if (item != null && item.LeastUsedComboBonus != null && item.LeastUsedComboBonus.Enabled)
                    _scratchItems.Add(item);
            }
            if (_scratchItems.Count == 0) return;

            var play = ServiceLocator.TryGetService<IComboPlayService>(out var p) ? p : null;
            var scratch = play?.CurrentPlayScratch;
            if (scratch == null) return;

            for (int i = 0; i < _scratchItems.Count; i++)
            {
                var item = _scratchItems[i];
                // Fallback perezoso: save viejo sin asignación.
                if (!_assigned.ContainsKey(item.ItemId) && !TryAssign(item.ItemId, announce: false)) continue;
                if (_assigned[item.ItemId] != payload.ComboId) continue;

                var before = ScratchSnapshot.Of(scratch);
                scratch.ComboMultiplierBonus += item.LeastUsedComboBonus.MultiplierBonus;
                ScratchSnapshot.RecordDelta(scratch, in before,
                    ScratchSourceKind.Item, item.ItemId, item, bagSlot: -1);
            }
            _scratchItems.Clear();
        }

        // Schema OnItemRemoved: [Guid playerGuid, string itemId]
        private void HandleItemRemoved(params object[] args)
        {
            if (args == null || args.Length < 2 || !(args[1] is string itemId)) return;
            _assigned.Remove(itemId);
        }

        private static Guid GetPlayerGuid()
            => ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null
                ? ps.PlayerGuid
                : Guid.Empty;

        // ---- ISaveable ----------------------------------------------------------

        public string SaveKey => SaveKeyConst;

        public object CaptureState() => new Dictionary<string, string>(_assigned);

        public void RestoreState(object state)
        {
            _assigned.Clear();
            if (state is IDictionary<string, string> dict)
            {
                foreach (var kvp in dict)
                {
                    if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value)) continue;
                    _assigned[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
