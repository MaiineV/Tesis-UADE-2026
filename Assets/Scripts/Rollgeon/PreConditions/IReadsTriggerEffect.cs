namespace Rollgeon.PreConditions
{
    /// <summary>
    /// Marca las precondiciones que leen <see cref="PreConditionContext.Effect"/> (el roll, el
    /// combo o el evento que disparó la evaluación). En un árbol de IA enemigo ese contexto no
    /// existe: <c>AIContextPcExtensions.BuildPcContext</c> lo deja en null, así que estas PCs
    /// nunca pasan o siempre pasan. El Editor de enemigos las marca como aviso.
    /// </summary>
    public interface IReadsTriggerEffect { }
}
