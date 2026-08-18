using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Implementación POCO de <see cref="IStunService"/>: registro global vía
    /// <see cref="IPreloadableService"/>, estado combat-scoped. Sólo el contador — el glue que
    /// traduce "pisó hielo" en <see cref="ApplyStun"/> vive en el subsistema de hazards.
    /// </summary>
    /// <remarks>
    /// <see cref="ConsumeTurn"/> es el único decremento y lo llama <see cref="StunTurnSkipper"/> al
    /// saltear el turno perdido. NO se decrementa en <c>OnTurnFinished</c>: ese evento también sale
    /// en los turnos que el jugador juega normalmente, y el stun duraría menos de lo aplicado.
    /// </remarks>
    public sealed class StunService : IStunService, IPreloadableService, IDisposable
    {
        // Lazy: si Odin deserializa este servicio desde una lista polimórfica bypassea el ctor y el
        // dict queda null.
        private Dictionary<Guid, int> _remainingTurns;
        private Dictionary<Guid, int> RemainingTurns
            => _remainingTurns ??= new Dictionary<Guid, int>();

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Sólo para diagnóstico: el filtrado por entidad lo hace el caller.</summary>
        private Func<Guid> _playerGuidResolver;

        /// <summary>Después de core services, antes de behaviors — igual que DiceBlockService.</summary>
        public int Priority => 80;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _playerGuidResolver = DefaultPlayerGuidResolver;
            SubscribeHandlers();

            ServiceLocator.AddService<IStunService>(this, ServiceScope.Global);
            // También por tipo concreto: los bootstraps que quieran el POCO sin pasar por la
            // interfaz lo resuelven directo.
            ServiceLocator.AddService<StunService>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests.</summary>
        public void ConfigureForTests(Func<Guid> playerGuidResolver)
        {
            _playerGuidResolver = playerGuidResolver ?? DefaultPlayerGuidResolver;
            SubscribeHandlers();
        }

        private void SubscribeHandlers()
        {
            // Idempotencia: Register y ConfigureForTests pueden correr sobre la misma instancia, y
            // re-suscribir sin desuscribir duplicaría el ClearAll.
            UnsubscribeHandlers();

            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        private void UnsubscribeHandlers()
        {
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }
        }

        public void Dispose()
        {
            UnsubscribeHandlers();
            RemainingTurns.Clear();
        }

        // ======================================================================
        // IStunService
        // ======================================================================

        /// <inheritdoc />
        public void ApplyStun(Guid entity, int turns = 1)
        {
            if (entity == Guid.Empty) return;
            if (turns <= 0) return;

            var map = RemainingTurns;
            int current = map.TryGetValue(entity, out var n) ? n : 0;
            // max(), no suma: dos disparos del mismo hazard no encadenan turnos perdidos.
            int applied = turns > current ? turns : current;
            map[entity] = applied;

            // Dispara aunque el max() no haya movido el contador: el feedback es del disparo, no
            // del delta.
            EventManager.Trigger(EventName.OnStunApplied, entity, applied);
        }

        /// <inheritdoc />
        public bool IsStunned(Guid entity)
        {
            if (entity == Guid.Empty) return false;
            return RemainingTurns.TryGetValue(entity, out var n) && n > 0;
        }

        /// <inheritdoc />
        public int GetStunTurns(Guid entity)
        {
            if (entity == Guid.Empty) return 0;
            return RemainingTurns.TryGetValue(entity, out var n) && n > 0 ? n : 0;
        }

        /// <inheritdoc />
        public bool ConsumeTurn(Guid entity)
        {
            if (entity == Guid.Empty) return false;

            var map = RemainingTurns;
            if (!map.TryGetValue(entity, out var remaining) || remaining <= 0)
            {
                // Defensivo: una entry en 0 no debería existir (siempre se borra al expirar).
                map.Remove(entity);
                return false;
            }

            remaining--;
            if (remaining <= 0)
            {
                map.Remove(entity);
                EventManager.Trigger(EventName.OnStunExpired, entity);
            }
            else
            {
                map[entity] = remaining;
            }

            return true;
        }

        /// <inheritdoc />
        public void Clear(Guid entity)
        {
            if (entity == Guid.Empty) return;
            if (!RemainingTurns.Remove(entity)) return;
            EventManager.Trigger(EventName.OnStunExpired, entity);
        }

        /// <inheritdoc />
        public void ClearAll()
        {
            RemainingTurns.Clear();
        }

        // ======================================================================
        // Diagnóstico
        // ======================================================================

        public bool IsPlayerStunned() => IsStunned(ResolvePlayerGuid());

        /// <summary>Vista read-only de las entidades con stun activo.</summary>
        public IReadOnlyDictionary<Guid, int> ActiveStuns => RemainingTurns;

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnScopeEndedExternal(params object[] args) => ClearAll();

        private Guid ResolvePlayerGuid()
            => _playerGuidResolver != null ? _playerGuidResolver() : Guid.Empty;

        private static Guid DefaultPlayerGuidResolver()
        {
            if (ServiceLocator.TryGetService<IPlayerService>(out var svc) && svc != null)
                return svc.PlayerGuid;
            return Guid.Empty;
        }
    }
}
