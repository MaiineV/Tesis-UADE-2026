using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Implementación POCO de <see cref="ITeleportCooldownService"/>, espejo estructural de
    /// <see cref="PoisonService"/>: registro global. El glue que traduce "usó un portal" en
    /// <see cref="Apply"/> vive en <c>SpecialTileService</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos relojes según el contexto</b>: en combate tickea al inicio del turno del
    /// afectado (criterio Veneno). En exploración no hay turnos, así que tickea por
    /// MOVIMIENTO completado del afectado (hook lazy a <see cref="IMovementService"/>,
    /// mismo patrón run-scoped-vs-global que <c>SpecialTileService</c>). Sin
    /// <c>IPhaseService</c> registrado se asume combate — los fixtures de EditMode corren
    /// sin fases y esperan el reloj de turnos.
    /// </para>
    /// <para>
    /// <b>ClearAll también en <c>OnRoomEntered</c>:</b> los portales son de la sala;
    /// cambiar de sala es el hard-reset natural (mismo criterio que las casillas).
    /// </para>
    /// </remarks>
    public sealed class TeleportCooldownService : ITeleportCooldownService, IPreloadableService, IDisposable
    {
        // Lazy: Odin puede bypassear el ctor al deserializar desde listas polimórficas.
        private Dictionary<Guid, int> _turns;
        private Dictionary<Guid, int> Turns => _turns ??= new Dictionary<Guid, int>();

        // Frame en el que se aplicó el cooldown: la reubicación post-teleport commitea un
        // path EN EL MISMO frame que el Apply y dispara OnEntityMoved — ese "movimiento"
        // no es una acción del jugador y no debe consumir un tick.
        private Dictionary<Guid, int> _appliedFrame;
        private Dictionary<Guid, int> AppliedFrame => _appliedFrame ??= new Dictionary<Guid, int>();

        private EventManager.EventReceiver _onTurnStartedHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;
        private EventManager.EventReceiver _onRoomEnteredHandler;

        // La instancia exacta a la que nos suscribimos: es run-scoped y este servicio es
        // Global — re-resolver al desuscribir podría devolver una más nueva y leakear la
        // vieja (gotcha _movementSubscribedTo de SpecialTileService).
        private IMovementService _movementSubscribedTo;

        /// <summary>Junto a PoisonService/StunService (80).</summary>
        public int Priority => 80;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            SubscribeHandlers();

            ServiceLocator.AddService<ITeleportCooldownService>(this, ServiceScope.Global);
            ServiceLocator.AddService<TeleportCooldownService>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests: arma las suscripciones sin ServiceLocator.</summary>
        public void ConfigureForTests() => SubscribeHandlers();

        private void SubscribeHandlers()
        {
            UnsubscribeHandlers();

            _onTurnStartedHandler = OnTurnStartedExternal;
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;
            _onRoomEnteredHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
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
            if (_onRoomEnteredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
                _onRoomEnteredHandler = null;
            }
        }

        public void Dispose()
        {
            UnsubscribeHandlers();
            UnsubscribeMovement();
            Turns.Clear();
            AppliedFrame.Clear();
        }

        // ======================================================================
        // Hook de movimiento (reloj de exploración)
        // ======================================================================

        private void EnsureMovementSubscription()
        {
            if (_movementSubscribedTo != null) return;
            if (!ServiceLocator.TryGetService<IMovementService>(out var movement) || movement == null) return;

            movement.OnEntityMoved += OnEntityMovedExternal;
            _movementSubscribedTo = movement;
        }

        private void UnsubscribeMovement()
        {
            if (_movementSubscribedTo == null) return;
            _movementSubscribedTo.OnEntityMoved -= OnEntityMovedExternal;
            _movementSubscribedTo = null;
        }

        private void OnEntityMovedExternal(Guid entity, GridCoord from, GridCoord to,
            IReadOnlyList<GridCoord> path)
        {
            // En combate el reloj es el turno — un tick extra por caminar duplicaría el
            // descuento del mismo turno.
            if (IsCombatContext()) return;
            if (!Turns.TryGetValue(entity, out var remaining) || remaining <= 0) return;

            // El mismo frame del Apply es la reubicación del motor de cadenas, no un paso
            // del jugador.
            if (AppliedFrame.TryGetValue(entity, out var frame)
                && frame == UnityEngine.Time.frameCount)
            {
                return;
            }

            Tick(entity, remaining);
        }

        /// <summary>Sin IPhaseService (tests) se asume combate: el reloj es OnTurnStarted.</summary>
        private static bool IsCombatContext()
        {
            if (ServiceLocator.TryGetService<Rollgeon.Phase.IPhaseService>(out var phase) && phase != null)
                return phase.CurrentBase == Rollgeon.Phase.GamePhase.Combat;
            return true;
        }

        // ======================================================================
        // ITeleportCooldownService
        // ======================================================================

        /// <inheritdoc />
        public void Apply(Guid entity, int turns)
        {
            if (entity == Guid.Empty || turns <= 0) return;

            Turns.TryGetValue(entity, out var current);
            var total = Math.Max(current, turns);
            Turns[entity] = total;
            AppliedFrame[entity] = UnityEngine.Time.frameCount;
            EnsureMovementSubscription();

            EventManager.Trigger(EventName.OnTeleportCooldownApplied, entity, total);
        }

        /// <inheritdoc />
        public bool IsOnCooldown(Guid entity)
        {
            if (entity == Guid.Empty) return false;
            return Turns.TryGetValue(entity, out var t) && t > 0;
        }

        /// <inheritdoc />
        public int GetTurns(Guid entity)
        {
            if (entity == Guid.Empty) return 0;
            return Turns.TryGetValue(entity, out var t) && t > 0 ? t : 0;
        }

        /// <inheritdoc />
        public void Clear(Guid entity)
        {
            if (entity == Guid.Empty) return;
            AppliedFrame.Remove(entity);
            if (!Turns.Remove(entity)) return;
            EventManager.Trigger(EventName.OnTeleportCooldownExpired, entity);
        }

        /// <inheritdoc />
        public void ClearAll()
        {
            Turns.Clear();
            AppliedFrame.Clear();
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnTurnStartedExternal(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid entity)) return;
            if (!Turns.TryGetValue(entity, out var remaining) || remaining <= 0) return;

            Tick(entity, remaining);
        }

        private void Tick(Guid entity, int remaining)
        {
            remaining--;
            if (remaining > 0)
            {
                Turns[entity] = remaining;
                EventManager.Trigger(EventName.OnTeleportCooldownTicked, entity, remaining);
                return;
            }

            Turns.Remove(entity);
            AppliedFrame.Remove(entity);
            EventManager.Trigger(EventName.OnTeleportCooldownTicked, entity, 0);
            EventManager.Trigger(EventName.OnTeleportCooldownExpired, entity);
        }

        private void OnScopeEndedExternal(params object[] args)
        {
            ClearAll();
            // La instancia de movimiento es run-scoped: soltarla acá evita quedar colgados
            // de una muerta; el próximo Apply re-hookea la vigente.
            UnsubscribeMovement();
        }
    }
}
