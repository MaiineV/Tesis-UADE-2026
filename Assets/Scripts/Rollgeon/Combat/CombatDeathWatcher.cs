using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Player;

namespace Rollgeon.Combat
{
    public sealed class CombatDeathWatcher : ICombatDeathWatcher
    {
        private readonly IPlayerService _player;
        private readonly ICombatSignaller _signaller;
        private readonly TurnOrderService _turnOrder;
        private readonly IEntityVisualService _visuals;
        private readonly IDungeonService _dungeon;
        private readonly IGridManager _grid;

        private readonly HashSet<Guid> _processed = new();
        private Action<DamageResolvedPayload> _handler;
        private bool _disposed;

        public CombatDeathWatcher(
            IPlayerService player,
            ICombatSignaller signaller,
            TurnOrderService turnOrder,
            IEntityVisualService visuals,
            IDungeonService dungeon,
            IGridManager grid = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _signaller = signaller ?? throw new ArgumentNullException(nameof(signaller));
            _turnOrder = turnOrder ?? throw new ArgumentNullException(nameof(turnOrder));
            _visuals = visuals;
            _dungeon = dungeon ?? throw new ArgumentNullException(nameof(dungeon));
            _grid = grid;

            _handler = OnDamageResolved;
            TypedEvent<DamageResolvedPayload>.Subscribe(_handler);
        }

        public static CombatDeathWatcher CreateAndRegister()
        {
            var player = ServiceLocator.GetService<IPlayerService>();
            var signaller = ServiceLocator.GetService<ICombatSignaller>();
            var turnOrder = ServiceLocator.GetService<TurnOrderService>();
            ServiceLocator.TryGetService<IEntityVisualService>(out var visuals);
            var dungeon = ServiceLocator.GetService<IDungeonService>();
            ServiceLocator.TryGetService<IGridManager>(out var grid);

            var watcher = new CombatDeathWatcher(player, signaller, turnOrder, visuals, dungeon, grid);
            ServiceLocator.AddService<ICombatDeathWatcher>(watcher, ServiceScope.Run);
            return watcher;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_handler != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_handler);
                _handler = null;
            }
            _processed.Clear();
        }

        private void OnDamageResolved(DamageResolvedPayload payload)
        {
            UnityEngine.Debug.Log($"[CombatDeathWatcher] OnDamageResolved — target={payload.TargetGuid} wasLethal={payload.WasLethal}");
            if (!payload.WasLethal) return;
            if (!_processed.Add(payload.TargetGuid)) return;

            if (payload.TargetGuid == _player.PlayerGuid)
            {
                UnityEngine.Debug.Log("[CombatDeathWatcher] Player muerto → NotifyCombatEnded(Defeat)");
                _signaller.NotifyCombatEnded(CombatOutcome.Defeat);
                return;
            }

            EventManager.Trigger(EventName.OnEntityDestroyed,
                payload.TargetGuid, payload.SourceGuid);

            _turnOrder.Remove(payload.TargetGuid);
            _visuals?.Despawn(payload.TargetGuid);
            _grid?.Unregister(payload.TargetGuid);

            var room = _dungeon.CurrentRoomInstance;
            int remaining = room?.SpawnedEnemies?.Count ?? -1;
            UnityEngine.Debug.Log($"[CombatDeathWatcher] Enemy muerto — room.State={room?.State} SpawnedEnemies.Count={remaining}");
            if (room != null
                && room.State == RoomState.Uncleared
                && room.SpawnedEnemies.Count == 0)
            {
                UnityEngine.Debug.Log("[CombatDeathWatcher] Sin enemigos → NotifyCombatEnded(Victory)");
                _signaller.NotifyCombatEnded(CombatOutcome.Victory);
            }
        }
    }
}
