// Foundation#0002_FSM — Patterns.FSM.TransitionGuard
// Plan §3, §4.4: delegate genérico para predicados declarativos sobre el contexto.
// Usado por StateMachineBuilder para expresar transiciones condicionales sin
// necesidad de overridear CheckInput.
// Zero UnityEngine deps — pure C#.

namespace Patterns.FSM
{
    /// <summary>
    /// Predicado que decide si una transición declarativa es admisible dado el
    /// contexto actual. Retornar <c>true</c> habilita la transición; <c>false</c>
    /// la descarta.
    /// </summary>
    /// <typeparam name="TContext">Tipo del contexto compartido por la FSM.</typeparam>
    public delegate bool TransitionGuard<TContext>(TContext context);
}
