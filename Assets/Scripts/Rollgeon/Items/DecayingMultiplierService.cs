using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos.Play;
using Patterns.Save;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// "Eco Menguante" (decisión GD 2026-09-03): el item multiplica el daño de ATAQUE por
    /// <c>Start − combos × DecayPerCombo</c> y descuenta un combo con CADA combo de combate
    /// jugado (ataque, defensa o cura); al tocar <c>Min</c> se rompe (sale del inventario +
    /// <see cref="EventName.OnItemBrokeDown"/>). El contador es de RUN: persiste entre
    /// combates y en el save (<see cref="ISaveable"/>, key <see cref="SaveKeyConst"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Multiplica ANTES de descontar.</b> El primer ataque sale a <c>Start</c>; el combo
    /// que deja el contador en el piso todavía pega con el valor previo y recién después el
    /// item se rompe — nunca pega a <c>Min</c>. Escribe al <c>CurrentPlayScratch</c> dentro
    /// del dispatch de ComboPlayed (la ventana de combo jugado está abierta) con atribución
    /// en el journal, así el breakdown muestra el icono del item como con cualquier hook.
    /// </para>
    /// <para>
    /// <b>Patrón SecondWind:</b> lee los pasivos del inventario en cada evento (sin
    /// Register/Unregister en InventoryService). El contador se borra cuando el item sale
    /// del inventario (<c>OnItemRemoved</c>) — si vuelve a aparecer en una tienda arranca
    /// de cero — y al empezar una run.
    /// </para>
    /// </remarks>
    public sealed class DecayingMultiplierService : IDecayingMultiplierService, ISaveable, IDisposable
    {
        public const string SaveKeyConst = "run.item_decay";
        private const string LogPrefix = "[DecayingMultiplierService] ";

        private readonly Dictionary<string, int> _combos = new();
        private readonly List<ItemSO> _scratchItems = new();

        private Action<ComboPlayedPayload> _onComboPlayed;
        private EventManager.EventReceiver _onRunStart;
        private EventManager.EventReceiver _onItemRemoved;

        public DecayingMultiplierService()
        {
            _onComboPlayed = HandleComboPlayed;
            TypedEvent<ComboPlayedPayload>.Subscribe(_onComboPlayed);
            _onRunStart = _ => _combos.Clear();
            EventManager.Subscribe(EventName.OnRunStart, _onRunStart);
            _onItemRemoved = HandleItemRemoved;
            EventManager.Subscribe(EventName.OnItemRemoved, _onItemRemoved);
            SaveSystem.Register(this);
        }

        public void Dispose()
        {
            SaveSystem.Unregister(this);
            if (_onComboPlayed != null) TypedEvent<ComboPlayedPayload>.Unsubscribe(_onComboPlayed);
            if (_onRunStart != null) EventManager.UnSubscribe(EventName.OnRunStart, _onRunStart);
            if (_onItemRemoved != null) EventManager.UnSubscribe(EventName.OnItemRemoved, _onItemRemoved);
            _onComboPlayed = null;
            _onRunStart = null;
            _onItemRemoved = null;
            _combos.Clear();
        }

        // ---- IDecayingMultiplierService ----------------------------------------

        public int GetCombosPlayed(string itemId)
            => !string.IsNullOrEmpty(itemId) && _combos.TryGetValue(itemId, out var n) ? n : 0;

        public float GetCurrentMultiplier(ItemSO item)
        {
            if (item == null || item.DecayingMultiplier == null || !item.DecayingMultiplier.Enabled) return 1f;
            return item.DecayingMultiplier.MultiplierAfter(GetCombosPlayed(item.ItemId));
        }

        // ---- handlers -----------------------------------------------------------

        private void HandleComboPlayed(ComboPlayedPayload payload)
        {
            var playerGuid = GetPlayerGuid();
            if (playerGuid == Guid.Empty || payload.SourceGuid != playerGuid) return;
            if (!payload.ActionKind.IsCombatPayable()) return;
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory) || inventory == null) return;

            // Snapshot: romper un item lo saca de PassiveItems en medio del recorrido.
            _scratchItems.Clear();
            var passives = inventory.PassiveItems;
            for (int i = 0; i < passives.Count; i++)
            {
                var item = passives[i]?.Item;
                if (item != null && item.DecayingMultiplier != null && item.DecayingMultiplier.Enabled)
                    _scratchItems.Add(item);
            }
            if (_scratchItems.Count == 0) return;

            var play = ServiceLocator.TryGetService<IComboPlayService>(out var p) ? p : null;
            var scratch = payload.ActionKind == RollActionKind.Attack ? play?.CurrentPlayScratch : null;

            for (int i = 0; i < _scratchItems.Count; i++)
            {
                var item = _scratchItems[i];
                var def = item.DecayingMultiplier;
                int played = GetCombosPlayed(item.ItemId);

                if (scratch != null)
                {
                    var before = ScratchSnapshot.Of(scratch);
                    scratch.ComboDamageMultiplier *= def.MultiplierAfter(played);
                    ScratchSnapshot.RecordDelta(scratch, in before,
                        ScratchSourceKind.Item, item.ItemId, item, bagSlot: -1);
                }

                played++;
                _combos[item.ItemId] = played;

                if (def.BreakAtMin && def.ReachedMin(played))
                {
                    inventory.RemoveItem(item.ItemId); // dispara OnItemRemoved → borra el contador
                    Debug.Log(LogPrefix + $"'{item.ItemId}' se rompió tras {played} combos.");
                    EventManager.Trigger(EventName.OnItemBrokeDown, playerGuid, item, played);
                }
            }
            _scratchItems.Clear();
        }

        // Schema OnItemRemoved: [Guid playerGuid, string itemId]
        private void HandleItemRemoved(params object[] args)
        {
            if (args == null || args.Length < 2 || !(args[1] is string itemId)) return;
            _combos.Remove(itemId);
        }

        private static Guid GetPlayerGuid()
            => ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null
                ? ps.PlayerGuid
                : Guid.Empty;

        // ---- ISaveable ----------------------------------------------------------

        public string SaveKey => SaveKeyConst;

        public object CaptureState() => new Dictionary<string, int>(_combos);

        public void RestoreState(object state)
        {
            _combos.Clear();
            if (state is IDictionary<string, int> dict)
            {
                foreach (var kvp in dict)
                {
                    if (string.IsNullOrEmpty(kvp.Key)) continue;
                    _combos[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
