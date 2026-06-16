using System.Collections.Generic;

namespace Rollgeon.Meta
{
    /// <summary>
    /// Servicio global que trackea el progreso de condiciones durante la run y
    /// dispara la evaluación mid-run / fin de run (#164). La UI de resultados
    /// (Victory/Defeat) lee <see cref="UnlocksThisRun"/> para listar lo conseguido.
    /// </summary>
    public interface IUnlockProgressService
    {
        /// <summary>
        /// Desbloqueos conseguidos en la última run (mid-run + cierre), en orden.
        /// Se limpia al arrancar la run siguiente — sobrevive al <c>EndRun</c> para
        /// que las pantallas de resultados puedan leerlo.
        /// </summary>
        IReadOnlyList<UnlockDefinitionSO> UnlocksThisRun { get; }
    }
}
