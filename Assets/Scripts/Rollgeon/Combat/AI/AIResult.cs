namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Resultado de evaluar un nodo de <see cref="AIDecisionNode"/>. TECHNICAL.md §7.5.
    /// </summary>
    /// <remarks>
    /// <see cref="Running"/> lo devuelve un nodo que dejó un <see cref="AIContext.PendingWait"/>
    /// para que el consumer lo drene; el camino síncrono sólo usa <see cref="Succeeded"/> y
    /// <see cref="Failed"/>.
    /// </remarks>
    public enum AIResult
    {
        Succeeded,
        Failed,
        Running
    }
}
