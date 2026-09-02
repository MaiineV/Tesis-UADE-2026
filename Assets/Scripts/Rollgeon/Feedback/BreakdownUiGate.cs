using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Latch global "la secuencia de breakdown de daño (N×M) está corriendo en la UI".
    /// Mientras esté pendiente: las <c>FeedbackSequence</c> no arrancan (el golpe real
    /// espera a que la suma animada termine — <see cref="FeedbackManager"/> tiene un
    /// failsafe anti soft-lock), y los teardowns de zona/outro de dados se difieren
    /// (mismo patrón que <c>DiceOutroGate</c>).
    /// </summary>
    /// <remarks>
    /// Ref-count y no bool: el outro de dados y el breakdown pueden solaparse y cada
    /// dueño debe poder Begin/End sin pisarse. Estado y no evento del bus porque quien
    /// llega tarde necesita preguntar "¿sigue pendiente?".
    /// </remarks>
    public static class BreakdownUiGate
    {
        private static int _count;

        public static bool Pending => _count > 0;

        public static event System.Action Changed;

        public static void Begin()
        {
            _count++;
            if (_count == 1) Changed?.Invoke();
        }

        public static void End()
        {
            if (_count == 0) return;
            _count--;
            if (_count == 0) Changed?.Invoke();
        }

        /// <summary>
        /// Corre <paramref name="continuation"/> cuando el gate esté libre: sincrónico si
        /// nada está pendiente (flujos sin secuencia — exploración, tests, director sin
        /// bindear — no cambian), o diferido hasta la transición a 0. El gate SIEMPRE
        /// baja (timeout del director + failsafe del FeedbackManager + abort en teardown),
        /// así que no hay soft-lock. Un solo disparo.
        /// </summary>
        public static void RunWhenIdle(System.Action continuation)
        {
            if (continuation == null) return;
            if (!Pending)
            {
                continuation();
                return;
            }

            System.Action handler = null;
            handler = () =>
            {
                if (Pending) return; // ref-count: esperar la transición a 0
                Changed -= handler;
                continuation();
            };
            Changed += handler;
        }

        // Sin domain reload (Enter Play Mode Options), los estáticos sobreviven entre
        // plays: un gate colgado bloquearía todos los feedbacks del combate.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _count = 0;
            Changed = null;
        }
    }
}
