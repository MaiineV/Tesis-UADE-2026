using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
using Rollgeon.Entities.Visuals;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Rollgeon.Player;

namespace Rollgeon.Combat
{
    public sealed class CombatDeathWatcher : ICombatDeathWatcher
    {
        /// <summary>
        /// Secuencia autorada en el <c>FeedbackDBSO</c> que corre al morir cualquier enemigo
        /// (scale-down + spin + VFX + SFX + Feel). Único punto de acoplamiento entre la
        /// muerte y el autoral: agregar un enemigo nuevo no requiere tocar nada de esto.
        /// </summary>
        public const string DeathSequenceId = "death.enemy";

        private readonly IPlayerService _player;
        private readonly ICombatSignaller _signaller;
        private readonly TurnOrderService _turnOrder;
        private readonly IEntityVisualService _visuals;
        private readonly IDungeonService _dungeon;
        private readonly IGridManager _grid;
        private readonly IEnemyAIRegistry _aiRegistry;
        private readonly IFeedbackService _feedback;
        private readonly TurnManager _turn;

        private readonly HashSet<Guid> _processed = new();
        private Action<DamageResolvedPayload> _handler;
        private bool _disposed;

        public CombatDeathWatcher(
            IPlayerService player,
            ICombatSignaller signaller,
            TurnOrderService turnOrder,
            IEntityVisualService visuals,
            IDungeonService dungeon,
            IGridManager grid = null,
            IEnemyAIRegistry aiRegistry = null,
            IFeedbackService feedback = null,
            TurnManager turn = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _signaller = signaller ?? throw new ArgumentNullException(nameof(signaller));
            _turnOrder = turnOrder ?? throw new ArgumentNullException(nameof(turnOrder));
            _visuals = visuals;
            _dungeon = dungeon ?? throw new ArgumentNullException(nameof(dungeon));
            _grid = grid;
            _aiRegistry = aiRegistry;
            _feedback = feedback;
            _turn = turn;

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
            ServiceLocator.TryGetService<IEnemyAIRegistry>(out var aiRegistry);
            ServiceLocator.TryGetService<IFeedbackService>(out var feedback);
            ServiceLocator.TryGetService<TurnManager>(out var turn);

            var watcher = new CombatDeathWatcher(
                player, signaller, turnOrder, visuals, dungeon, grid, aiRegistry, feedback, turn);
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

            var deadGuid = payload.TargetGuid;

            // La lógica lo entierra YA: sale del turn order, del grid y del registry de AI
            // aunque su pawn siga en pantalla desvaneciéndose. Si esto esperara al feedback,
            // el cadáver seguiría siendo targeteable y podría llegar a jugar su turno.
            _turnOrder.Remove(deadGuid);
            _grid?.Unregister(deadGuid);
            _aiRegistry?.Unregister(deadGuid);

            var room = _dungeon.CurrentRoomInstance;
            bool isFinalKill = room != null
                && room.State == RoomState.Uncleared
                && room.SpawnedEnemies.Count == 0;

            // El boss es el único enemigo trackeado en SpawnedEnemies; los refuerzos que spawnea
            // mid-combate (AINode_SpawnReinforcements) viven en el turn order / grid / aiRegistry
            // y como pawns, pero NUNCA se agregan a SpawnedEnemies. Por eso matar al boss deja
            // SpawnedEnemies vacío ⇒ isFinalKill=true y se dispara Victory con los refuerzos aún
            // vivos: quedaban como pawns huérfanos (nadie los despawnea) y, al seguir en la cola
            // en curso, alcanzaban a actuar durante la animación de muerte del boss — daño en un
            // combate ya ganado y estado colgado cuando CombatExitState hace TurnOrder.Reset().
            // Decisión de diseño (lockeada): al morir el boss se despawnean los refuerzos
            // restantes y el combate cierra limpio como Victory. Se hace YA (no diferido) para
            // que ningún refuerzo llegue a actuar entre el golpe letal y la Victory.
            if (isFinalKill)
                DespawnRemainingCombatants(deadGuid);

            // Token del combate en curso. El cierre por Victory se difiere hasta que termina
            // la animación de muerte (callback abajo), y esa coroutine del FeedbackManager no
            // se cancela. Si el combate se cerró por otra vía (Defeat simultáneo, EffForceDoor)
            // y el callback llega tarde —ya en OTRO combate en otra sala— cerrar acá dispararía
            // un OnCombatEnd espurio sobre la FSM equivocada y el ScreenManager sobre-popearía
            // la exploración (HUD colgado). Comparamos la sala vigente en FinishDeath.
            var combatRoomId = room?.InstanceId ?? Guid.Empty;

            // Sin feedback service (EditMode tests, escenas sin bootstrap) no hay animación
            // que esperar: el enemigo se va de un frame al otro, como antes.
            if (_feedback == null)
            {
                FinishDeath(deadGuid, isFinalKill, combatRoomId);
                return;
            }

            // Despawn y Victory quedan diferidos hasta que la secuencia termina — eso es lo
            // que hace que la muerte se VEA. El BeginFeedbackWait, además, frena los turnos
            // de los enemigos (los únicos que respetan el gate) si el player mata y cierra
            // turno de inmediato.
            _turn?.BeginFeedbackWait();
            var request = new FeedbackRequest
            {
                FeedbackId = DeathSequenceId,
                IsSequence = true,
                SourceGuid = payload.SourceGuid,
                TargetGuid = deadGuid,
            };
            _feedback.RequestFeedbackBlocking(request, () =>
            {
                _turn?.OnFeedbackComplete();
                FinishDeath(deadGuid, isFinalKill, combatRoomId);
            });
        }

        /// <summary>
        /// Despawnea a todos los combatientes vivos que aún quedan en el turn order y que no son
        /// el jugador (típicamente los refuerzos del boss). Espeja EXACTAMENTE el entierro de una
        /// muerte normal (sale del turn order, del grid, del registry de AI y su pawn se despawnea)
        /// pero sin la secuencia de muerte ni el drop: no fue una kill, es una limpieza de fin de
        /// combate. Los marca en <see cref="_processed"/> para descartar cualquier evento letal
        /// tardío contra un refuerzo ya despawneado.
        /// </summary>
        private void DespawnRemainingCombatants(Guid justDied)
        {
            // Snapshot: Remove muta la cola viva del servicio.
            var remaining = new List<Guid>(_turnOrder.OrderForRound);
            foreach (var guid in remaining)
            {
                if (guid == Guid.Empty || guid == justDied || guid == _player.PlayerGuid) continue;

                _processed.Add(guid);
                _turnOrder.Remove(guid);
                _grid?.Unregister(guid);
                _aiRegistry?.Unregister(guid);
                _visuals?.Despawn(guid);
            }
        }

        private void FinishDeath(Guid deadGuid, bool isFinalKill, Guid combatRoomId)
        {
            _visuals?.Despawn(deadGuid);
            if (!isFinalKill) return;

            // Descarta el cierre si el combate ya cambió de sala entre el golpe letal y este
            // callback diferido: sería un OnCombatEnd contra un combate distinto (ver arriba).
            var current = _dungeon.CurrentRoomInstance;
            if (current == null || current.InstanceId != combatRoomId) return;

            _signaller.NotifyCombatEnded(CombatOutcome.Victory);
        }
    }
}
