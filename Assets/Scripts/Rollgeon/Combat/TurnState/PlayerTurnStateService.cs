using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Player;

namespace Rollgeon.Combat.TurnState
{
    /// <summary>
    /// Impl runtime de <see cref="IPlayerTurnStateService"/> (clase plana, patrón
    /// <c>RollPoolService</c>). Acumula tiles del turno desde
    /// <see cref="IMovementService.OnEntityMoved"/> y la racha de rondas limpias desde
    /// <c>DamageResolvedPayload</c>, con los bordes en <c>OnTurnStarted</c> /
    /// <c>OnCombatStart/End</c> / <c>OnRunStart</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reset DIFERIDO post-ataque.</b> Al jugarse un combo de ataque no se resetea el
    /// contador de tiles en el momento: este servicio se suscribe en bootstrap, ANTES de
    /// que los items binden sus handlers de ComboPlayed — un reset sincrónico dentro del
    /// mismo dispatch haría que <c>ReadTilesMovedThisTurn</c> del item lea 0. Se marca
    /// <c>_consumePending</c> y el contador se limpia recién en el próximo movimiento,
    /// lo que además implementa "solo el ataque que sigue al movimiento" para builds con
    /// doble ataque.
    /// </para>
    /// <para>
    /// <b>"Recibir daño" = perder vida.</b> Un golpe 100% absorbido por escudo
    /// (<c>FinalDamage == 0</c>) NO corta la racha de Furia — jugar defensivo es
    /// exactamente lo que el item premia. Si GD lo quiere al revés, es un solo if.
    /// </para>
    /// <para>
    /// <b>Sin ISaveable.</b> Todo el estado es turn/combat-scoped y los saves de run
    /// ocurren fuera de combate, con esto vacío por definición. Resume mid-combate
    /// (CombatResumeSnapshot) perdería la racha — anotado como follow-up, no bloqueante.
    /// </para>
    /// </remarks>
    public sealed class PlayerTurnStateService : IPlayerTurnStateService, IDisposable
    {
        private readonly IMovementService _movement;

        private EventManager.EventReceiver _onTurnStarted;
        private EventManager.EventReceiver _onCombatStart;
        private EventManager.EventReceiver _onCombatEnd;
        private EventManager.EventReceiver _onRunStart;
        private Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> _onEntityMoved;
        private Action<ComboPlayedPayload> _onComboPlayed;
        private Action<DamageResolvedPayload> _onDamageResolved;

        private bool _inCombat;
        private int _tilesMovedThisTurn;
        private bool _consumePending;
        private int _cleanTurnStreak;
        private bool _damagedThisRound;
        private bool _roundOpen;

        public int TilesMovedThisTurn => _tilesMovedThisTurn;
        public int CleanTurnStreak => _cleanTurnStreak;

        public PlayerTurnStateService(IMovementService movement)
        {
            _movement = movement;
            Subscribe();
        }

        public void Dispose()
        {
            if (_onEntityMoved != null && _movement != null)
                _movement.OnEntityMoved -= _onEntityMoved;
            if (_onComboPlayed != null) TypedEvent<ComboPlayedPayload>.Unsubscribe(_onComboPlayed);
            if (_onDamageResolved != null) TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
            if (_onTurnStarted != null) EventManager.UnSubscribe(EventName.OnTurnStarted, _onTurnStarted);
            if (_onCombatStart != null) EventManager.UnSubscribe(EventName.OnCombatStart, _onCombatStart);
            if (_onCombatEnd != null) EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEnd);
            if (_onRunStart != null) EventManager.UnSubscribe(EventName.OnRunStart, _onRunStart);
            _onEntityMoved = null;
            _onComboPlayed = null;
            _onDamageResolved = null;
            _onTurnStarted = _onCombatStart = _onCombatEnd = _onRunStart = null;
        }

        private void Subscribe()
        {
            _onEntityMoved = HandleEntityMoved;
            if (_movement != null) _movement.OnEntityMoved += _onEntityMoved;

            _onComboPlayed = HandleComboPlayed;
            TypedEvent<ComboPlayedPayload>.Subscribe(_onComboPlayed);

            _onDamageResolved = HandleDamageResolved;
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);

            _onTurnStarted = HandleTurnStarted;
            _onCombatStart = HandleCombatStart;
            _onCombatEnd = HandleCombatEnd;
            _onRunStart = HandleCombatEnd; // mismo reset total, defensivo
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStarted);
            EventManager.Subscribe(EventName.OnCombatStart, _onCombatStart);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEnd);
            EventManager.Subscribe(EventName.OnRunStart, _onRunStart);
        }

        // ---- handlers ----------------------------------------------------------

        private void HandleEntityMoved(Guid entity, GridCoord from, GridCoord to,
            IReadOnlyList<GridCoord> path)
        {
            if (!_inCombat || entity != GetPlayerGuid()) return;

            if (_consumePending)
            {
                _tilesMovedThisTurn = 0;
                _consumePending = false;
            }
            // path incluye origen y destino ⇒ tiles pisadas = count - 1 (invariante
            // documentada en IMovementPathFilter: el path ya viene truncado/efectivo).
            if (path != null && path.Count > 1)
                _tilesMovedThisTurn += path.Count - 1;
        }

        private void HandleComboPlayed(ComboPlayedPayload payload)
        {
            if (!_inCombat) return;
            if (payload.SourceGuid != GetPlayerGuid()) return;
            if (payload.ActionKind != RollActionKind.Attack) return;
            _consumePending = true;
        }

        private void HandleDamageResolved(DamageResolvedPayload payload)
        {
            if (!_inCombat) return;
            if (payload.TargetGuid != GetPlayerGuid()) return;
            if (payload.FinalDamage <= 0) return;
            bool changed = _cleanTurnStreak != 0;
            _cleanTurnStreak = 0;
            _damagedThisRound = true;
            if (changed) EmitStreakChanged();
        }

        private void HandleTurnStarted(params object[] args)
        {
            if (!_inCombat) return;
            if (args == null || args.Length == 0 || !(args[0] is Guid g) || g != GetPlayerGuid()) return;

            // El turno del player como límite de ronda cubre los turnos enemigos
            // intermedios (donde cae el daño). La primera ronda del combate no suma.
            if (_roundOpen && !_damagedThisRound)
            {
                _cleanTurnStreak++;
                EmitStreakChanged();
            }
            _roundOpen = true;
            _damagedThisRound = false;
            _tilesMovedThisTurn = 0;
            _consumePending = false;
        }

        // Solo en cambios reales — los suscriptores (UI de daño base con Furia) re-leen
        // el override; spamearlo en cada turno sin cambio sería ruido para nada.
        private void EmitStreakChanged()
            => EventManager.Trigger(EventName.OnCleanTurnStreakChanged, GetPlayerGuid(), _cleanTurnStreak);

        private void HandleCombatStart(params object[] args)
        {
            ResetAll();
            _inCombat = true;
        }

        private void HandleCombatEnd(params object[] args)
        {
            ResetAll();
        }

        private void ResetAll()
        {
            // Emite solo si el reset ocurre DENTRO de combate con racha viva (no en el
            // teardown de cada combate sin racha, ni en OnRunStart).
            bool emit = _inCombat && _cleanTurnStreak != 0;
            _inCombat = false;
            _tilesMovedThisTurn = 0;
            _consumePending = false;
            _cleanTurnStreak = 0;
            _damagedThisRound = false;
            _roundOpen = false;
            if (emit) EmitStreakChanged();
        }

        private static Guid GetPlayerGuid()
            => ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null
                ? ps.PlayerGuid
                : Guid.Empty;
    }
}
