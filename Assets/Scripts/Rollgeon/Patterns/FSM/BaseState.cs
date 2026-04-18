// Foundation#0002_FSM — Patterns.FSM.BaseState<TContext, TInput>
// Plan §3, §4.2: clase abstracta base para estados.
// - TContext: owner entity (ej. CombatController, BossAI, CurrencyWallet).
// - TInput: discriminador que decide la próxima transición (ej. TurnInput, enum).
// Callbacks Enter/Exit/Update/LateUpdate/FixedUpdate son virtual vacíos.
// CheckInput es virtual y devuelve false por default: los estados que usen
// el StateMachineBuilder declarativo no necesitan overridearlo.
// Zero UnityEngine deps — pure C#.

namespace Patterns.FSM
{
    /// <summary>
    /// Estado base para una <see cref="StateMachine{TContext,TInput}"/>.
    /// Guarda un <typeparamref name="TContext"/> inmutable accesible por las subclases
    /// y provee hooks de ciclo de vida (Enter/Exit) y tick (Update/LateUpdate/FixedUpdate).
    /// </summary>
    /// <typeparam name="TContext">Tipo del owner (entity/controller que posee la FSM).</typeparam>
    /// <typeparam name="TInput">Tipo discriminador de inputs de transición.</typeparam>
    public abstract class BaseState<TContext, TInput> : IState
    {
        /// <summary>Contexto recibido en el constructor. Inmutable.</summary>
        protected TContext Context { get; }

        /// <inheritdoc />
        public virtual string Name => GetType().Name;

        protected BaseState(TContext context)
        {
            Context = context;
        }

        /// <summary>
        /// Se invoca cuando la FSM entra en este estado. <paramref name="input"/>
        /// es el input que disparó la transición (o <c>default</c> si fue Start/ForceState).
        /// </summary>
        public virtual void Enter(TInput input) { }

        /// <summary>
        /// Se invoca justo antes de abandonar el estado. <paramref name="input"/>
        /// es el input que disparó la transición (o <c>default</c> en <c>Stop()</c>).
        /// </summary>
        public virtual void Exit(TInput input) { }

        /// <summary>Tick por frame. Invocado por la FSM sólo si <c>IsRunning</c>.</summary>
        public virtual void Update() { }

        /// <summary>Tick post-render. Invocado por la FSM sólo si <c>IsRunning</c>.</summary>
        public virtual void LateUpdate() { }

        /// <summary>Tick físico. Invocado por la FSM sólo si <c>IsRunning</c>.</summary>
        public virtual void FixedUpdate() { }

        /// <summary>
        /// Decide si un input produce transición y, en caso afirmativo, provee el
        /// estado siguiente via <paramref name="next"/>.
        /// </summary>
        /// <remarks>
        /// Contrato del override (spec §1.3):
        /// - Retornar <c>true</c> + <paramref name="next"/> no nulo → se ejecuta la transición.
        /// - Retornar <c>false</c> → input ignorado, no hay transición ni eventos.
        /// <para/>
        /// Default: devuelve <c>false</c>. Eso permite que estados declarados con
        /// <see cref="StateMachineBuilder{TContext,TInput}"/> no necesiten overridear.
        /// La <see cref="StateMachine{TContext,TInput}"/> consulta primero este método
        /// y, si da <c>false</c>, cae a la tabla del builder (si existe).
        /// </remarks>
        public virtual bool CheckInput(TInput input, out BaseState<TContext, TInput> next)
        {
            next = null;
            return false;
        }
    }
}
