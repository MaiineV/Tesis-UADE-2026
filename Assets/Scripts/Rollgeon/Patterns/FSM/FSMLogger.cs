// Foundation#0002_FSM — Patterns.FSM.FSMLogger
// Plan §3, §4.3: utilitario estático para logueo de transiciones.
// Compilado-fuera en release via [Conditional]: overhead cero en builds finales.
// Única clase del paquete autorizada a tocar UnityEngine.
//
// Uso:
//   sm.OnTransition += (from, to, input) => FSMLogger.LogTransition(from, to, input);
//
// Los Conditional atributos son inclusivos (OR): basta con que UNA de las dos
// flags esté definida para que el cuerpo se compile.

using System.Diagnostics;

namespace Patterns.FSM
{
    /// <summary>
    /// Wrapper de logueo para transiciones/eventos de una
    /// <see cref="StateMachine{TContext,TInput}"/>. Marcado con
    /// <see cref="ConditionalAttribute"/> para que las invocaciones se eliminen
    /// del IL en builds de release (sin <c>UNITY_EDITOR</c> ni <c>DEVELOPMENT_BUILD</c>).
    /// </summary>
    public static class FSMLogger
    {
        /// <summary>Loguea una transición normal (por input).</summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogTransition<TContext, TInput>(
            BaseState<TContext, TInput> from,
            BaseState<TContext, TInput> to,
            TInput input)
        {
            var fromName = from != null ? from.Name : "<null>";
            var toName = to != null ? to.Name : "<null>";
            UnityEngine.Debug.Log($"[FSM] {fromName} --({input})--> {toName}");
        }

        /// <summary>Loguea una transición forzada (via <c>ForceState</c>).</summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogForced<TContext, TInput>(
            BaseState<TContext, TInput> from,
            BaseState<TContext, TInput> to)
        {
            var fromName = from != null ? from.Name : "<null>";
            var toName = to != null ? to.Name : "<null>";
            UnityEngine.Debug.Log($"[FSM][Forced] {fromName} --> {toName}");
        }

        /// <summary>Loguea una entrada de estado.</summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogEntered<TContext, TInput>(BaseState<TContext, TInput> state)
        {
            var name = state != null ? state.Name : "<null>";
            UnityEngine.Debug.Log($"[FSM] Entered {name}");
        }

        /// <summary>Loguea una salida de estado.</summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogExited<TContext, TInput>(BaseState<TContext, TInput> state)
        {
            var name = state != null ? state.Name : "<null>";
            UnityEngine.Debug.Log($"[FSM] Exited {name}");
        }

        /// <summary>Loguea un mensaje arbitrario con prefijo FSM.</summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message)
        {
            UnityEngine.Debug.Log($"[FSM] {message}");
        }
    }
}
