namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Resultado de evaluar un nodo de <see cref="AIDecisionNode"/>. TECHNICAL.md §7.5.
    /// </summary>
    /// <remarks>
    /// <see cref="Running"/> queda reservado para ticks multi-frame futuros (ej. animaciones
    /// largas de habilidades). El FP evalua sincrono y solo usa <see cref="Succeeded"/> y
    /// <see cref="Failed"/>.
    /// </remarks>
    public enum AIResult
    {
        Succeeded,
        Failed,
        Running
    }
}
