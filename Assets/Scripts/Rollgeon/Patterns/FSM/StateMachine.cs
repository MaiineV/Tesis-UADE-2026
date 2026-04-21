// Foundation#0002_FSM — Patterns.FSM.StateMachine<TContext, TInput>
// Plan §3, §4.3, §5: coordinador genérico, single-thread, sin MonoBehaviour.
//
// Reglas clave:
//  - SendInput es reentrante-safe via queue + drain (§5.3, §10 R6).
//  - Stop() llama Exit(default) en el Current (§5.4).
//  - Update/LateUpdate/FixedUpdate son no-ops si !IsRunning o Current == null.
//  - Emite C# events tipados: OnStateEntered, OnStateExited, OnTransition.
//  - Zero acoplamiento con EventManager (R3). Zero UnityEngine deps en core.
//  - Si un estado concreto no overridea CheckInput (devuelve false), la FSM
//    consulta la tabla declarativa registrada por StateMachineBuilder (si existe).

using System;
using System.Collections.Generic;

namespace Patterns.FSM
{
    /// <summary>
    /// Finite State Machine genérica y pura (no MonoBehaviour, no UnityEngine).
    /// El owner decide cuándo tickearla (Update/LateUpdate/FixedUpdate) y cuándo
    /// alimentarla con inputs (<see cref="SendInput"/>). Ver plan §5.
    /// </summary>
    /// <typeparam name="TContext">Tipo del owner/contexto compartido por todos los estados.</typeparam>
    /// <typeparam name="TInput">Tipo discriminador de inputs de transición.</typeparam>
    public sealed class StateMachine<TContext, TInput>
    {
        private readonly BaseState<TContext, TInput> _initial;
        private readonly Queue<TInput> _pendingInputs = new Queue<TInput>();
        private bool _isDispatching;

        // Tabla declarativa opcional registrada por StateMachineBuilder.
        // Key: (fromState, input). Value: list de (guard, targetState) evaluados en orden.
        // guard puede ser null (se interpreta como "siempre true").
        internal Dictionary<TransitionKey, List<TransitionEntry>> DeclarativeTable { get; set; }

        /// <summary>Contexto compartido; inmutable tras construcción.</summary>
        public TContext Context { get; }

        /// <summary>Estado activo. Null antes de <see cref="Start"/> o tras <see cref="Stop"/>.</summary>
        public BaseState<TContext, TInput> Current { get; private set; }

        /// <summary>
        /// <c>true</c> entre <see cref="Start"/> y <see cref="Stop"/>. Los ticks
        /// (<see cref="Update"/>/<see cref="LateUpdate"/>/<see cref="FixedUpdate"/>)
        /// son no-ops cuando esto es <c>false</c>.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>Notifica el estado recién entrado (después de <c>Enter</c>).</summary>
        public event Action<BaseState<TContext, TInput>> OnStateEntered;

        /// <summary>Notifica el estado recién abandonado (después de <c>Exit</c>).</summary>
        public event Action<BaseState<TContext, TInput>> OnStateExited;

        /// <summary>Notifica (previous, next, input) luego de consumar la transición.</summary>
        public event Action<BaseState<TContext, TInput>,
                            BaseState<TContext, TInput>,
                            TInput> OnTransition;

        public StateMachine(TContext context, BaseState<TContext, TInput> initial)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            Context = context;
            _initial = initial;
        }

        /// <summary>
        /// Inicia la FSM. Setea <see cref="Current"/> al estado inicial, llama
        /// <c>Enter(initialInput)</c> y emite <see cref="OnStateEntered"/>.
        /// No-op si ya está corriendo.
        /// </summary>
        public void Start(TInput initialInput = default)
        {
            if (IsRunning) return;
            Current = _initial;
            IsRunning = true;
            Current.Enter(initialInput);
            OnStateEntered?.Invoke(Current);
        }

