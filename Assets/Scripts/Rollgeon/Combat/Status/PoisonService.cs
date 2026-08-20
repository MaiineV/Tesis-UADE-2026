using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Implementación POCO de <see cref="IPoisonService"/>, espejo estructural de
    /// <see cref="StunService"/>: registro global, estado combat-scoped, y el glue que
    /// traduce "pisó Veneno" en <see cref="ApplyPoison"/> vive en el handler de tiles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tick al inicio del turno del envenenado</b> (<c>OnTurnStarted</c>): consistente con
    /// el tick de Fuego y garantiza N ticks completos aunque el envenenamiento ocurra en
    /// turno ajeno (empuje). El daño va por <see cref="IDamagePipeline"/> con
    /// <see cref="AttackKind.DamageOverTime"/>.
    /// </para>
    /// <para>
    /// <b>Refresh, no stack:</b> re-aplicar toma <c>max(restante, nuevo)</c> y pisa
    /// daño/fuente con los últimos — el GDD prohíbe acumular niveles o turnos.
    /// </para>
    /// </remarks>
    public sealed class PoisonService : IPoisonService, IPreloadableService, IDisposable
    {
        private sealed class PoisonState
        {
            public int DamagePerTurn;
            public int RemainingTurns;
            public Guid SourceId;
        }

        // Lazy: Odin puede bypassear el ctor al deserializar desde listas polimórficas.
        private Dictionary<Guid, PoisonState> _states;
        private Dictionary<Guid, PoisonState> States
            => _states ??= new Dictionary<Guid, PoisonState>();

        private EventManager.EventReceiver _onTurnStartedHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Junto a StunService (80).</summary>
        public int Priority => 80;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            SubscribeHandlers();

            ServiceLocator.AddService<IPoisonService>(this, ServiceScope.Global);
            ServiceLocator.AddService<PoisonService>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests: arma las suscripciones sin ServiceLocator.</summary>
        public void ConfigureForTests() => SubscribeHandlers();

        private void SubscribeHandlers()
        {
            UnsubscribeHandlers();

            _onTurnStartedHandler = OnTurnStartedExternal;
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        private void UnsubscribeHandlers()
        {
            if (_onTurnStartedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
                _onTurnStartedHandler = null;
            }
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
            States.Clear();
        }

        // ======================================================================
        // IPoisonService
        // ======================================================================

        /// <inheritdoc />
        public void ApplyPoison(Guid entity, int damagePerTurn, int turns, Guid sourceId)
        {
            if (entity == Guid.Empty) return;
            if (damagePerTurn <= 0 || turns <= 0) return;

            var map = States;
            if (!map.TryGetValue(entity, out var state))
            {
                state = new PoisonState();
                map[entity] = state;
            }

            // Refresh, no stack: max() en turnos (re-pisar veneno con 1 restante vuelve a 3),
            // y el daño/fuente quedan los del último aplicador — nunca se suman.
            state.RemainingTurns = Math.Max(state.RemainingTurns, turns);
            state.DamagePerTurn = damagePerTurn;
            state.SourceId = sourceId;

            EventManager.Trigger(EventName.OnPoisonApplied, entity, state.RemainingTurns);
        }

        /// <inheritdoc />
        public bool IsPoisoned(Guid entity)
        {
            if (entity == Guid.Empty) return false;
            return States.TryGetValue(entity, out var s) && s.RemainingTurns > 0;
        }

        /// <inheritdoc />
        public int GetPoisonTurns(Guid entity)
        {
            if (entity == Guid.Empty) return 0;
            return States.TryGetValue(entity, out var s) && s.RemainingTurns > 0 ? s.RemainingTurns : 0;
        }

        /// <inheritdoc />
        public void Clear(Guid entity)
        {
            if (entity == Guid.Empty) return;
            if (!States.Remove(entity)) return;
            EventManager.Trigger(EventName.OnPoisonExpired, entity);
        }

        /// <inheritdoc />
        public void ClearAll() => States.Clear();

        /// <summary>Daño por tick vigente — el pathing IA lo usa para el costo condicional del Veneno.</summary>
        public int GetDamagePerTurn(Guid entity)
            => States.TryGetValue(entity, out var s) && s.RemainingTurns > 0 ? s.DamagePerTurn : 0;

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnTurnStartedExternal(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid entity)) return;
            if (!States.TryGetValue(entity, out var state) || state.RemainingTurns <= 0) return;

            if (ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) && pipeline != null)
            {
                pipeline.Resolve(new DamageContext
                {
                    SourceId = state.SourceId,
                    TargetId = entity,
                    BaseDamage = state.DamagePerTurn,
                    Kind = AttackKind.DamageOverTime,
                });
            }

            state.RemainingTurns--;
            EventManager.Trigger(EventName.OnPoisonTicked, entity, state.DamagePerTurn, state.RemainingTurns);

            if (state.RemainingTurns > 0) return;
            States.Remove(entity);
            EventManager.Trigger(EventName.OnPoisonExpired, entity);
        }

        private void OnScopeEndedExternal(params object[] args) => ClearAll();
    }
}
