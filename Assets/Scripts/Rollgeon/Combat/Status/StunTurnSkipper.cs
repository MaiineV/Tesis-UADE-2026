using System;
using Patterns;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Glue entre <see cref="IStunService"/> y la FSM de combate: escucha
    /// <see cref="EventName.OnTurnStarted"/> y, si el actor está stuneado, consume 1 turno de
    /// stun y cierra el turno inmediatamente.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>POCO a propósito.</b> El dueño es el <c>CombatController</c>, pero la lógica vive acá
    /// (delegates en vez de refs al MonoBehaviour) para poder testearla en EditMode sin montar
    /// escena ni <c>ServiceBootstrapSO</c>.
    /// </para>
    /// <para>
    /// <b>Por qué cerrar el turno por el input normal.</b> Se sale por <c>PlayerEndTurn</c> /
    /// <c>EnemyDone</c> en vez de tocar el <c>TurnOrderService</c>: el camino normal garantiza
    /// que <c>OnTurnFinished</c> salga con el guid correcto (energía, modificadores y el botón
    /// de End Turn se re-deshabilitan solos) y que <c>Advance()</c> corra una sola vez.
    /// <c>StateMachine.SendInput</c> encola cuando se lo llama desde dentro de un
    /// <c>Enter</c>, así que el input se drena recién cuando la transición actual terminó.
    /// </para>
    /// <para>
    /// <b>Enemigos.</b> El mismo subscriber cubre cualquier guid. Para el enemigo funciona
    /// porque <c>EnemyTurnState</c> difiere la invocación del <c>EnemyActionHandler</c> al
    /// grace period (CNF-006): el <c>EnemyDone</c> encolado dispara <c>Exit</c> antes de que el
    /// countdown llegue a 0, y el handler nunca corre. Si el contexto se construye sin
    /// <c>DeltaTimeProvider</c> (path legacy síncrono, usado por tests viejos), el handler ya
    /// corrió durante <c>Enter</c> y el "skip" solo termina el turno después de que el enemigo
    /// actuó. Hoy ningún sistema stunea enemigos, así que el caso no se da en runtime.
    /// </para>
    /// </remarks>
    public sealed class StunTurnSkipper : IDisposable
    {
        private readonly Func<IStunService> _stunResolver;
        private readonly Func<Guid> _playerIdResolver;
        private readonly Action _endPlayerTurn;
        private readonly Action _endEnemyTurn;

        private EventManager.EventReceiver _onTurnStartedHandler;

        /// <summary>Diagnóstico: cuántos turnos salteó desde que se enganchó.</summary>
        public int SkipsPerformed { get; private set; }

        /// <param name="stunResolver">
        /// Resolver lazy del servicio. Lazy y no una ref fija porque el bootstrap del
        /// <see cref="StunService"/> puede correr después del <c>Awake</c> del dueño; si
        /// devuelve <c>null</c> el skipper queda inerte y el flujo de turnos no cambia.
        /// </param>
        /// <param name="playerIdResolver">Guid del player del combate en curso.</param>
        /// <param name="endPlayerTurn">Cierra el turno del player (<c>PlayerEndTurn</c>).</param>
        /// <param name="endEnemyTurn">Cierra el turno del enemy (<c>EnemyDone</c>). Puede ser null.</param>
        public StunTurnSkipper(
            Func<IStunService> stunResolver,
            Func<Guid> playerIdResolver,
            Action endPlayerTurn,
            Action endEnemyTurn = null)
        {
            _stunResolver = stunResolver;
            _playerIdResolver = playerIdResolver;
            _endPlayerTurn = endPlayerTurn;
            _endEnemyTurn = endEnemyTurn;
        }

        /// <summary>Engancha el subscriber. Idempotente.</summary>
        public void Attach()
        {
            if (_onTurnStartedHandler != null) return;
            _onTurnStartedHandler = HandleTurnStarted;
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
        }

        public void Dispose()
        {
            if (_onTurnStartedHandler == null) return;
            EventManager.UnSubscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
            _onTurnStartedHandler = null;
        }

        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid actingGuid)) return;
            if (actingGuid == Guid.Empty) return;

            var stun = _stunResolver?.Invoke();
            if (stun == null) return;
            if (!stun.IsStunned(actingGuid)) return;

            bool isPlayer = _playerIdResolver != null && actingGuid == _playerIdResolver();
            var endTurn = isPlayer ? _endPlayerTurn : _endEnemyTurn;

            // Sin vía para cerrar el turno de este actor no salteamos NI consumimos: quemar el
            // turno de stun sin saltear dejaría al actor jugando gratis y con un turno menos
            // de stun.
            if (endTurn == null) return;

            // El decremento va acá — en el turno que efectivamente se pierde. Hacerlo en
            // OnTurnFinished descontaría también los turnos jugados normalmente.
            stun.ConsumeTurn(actingGuid);
            SkipsPerformed++;

            endTurn();
        }
    }
}