        /// <summary>
        /// Detiene la FSM. Llama <c>Exit(default)</c> sobre <see cref="Current"/>,
        /// marca <see cref="IsRunning"/>=false. No emite <see cref="OnStateExited"/>
        /// (es shutdown, no transición). Ver plan §5.4.
        /// </summary>
        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            Current?.Exit(default);
            _pendingInputs.Clear();
        }

        /// <summary>
        /// Envía un input. Si hay un dispatch en curso (caller vino de dentro de
        /// un Enter/Exit), se encola y drena después. Ver plan §5.3, §10 R6.
        /// No-op si !IsRunning.
        /// </summary>
        public void SendInput(TInput input)
        {
            if (!IsRunning) return;

            if (_isDispatching)
            {
                _pendingInputs.Enqueue(input);
                return;
            }

            _isDispatching = true;
            try
            {
                DispatchInternal(input);
                while (_pendingInputs.Count > 0 && IsRunning)
                {
                    DispatchInternal(_pendingInputs.Dequeue());
                }
            }
            finally
            {
                _isDispatching = false;
            }
        }

        /// <summary>
        /// Fuerza un estado sin pasar por <c>CheckInput</c>. Útil para bootstrapping,
        /// recovery y testing. Emite Exit(old) → Enter(new) → OnStateExited →
        /// OnStateEntered → OnTransition, con la misma reentrancy-safety que <see cref="SendInput"/>.
        /// No-op si !IsRunning, si <paramref name="next"/> es null o si coincide con Current.
        /// </summary>
        public void ForceState(BaseState<TContext, TInput> next, TInput input = default)
        {
            if (!IsRunning) return;
            if (next == null) return;
            if (ReferenceEquals(next, Current)) return;

            if (_isDispatching)
            {
                // Durante un dispatch, encolamos un "forced transition" como marker
                // especial usando una pseudo-queue paralela no sería limpio; la
                // forma conservadora es ejecutarlo inmediatamente bajo el mismo
                // dispatch flag (Enter/Exit de estados concretos pueden seguir
                // encolando inputs vía SendInput sin reentrar).
                ApplyTransition(Current, next, input);
                return;
            }

            _isDispatching = true;
            try
            {
                ApplyTransition(Current, next, input);
                while (_pendingInputs.Count > 0 && IsRunning)
                {
                    DispatchInternal(_pendingInputs.Dequeue());
                }
            }
            finally
            {
                _isDispatching = false;
            }
        }

        /// <summary>Tick por frame. No-op si !IsRunning o Current==null.</summary>
        public void Update()
        {
            if (!IsRunning) return;
            Current?.Update();
        }

        /// <summary>Tick post-render. No-op si !IsRunning o Current==null.</summary>
        public void LateUpdate()
        {
            if (!IsRunning) return;
            Current?.LateUpdate();
        }

        /// <summary>Tick físico. No-op si !IsRunning o Current==null.</summary>
        public void FixedUpdate()
        {
            if (!IsRunning) return;
            Current?.FixedUpdate();
        }

        // --- internals ---

        private void DispatchInternal(TInput input)
        {
            if (!IsRunning || Current == null) return;

            // 1) Pregunta al override de CheckInput. Self-transitions permitidas:
            //    devolver Current corre Exit(old) + Enter(new) en el mismo estado —
            //    necesario para cadenas de enemies (EnemyTurnState.Self) y para el
            //    caso "player unico participante" en PlayerTurnState.Self.
            if (Current.CheckInput(input, out var next) && next != null)
            {
                ApplyTransition(Current, next, input);
                return;
            }

            // 2) Fallback a la tabla declarativa, si hay.
            if (DeclarativeTable != null &&
                DeclarativeTable.TryGetValue(new TransitionKey(Current, input), out var entries))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry.Guard == null || entry.Guard(Context))
                    {
                        if (entry.Target != null)
                        {
                            ApplyTransition(Current, entry.Target, input);
                        }
                        return;
                    }
                }
            }
            // else: input ignorado — no-op, no eventos.
        }

        private void ApplyTransition(BaseState<TContext, TInput> from,
                                     BaseState<TContext, TInput> to,
                                     TInput input)
        {
            from?.Exit(input);
            OnStateExited?.Invoke(from);

            Current = to;

            to.Enter(input);
            OnStateEntered?.Invoke(to);

            OnTransition?.Invoke(from, to, input);
        }

        // --- internal types shared with the builder ---

        internal readonly struct TransitionKey : IEquatable<TransitionKey>
        {
            public readonly BaseState<TContext, TInput> From;
            public readonly TInput Input;

            public TransitionKey(BaseState<TContext, TInput> from, TInput input)
            {
                From = from;
                Input = input;
            }

            public bool Equals(TransitionKey other)
            {
                return ReferenceEquals(From, other.From)
                    && EqualityComparer<TInput>.Default.Equals(Input, other.Input);
            }

            public override bool Equals(object obj) => obj is TransitionKey k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = From != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(From) : 0;
                    h = (h * 397) ^ (Input != null ? EqualityComparer<TInput>.Default.GetHashCode(Input) : 0);
                    return h;
                }
            }
        }

        internal readonly struct TransitionEntry
        {
            public readonly TransitionGuard<TContext> Guard;
            public readonly BaseState<TContext, TInput> Target;

            public TransitionEntry(TransitionGuard<TContext> guard, BaseState<TContext, TInput> target)
            {
                Guard = guard;
                Target = target;
            }
        }
    }
}
