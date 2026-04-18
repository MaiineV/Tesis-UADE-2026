// Foundation#0002_FSM — Patterns.FSM.StateMachineBuilder<TContext, TInput>
// Plan §3, §4.4: fluent builder declarativo.
//
// Uso típico:
//   var sm = new StateMachineBuilder<Ctx, Input>()
//       .From(a).On(Input.X).To(b)
//       .From(b).On(Input.Y).If(ctx => ctx.ready).To(c)
//       .Build(ctx, initial: a);
//
// Convive con override-based: si el estado overrides CheckInput y devuelve true,
// la StateMachine ignora la tabla. Si devuelve false, cae a la tabla.
// Zero UnityEngine deps — pure C#.

using System;
using System.Collections.Generic;

namespace Patterns.FSM
{
    /// <summary>
    /// Builder declarativo para armar tablas de transición sin necesidad de
    /// overridear <see cref="BaseState{TContext,TInput}.CheckInput"/> en cada
    /// estado. Ver plan §4.4.
    /// </summary>
    public sealed class StateMachineBuilder<TContext, TInput>
    {
        // Internamente usamos el mismo tipo de key que StateMachine, pero acá
        // acumulamos (from, input) → lista ordenada de (guard, target).
        private readonly List<PendingTransition> _pending = new List<PendingTransition>();

        // Estado del fluent (from → on → if? → to).
        private BaseState<TContext, TInput> _currentFrom;
        private bool _hasFrom;

        private TInput _currentInput;
        private bool _hasInput;

        private TransitionGuard<TContext> _currentGuard;

        /// <summary>Inicia la declaración de una transición con el estado de origen.</summary>
        public StateMachineBuilder<TContext, TInput> From(BaseState<TContext, TInput> state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            _currentFrom = state;
            _hasFrom = true;
            // Reset de input/guard pendientes — nueva transición empezada.
            _hasInput = false;
            _currentInput = default;
            _currentGuard = null;
            return this;
        }

        /// <summary>Especifica el input que dispara esta transición.</summary>
        public StateMachineBuilder<TContext, TInput> On(TInput input)
        {
            if (!_hasFrom)
                throw new InvalidOperationException("StateMachineBuilder: call From(...) before On(...).");
            _currentInput = input;
            _hasInput = true;
            _currentGuard = null;
            return this;
        }

        /// <summary>
        /// Agrega un guard al par (From, On) pendiente. La transición se aplica
        /// sólo si <paramref name="guard"/> devuelve <c>true</c> en runtime.
        /// </summary>
        public StateMachineBuilder<TContext, TInput> If(TransitionGuard<TContext> guard)
        {
            if (!_hasFrom || !_hasInput)
                throw new InvalidOperationException("StateMachineBuilder: call From(...).On(...) before If(...).");
            _currentGuard = guard;
            return this;
        }

        /// <summary>
        /// Cierra la transición pendiente y la acumula en la tabla. Deja pre-cargado
        /// el mismo <c>From</c> para encadenar múltiples <c>.On(...).To(...)</c> seguidos.
        /// </summary>
        public StateMachineBuilder<TContext, TInput> To(BaseState<TContext, TInput> target)
        {
            if (!_hasFrom || !_hasInput)
                throw new InvalidOperationException("StateMachineBuilder: call From(...).On(...) before To(...).");
            if (target == null) throw new ArgumentNullException(nameof(target));

            _pending.Add(new PendingTransition(_currentFrom, _currentInput, _currentGuard, target));

            // Reset parcial: el From se mantiene (ergonomía para encadenar),
            // input y guard se limpian para forzar una nueva On().
            _hasInput = false;
            _currentInput = default;
            _currentGuard = null;
            return this;
        }

        /// <summary>
        /// Compila las transiciones declaradas, crea la <see cref="StateMachine{TContext,TInput}"/>
        /// y enlaza la tabla. No llama <c>Start()</c> — eso queda a cargo del caller.
        /// </summary>
        public StateMachine<TContext, TInput> Build(TContext context,
                                                    BaseState<TContext, TInput> initial)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));

            var sm = new StateMachine<TContext, TInput>(context, initial);

            if (_pending.Count == 0)
            {
                return sm; // Builder sin transiciones declarativas — equivale a usar la FSM "cruda".
            }

            var table = new Dictionary<StateMachine<TContext, TInput>.TransitionKey,
                                       List<StateMachine<TContext, TInput>.TransitionEntry>>();

            for (int i = 0; i < _pending.Count; i++)
            {
                var pt = _pending[i];
                var key = new StateMachine<TContext, TInput>.TransitionKey(pt.From, pt.Input);
                if (!table.TryGetValue(key, out var list))
                {
                    list = new List<StateMachine<TContext, TInput>.TransitionEntry>(1);
                    table[key] = list;
                }
                list.Add(new StateMachine<TContext, TInput>.TransitionEntry(pt.Guard, pt.Target));
            }

            sm.DeclarativeTable = table;
            return sm;
        }

        private readonly struct PendingTransition
        {
            public readonly BaseState<TContext, TInput> From;
            public readonly TInput Input;
            public readonly TransitionGuard<TContext> Guard;
            public readonly BaseState<TContext, TInput> Target;

            public PendingTransition(BaseState<TContext, TInput> from,
                                     TInput input,
                                     TransitionGuard<TContext> guard,
                                     BaseState<TContext, TInput> target)
            {
                From = from;
                Input = input;
                Guard = guard;
                Target = target;
            }
        }
    }
}
