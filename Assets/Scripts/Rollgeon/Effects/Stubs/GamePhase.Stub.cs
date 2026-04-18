namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [STUB] — TECHNICAL.md §12.0.1. Enum mínimo para que
    /// <see cref="Selection.TargetQueryContext"/> pueda reportar la fase actual a las
    /// queries sin acoplar esta foundation a <c>IPhaseService</c>. El enum real tiene
    /// más valores (Loading, GameOver) y vive en Rollgeon.Combat / Rollgeon.Phase.
    /// <para>Regla de estabilidad: nunca renumerar — los valores persisten en saves.</para>
    /// </summary>
    public enum GamePhase
    {
        None = 0,
        Exploration = 1,
        Combat = 2,
    }
}
