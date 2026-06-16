// Foundation#0002_FSM — Patterns.FSM.IState
// Plan §3, §4.1: marker interface no genérico para permitir colecciones heterogéneas
// de estados y chequeos de runtime. Sólo expone Name para logs/debug.
// Zero UnityEngine deps — pure C#.

namespace Patterns.FSM
{
    /// <summary>
    /// Marker interface for any state that participates in a <see cref="StateMachine{TContext,TInput}"/>.
    /// Non-generic por diseño: habilita referencias heterogéneas (listas, logs) sin
    /// arrastrar type params. Los estados concretos heredan de <see cref="BaseState{TContext,TInput}"/>.
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// Nombre legible del estado. Por default <c>GetType().Name</c> en
        /// <see cref="BaseState{TContext,TInput}"/>, pero se puede overridear.
        /// </summary>
        string Name { get; }
    }
}
