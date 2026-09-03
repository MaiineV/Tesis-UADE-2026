using System;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Economy;
using Rollgeon.Exploration;
using Rollgeon.Player;
using Rollgeon.Run;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.Items
{
    /// <summary>
    /// Peaje (<see cref="ItemSO.CombatToll"/>): al entrar a una sala de combate estándar
    /// ofrece pagar <c>BaseCost + CostPerFloor × piso</c> para limpiarla sin pelear. Pagar
    /// no dispara <c>OnCombatTriggered</c> — no spawnean enemigos ni cofre, así que no hay
    /// loot — y limpia la sala por <see cref="IDungeonService.MarkRoomCleared"/>, que abre
    /// las puertas sin contar como victoria de combate.
    /// </summary>
    /// <remarks>
    /// Mientras el prompt está abierto el jugador puede caminar por la sala pero no salir:
    /// las puertas de una sala Combat nacen bloqueadas y solo se abren al limpiarla. Un
    /// cambio de sala con prompt pendiente (defensivo) lo cierra y descarta la oferta.
    /// </remarks>
    public sealed class CombatTollService : ICombatSkipOffer, IDisposable
    {
        private const string LogPrefix = "[CombatTollService] ";
        private static readonly int PromptOwnerId = typeof(CombatTollService).GetHashCode();

        private readonly IInventoryService _inventory;
        private readonly IEconomyService _economy;
        private readonly IDungeonService _dungeon;
        private readonly IRunContextService _run;
        private readonly IPlayerService _player;

        private Guid _pendingInstanceId;
        private Action _pendingFight;
        private int _pendingCost;
        private bool _hasPending;

        private EventManager.EventReceiver _onRoomEntered;

        /// <summary>
        /// Seam de tests: reemplaza al <see cref="InteractionPromptView"/>. Recibe
        /// (item, costo, canAfford). Null = prompt real.
        /// </summary>
        public Action<ItemSO, int, bool> PromptPresenter;

        public bool HasPendingOffer => _hasPending;
        public Guid PendingInstanceId => _hasPending ? _pendingInstanceId : Guid.Empty;
        public int PendingCost => _hasPending ? _pendingCost : 0;

        public CombatTollService(IInventoryService inventory, IEconomyService economy,
            IDungeonService dungeon, IRunContextService run, IPlayerService player)
        {
            _inventory = inventory;
            _economy = economy;
            _dungeon = dungeon;
            _run = run;
            _player = player;

            _onRoomEntered = OnRoomEntered;
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEntered);
        }

        /// <summary>
        /// Factory: resuelve deps del <see cref="ServiceLocator"/> (las que falten quedan
        /// null y la oferta degrada a "no ofrecer") y registra como
        /// <see cref="ICombatSkipOffer"/> en <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static CombatTollService CreateAndRegister()
        {
            ServiceLocator.TryGetService<IInventoryService>(out var inventory);
            ServiceLocator.TryGetService<IEconomyService>(out var economy);
            ServiceLocator.TryGetService<IDungeonService>(out var dungeon);
            ServiceLocator.TryGetService<IRunContextService>(out var run);
            ServiceLocator.TryGetService<IPlayerService>(out var player);
            var service = new CombatTollService(inventory, economy, dungeon, run, player);
            ServiceLocator.AddService<ICombatSkipOffer>(service, ServiceScope.Run);
            return service;
        }

        public void Dispose()
        {
            if (_onRoomEntered != null)
            {
                EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEntered);
                _onRoomEntered = null;
            }
            CancelPending();
        }

        // ---- ICombatSkipOffer ---------------------------------------------------

        public bool TryOffer(RoomInstance instance, Action fight)
        {
            if (instance?.Template == null || instance.Template.Type != RoomType.Combat) return false;
            if (instance.State == RoomState.Cleared) return false;
            if (_inventory == null || _economy == null || _dungeon == null) return false;
            if (_hasPending && _pendingInstanceId == instance.InstanceId) return true; // idempotente

            var item = FindTollItem();
            if (item == null) return false;

            int cost = item.CombatToll.CostFor(_run != null ? _run.FloorIndex : 0);
            bool canAfford = _economy.CanAfford(cost);

            _pendingInstanceId = instance.InstanceId;
            _pendingFight = fight;
            _pendingCost = cost;
            _hasPending = true;

            if (PromptPresenter != null)
                PromptPresenter(item, cost, canAfford);
            else
                ShowPrompt(item, cost, canAfford);
            return true;
        }

        // ---- decisión -----------------------------------------------------------

        /// <summary>El jugador paga: descuenta, cierra el prompt y limpia la sala.</summary>
        public bool AcceptPending()
        {
            if (!_hasPending) return false;
            if (!_economy.Spend(_pendingCost))
            {
                Debug.Log(LogPrefix + $"sin oro para el peaje ({_pendingCost} G).");
                return false;
            }

            var instanceId = _pendingInstanceId;
            int paid = _pendingCost;
            ClearPending();
            InteractionPromptView.Hide(PromptOwnerId);

            _dungeon.MarkRoomCleared(instanceId);
            Debug.Log(LogPrefix + $"peaje pagado ({paid} G) — sala {instanceId} limpia sin combate.");
            EventManager.Trigger(EventName.OnCombatTollPaid,
                _player != null ? _player.PlayerGuid : Guid.Empty, instanceId, paid);
            return true;
        }

        /// <summary>El jugador pelea: cierra el prompt y arranca el combate normal.</summary>
        public void DeclinePending()
        {
            if (!_hasPending) return;
            var fight = _pendingFight;
            ClearPending();
            InteractionPromptView.Hide(PromptOwnerId);
            fight?.Invoke();
        }

        private void CancelPending()
        {
            if (!_hasPending) return;
            ClearPending();
            InteractionPromptView.Hide(PromptOwnerId);
        }

        private void ClearPending()
        {
            _hasPending = false;
            _pendingFight = null;
            _pendingCost = 0;
            _pendingInstanceId = Guid.Empty;
        }

        // Schema OnRoomEntered: [Guid roomInstanceId, string roomId]
        private void OnRoomEntered(params object[] args)
        {
            if (!_hasPending) return;
            if (args != null && args.Length >= 1 && args[0] is Guid id && id == _pendingInstanceId) return;
            CancelPending();
        }

        // ---- helpers ------------------------------------------------------------

        private ItemSO FindTollItem()
        {
            var passives = _inventory.PassiveItems;
            for (int i = 0; i < passives.Count; i++)
            {
                var item = passives[i]?.Item;
                if (item != null && item.CombatToll != null && item.CombatToll.Enabled) return item;
            }
            return null;
        }

        private void ShowPrompt(ItemSO item, int cost, bool canAfford)
        {
            string title = Rollgeon.Localization.LocalizedContent.Name(item.ItemId, item.DisplayName);
            string desc = Rollgeon.Localization.LocalizedContent.Description(item.ItemId, item.Description);
            var content = new InteractionPromptContent("F", "Pagar", title, desc, cost, canAfford);
            InteractionPromptView.ShowChoice(PromptOwnerId, in content,
                () => AcceptPending(), Key.F,
                "Esc", "Pelear", DeclinePending, Key.Escape);
        }
    }
}
