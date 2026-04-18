using System;
using Patterns;
using Patterns.FSM;

namespace Rollgeon.Combat.FSM.States
{
    /// <summary>
    /// Turno del player: espera <c>PlayerEndTurn</c> explicito desde la UI.
    /// Plan §3.2 / §4.3 + Revision 2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Revision 2 — IMPORTANTE.</b> El turno <b>no</b> se auto-cierra cuando
    /// <c>Energy == 0</c>. El player decide via UI (<c>PlayerEndTurn</c>).
    /// Con energia en 0 el <see cref="Rollgeon.Combat.Actions.TurnManager.CanExecute"/>
    /// devuelve false para acciones que cuesten >0, pero el FSM se queda aca.
    /// El player puede seguir usando items/acciones de 0 costo.
    /// </para>
    /// <para>
    /// <b>Event ordering (plan R9).</b> <c>Exit</c> dispara <c>OnTurnFinished</c>
    /// con el Guid <b>cacheado</b> en <c>Enter</c> y RECIEN DESPUES llama
    /// <c>TurnOrder.Advance()</c>. Los listeners leen el cursor correcto.
    /// </para>
    /// </remarks>
    public sealed class PlayerTurnState : BaseState<CombatContext, CombatInput>
    {
        internal EnemyTurnState Enemy;
        internal CombatExitState Exit;
        // Self-ref: tras Advance puede volver a ser el turno del player (Speed > enemy).
        internal PlayerTurnState Self;

        // Guid cacheado para garantizar que OnTurnStarted / OnTurnFinished
        // refieren al mismo actor aun si TurnOrder.Current cambio durante el turno.
        private Guid _actingGuid;

        public PlayerTurnState(CombatContext context) : base(context) { }

        public override void Enter(CombatInput input)
        {
            _actingGuid = Context.PlayerId;
            // TurnManager (T100b) esta suscrito a OnTurnStarted y limpia _actionsUsedThisTurn.
            EventManager.Trigger(EventName.OnTurnStarted, _actingGuid);
        }

        public override void Exit(CombatInput input)
        {
            // 1) OnTurnFinished con el Guid cacheado — cursor AUN no cambio.
            //    EnergyService puede correr regen leyendo TurnOrder.Current == _actingGuid.
            EventManager.Trigger(EventName.OnTurnFinished, _actingGuid);

            // 2) Avanza el cursor salvo que estemos saliendo por CombatEnded
            //    (el combate termino — no tiene sentido rotar al siguiente actor).
            //    Si hay wrap-around TurnOrder dispara OnTurnQueueBuilt internamente.
            if (input != CombatInput.CombatEnded)
            {
                Context.TurnOrder.Advance();
            }
        }

        public override bool CheckInput(CombatInput input, out BaseState<CombatContext, CombatInput> next)
        {
            switch (input)
            {
                case CombatInput.PlayerActionDone:
                    // Revision 2: NO transicionar por energia == 0. Self-loop inerte —
                    // el state queda donde esta, esperando PlayerEndTurn o CombatEnded.
                    // La UI es responsable de deshabilitar visualmente las acciones
                    // que el player no puede costear.
                    next = null;
                    return false;

                case CombatInput.PlayerEndTurn:
                    // Decidir siguiente estado leyendo TurnOrder.Current DESPUES de
                    // Advance(). Advance() corre en Exit() → ApplyTransition en la FSM
                    // secuencia Exit → Enter con Current ya expuesto; pero necesitamos
                    // decidir el TARGET aca, ANTES de que Exit corra. Truco: llamamos
                    // Advance() aca en el guard (idempotente) y elegimos next; Exit
                    // solo dispara OnTurnFinished. Rompe la nice separacion del plan.
                    //
                    // Alternativa cleaner (elegida): Exit primero dispara event +
                    // Advance, pero CheckInput NO lo hace — decidimos el target
                    // leyendo "quien seria el proximo" simulando con un peek. Como
                    // TurnOrderService no expone Peek, lo resolvemos asi:
                    //   - Devolvemos un target "placeholder" (Enemy) siempre.
                    //   - El dispatcher llama Exit (donde Advance corre).
                    //   - Enter de EnemyTurnState chequea si TurnOrder.Current ==
                    //     PlayerId (deberia ser imposible tras Advance salvo que el
                    //     player sea el unico participante) y en ese caso ya estamos
                    //     en el estado correcto self-looping al player.
                    //
                    // Decision final: leer el next guid SIN mutar el servicio. Como
                    // el servicio es circular, "proximo" = order[(cursor+1) %
                    // ParticipantCount]. Para evitar tocar la API, hacemos peek
                    // manual via la lista OrderForRound.
                    var order = Context.TurnOrder.OrderForRound;
                    if (order == null || order.Count == 0)
                    {
                        next = Exit;
                        return true;
                    }

                    // Peek del proximo guid.
                    int curIndex = IndexOf(order, Context.TurnOrder.Current);
                    if (curIndex < 0)
                    {
                        // Shouldn't happen — defensive fallback.
                        next = Self;
                        return true;
                    }
                    int nextIndex = (curIndex + 1) % order.Count;
                    Guid nextGuid = order[nextIndex];

                    next = (nextGuid == Context.PlayerId)
                        ? (BaseState<CombatContext, CombatInput>)Self
                        : Enemy;
                    return true;

                case CombatInput.CombatEnded:
                    next = Exit;
                    return true;

                default:
                    next = null;
                    return false;
            }
        }

        private static int IndexOf(System.Collections.Generic.IReadOnlyList<Guid> list, Guid g)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == g) return i;
            }
            return -1;
        }
    }
}
