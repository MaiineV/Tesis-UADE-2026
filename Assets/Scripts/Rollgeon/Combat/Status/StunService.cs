using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Implementación POCO de <see cref="IStunService"/>. Calca el lifecycle de
    /// <c>DiceBlockService</c>: registro global vía <see cref="IPreloadableService"/>, estado
    /// combat-scoped y limpieza por eventos de fin de combate / fin de run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sin acople al disparador.</b> No escucha eventos de hazards ni de daño — el glue que
    /// traduce "el jugador pisó hielo" en <see cref="ApplyStun"/> vive en el subsistema de
    /// hazards. Acá solo existe el contador.
    /// </para>
    /// <para>
    /// <b>Consumo del turno.</b> <see cref="ConsumeTurn"/> es el único decremento. Lo llama el
    /// skip de turno (<see cref="StunTurnSkipper"/>) al saltear el turno que se pierde. A
    /// propósito NO se decrementa en <c>OnTurnFinished</c>: ese evento también sale en los
    /// turnos que el jugador juega normalmente, y el stun duraría menos de lo aplicado.
    /// </para>
    /// </remarks>
    public sealed class StunService : IStunService, IPreloadableService, IDisposable
    {
        // Lazy por la misma razón que ComboBlockService/DiceBlockService: si Odin deserializa
        // este servicio desde una lista polimórfica bypassea el ctor y el dict queda null.
        private Dictionary<Guid, int> _remainingTurns;
        private Dictionary<Guid, int> RemainingTurns
            => _remainingTurns ??= new Dictionary<Guid, int>();

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>
        /// Resolver del PlayerGuid. Hoy solo se usa para diagnóstico
        /// (<see cref="IsPlayerStunned"/>); el filtrado por entidad lo hace el caller. Se
        /// mantiene para paridad con el resto de los servicios y para el hook de tests.
        /// </summary>
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
            // También por tipo concreto: los bootstraps de hazards/bosses que quieran el POCO
            // sin pasar por la interfaz lo resuelven directo (mismo criterio que TurnOrderService).
            ServiceLocator.AddService<StunService>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests — suscribe handlers e inyecta el resolver del player guid.</summary>
        public void ConfigureForTests(Func<Guid> playerGuidResolver)
        {
            _playerGuidResolver = playerGuidResolver ?? DefaultPlayerGuidResolver;
            SubscribeHandlers();
        }

        private void SubscribeHandlers()
        {
            // Idempotencia: si Register y ConfigureForTests corren sobre la misma instancia,
            // desuscribimos antes de re-suscribir para no duplicar el ClearAll.
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

            // Dispara siempre, incluso si el max() no movió el contador — el feedback
            // ("te volvió a pegar el hielo") es del disparo, no del delta.
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

        /// <summary>Conveniencia para debug/UI: <c>IsStunned(playerGuid)</c>.</summary>
        public bool IsPlayerStunned() => IsStunned(ResolvePlayerGuid());

        /// <summary>Entidades con stun activo (vista read-only para debug / tests).</summary>
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
