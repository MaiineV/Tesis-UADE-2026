namespace Rollgeon.Feedback
{
    /// <summary>
    /// Tipo de un <see cref="FeedbackEntry"/>. Determina qué campos autorales son relevantes
    /// (via <c>[ShowIf]</c> en el inspector) y qué rama del dispatch corre en
    /// <see cref="FeedbackManager"/>. TECHNICAL.md §10.3.
    /// </summary>
    public enum FeedbackType
    {
        VFX,
        SFX,
        Animation,
        Wait,
        BehaviorValue,
        FloatingNumber,
    }

    /// <summary>
    /// Indica si el handler de <c>BehaviorValue</c> debe leer el valor del pawn source o target.
    /// </summary>
    public enum BehaviorValueTarget
    {
        Source,
        Target,
    }

    /// <summary>
    /// Estrategia de resolución de posición spawn en <see cref="FeedbackPositionResolver"/>.
    /// TECHNICAL.md §10.6.
    /// </summary>
    public enum SpawnPosition
    {
        AtSource,
        AtTarget,
        AtSlot,
        BetweenSourceAndTarget,
        WorldPosition,
        FromReader,
    }

    /// <summary>
    /// Cómo sabe el manager que un feedback terminó. <c>Timer</c> es el fallback universal.
    /// TECHNICAL.md §10.5.
    /// </summary>
    public enum FeedbackCompletionMode
    {
        Timer,
        AnimationEvent,
        ParticleEnd,
    }

    /// <summary>Source type de un <see cref="FeedbackSequenceStep"/>. TECHNICAL.md §10.8.</summary>
    public enum StepSource
    {
        FeedbackRef,
        InlineWait,
        InlineAnimation,
        InlineBehaviorValue,
    }

    /// <summary>Cuándo arranca un step de una secuencia. TECHNICAL.md §10.8.</summary>
    public enum StepStartMode
    {
        Immediate,
        AfterPrevious,
        AfterStep,
        OnEvent,
    }

    /// <summary>Cuándo se considera terminado un step. TECHNICAL.md §10.8.</summary>
    public enum StepEndMode
    {
        OnDuration,
        OnNaturalEnd,
        OnEvent,
        Immediate,
    }

    /// <summary>Player target del <c>FromReader</c> mode — resolver decide con el <c>PlayerId</c>.</summary>
    public enum FeedbackPlayer
    {
        Player = 0,
        Enemy = 1,
    }
}
