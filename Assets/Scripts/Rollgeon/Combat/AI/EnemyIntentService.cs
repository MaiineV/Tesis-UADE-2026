using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Player;

namespace Rollgeon.Combat.AI
{
    /// <inheritdoc cref="IEnemyIntentService" />
    public sealed class EnemyIntentService : IEnemyIntentService, IDisposable
    {
        private readonly IEnemyAIRegistry _registry;
        private readonly IPlayerService _players;

        // El contexto lo arma el handler y no este servicio: uno propio al que le faltara el
        // DamagePipeline le haría contestar a AINode_RangedShot.CanFire que no va a disparar
        // justo cuando sí va a disparar.
        private readonly Func<Guid, AIContext> _readContext;

        private EventManager.EventReceiver _onTurnStarted;
        private EventManager.EventReceiver _onCombatEnd;
        private Guid _actingGuid;

        public EnemyIntentService(IEnemyAIRegistry registry, IPlayerService players,
                                  Func<Guid, AIContext> readContext)
        {
            _registry = registry;
            _players = players;
            _readContext = readContext;

            _onTurnStarted = HandleTurnStarted;
            _onCombatEnd = HandleCombatEnd;
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStarted);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEnd);
        }

        /// <inheritdoc />
        public bool TryRead(Guid enemyId, List<AIIntent> standing, List<AIIntent> next)
        {
            standing?.Clear();
            next?.Clear();

            if (!IsPlayerTurn()) return false;
            if (enemyId == Guid.Empty || _registry == null || _readContext == null) return false;
            if (!_registry.TryGet(enemyId, out var root, out _) || root == null) return false;

            var context = _readContext(enemyId);
            if (context == null) return false;

            AIIntentWalker.Collect(root, context, standing, next);
            return true;
        }

        // Durante el turno del enemigo el índice de su ciclo ya avanzó y sus marcas están en
        // movimiento: lo que se lea ahí no es una predicción sino una foto a medio revelar.
        private bool IsPlayerTurn()
        {
            var playerGuid = _players?.PlayerGuid ?? Guid.Empty;
            return playerGuid != Guid.Empty && _actingGuid == playerGuid;
        }

        private void HandleTurnStarted(params object[] args)
        {
            if (args != null && args.Length > 0 && args[0] is Guid guid) _actingGuid = guid;
        }

        private void HandleCombatEnd(params object[] args) => _actingGuid = Guid.Empty;

        public void Dispose()
        {
            if (_onTurnStarted != null) EventManager.UnSubscribe(EventName.OnTurnStarted, _onTurnStarted);
            if (_onCombatEnd != null) EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEnd);
            _onTurnStarted = null;
            _onCombatEnd = null;
        }
    }
}
