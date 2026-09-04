using System;
using Patterns;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Segundo Aliento corta la fase enemiga: cuando <c>OnSecondWindTriggered</c> salva al
    /// jugador mientras un enemigo actúa, pide al combate que al próximo <c>EnemyDone</c>
    /// el turno vuelva al jugador en vez de seguir con los enemigos que faltaban.
    /// </summary>
    /// <remarks>
    /// Mismo patrón que <see cref="StunTurnSkipper"/>: POCO con delegates, dueño el
    /// <c>CombatController</c>, y nunca toca el <c>TurnOrderService</c> a mano — solo
    /// levanta <c>CombatContext.EnemyPhaseCutRequested</c>; el estado de turno enemigo
    /// hace el resto por el input normal. El enemigo que pegó termina su acción (su
    /// feedback está en vuelo) y su propio <c>EnemyDone</c> es el que consume el corte.
    /// </remarks>
    public sealed class SecondWindPhaseCutter : IDisposable
    {
        private readonly Func<Guid> _playerIdResolver;
        private readonly Func<bool> _isEnemyTurn;
        private readonly Action _requestCut;

        private EventManager.EventReceiver _onSecondWindHandler;

        public int CutsRequested { get; private set; }

        public SecondWindPhaseCutter(Func<Guid> playerIdResolver, Func<bool> isEnemyTurn, Action requestCut)
        {
            _playerIdResolver = playerIdResolver;
            _isEnemyTurn = isEnemyTurn;
            _requestCut = requestCut;
        }

        public void Attach()
        {
            if (_onSecondWindHandler != null) return;
            _onSecondWindHandler = HandleSecondWind;
            EventManager.Subscribe(EventName.OnSecondWindTriggered, _onSecondWindHandler);
        }

        public void Dispose()
        {
            if (_onSecondWindHandler == null) return;
            EventManager.UnSubscribe(EventName.OnSecondWindTriggered, _onSecondWindHandler);
            _onSecondWindHandler = null;
        }

        // Schema OnSecondWindTriggered: [Guid playerGuid, ItemSO item, int remainingHp]
        private void HandleSecondWind(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid saved)) return;
            if (saved == Guid.Empty) return;
            if (_playerIdResolver == null || saved != _playerIdResolver()) return;
            // Fuera de la fase enemiga (ej. daño en el propio turno del jugador) no hay
            // nada que cortar; el flag quedaría colgado hasta el próximo EnemyDone.
            if (_isEnemyTurn == null || !_isEnemyTurn()) return;
            if (_requestCut == null) return;

            CutsRequested++;
            _requestCut();
        }
    }
}
