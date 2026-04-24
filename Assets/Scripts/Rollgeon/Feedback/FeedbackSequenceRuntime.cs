namespace Rollgeon.Feedback
{
    /// <summary>
    /// Puntero estático al <see cref="FeedbackEventBus"/> activo. Usado por componentes
    /// que no tienen referencia directa al bus (Animation Events, particle stop callbacks).
    /// TECHNICAL.md §10.8.2.
    /// </summary>
    public static class FeedbackSequenceRuntime
    {
        public static FeedbackEventBus Current { get; private set; }

        public static void SetCurrent(FeedbackEventBus bus) => Current = bus;

        /// <summary>
        /// Limpia el puntero solo si coincide con <paramref name="expected"/>. Protege
        /// contra teardowns fuera de orden entre secuencias anidadas/concurrentes.
        /// </summary>
        public static void ClearCurrent(FeedbackEventBus expected)
        {
            if (Current == expected) Current = null;
        }

        /// <summary>Conveniencia — no-op si no hay bus activo.</summary>
        public static void Publish(string key) => Current?.Publish(key);
    }
}
