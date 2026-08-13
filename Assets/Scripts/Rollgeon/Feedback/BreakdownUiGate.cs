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
