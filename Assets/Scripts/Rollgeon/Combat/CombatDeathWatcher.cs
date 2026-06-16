using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Patterns;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat
{
    public sealed class CombatDeathWatcher : ICombatDeathWatcher
    {
        // Tiempo que esperamos antes de despawnear visualmente al enemigo + notificar
        // Victory en el kill final. Originalmente le daba chance a los floating numbers
        // de aparecer antes de que el HUD se desmonte, pero esa pausa entre salas de
        // combate no aportaba, así que está en 0 → el combate termina instantáneamente.
        // Subir este valor reactiva el delay (y vuelve a pasar por la coroutine).
        private const float DeathAnimationDelaySeconds = 0f;

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
            if (!payload.WasLethal) return;
            if (!_processed.Add(payload.TargetGuid)) return;

            if (payload.TargetGuid == _player.PlayerGuid)
            {
                _signaller.NotifyCombatEnded(CombatOutcome.Defeat);
                return;
            }

            // OnEntityDestroyed se dispara YA — los listeners deben saberlo de inmediato
            // (gold drop, achievements). DungeonManager también lo escucha y remueve al
            // enemigo de room.SpawnedEnemies, que es lo que chequeamos abajo para Victory.
            EventManager.Trigger(EventName.OnEntityDestroyed,
                payload.TargetGuid, payload.SourceGuid);

            _turnOrder.Remove(payload.TargetGuid);

            var deadGuid = payload.TargetGuid;
            var room = _dungeon.CurrentRoomInstance;
            bool isFinalKill = room != null
                && room.State == RoomState.Uncleared
                && room.SpawnedEnemies.Count == 0;

            if (isFinalKill && Application.isPlaying && DeathAnimationDelaySeconds > 0f)
            {
                // Solo delayamos el despawn + Victory en el kill FINAL, y únicamente si el
                // delay está configurado > 0. Con delay en 0 (default actual) caemos al
                // path inmediato de abajo → el combate termina al instante, sin esperar.
                CoroutineHost.Run(DelayedFinishCombat(deadGuid));
            }
            else
            {
                _visuals?.Despawn(deadGuid);
                _grid?.Unregister(deadGuid);

                if (isFinalKill)
                    _signaller.NotifyCombatEnded(CombatOutcome.Victory);
            }
        }

        private IEnumerator DelayedFinishCombat(Guid deadGuid)
        {
            yield return new WaitForSeconds(DeathAnimationDelaySeconds);
            _visuals?.Despawn(deadGuid);
            _grid?.Unregister(deadGuid);
            _signaller.NotifyCombatEnded(CombatOutcome.Victory);
        }
    }
}
