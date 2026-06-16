using System;
using System.Collections.Generic;

namespace Rollgeon.Combat.Initiative
{
    /// <summary>
    /// Utilidades de desempate determinista para el orden de turno.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué Guid.CompareTo.</b> Cuando dos entidades sacan el mismo
    /// initiative total, queremos un orden fijo y reproducible entre corridas
    /// (TECHNICAL.md §12.7: "el orden se fija al build y no cambia hasta el
    /// próximo <c>BuildForCombat</c>"). <see cref="Guid.CompareTo(Guid)"/> es
    /// total y determinista — no depende del orden de inserción ni del RNG.
    /// </para>
    /// </remarks>
    public static class InitiativeFallbacks
    {
        /// <summary>
        /// Comparer que ordena por initiative DESC, con tie-break por Guid ASC.
        /// </summary>
        public static readonly IComparer<(Guid guid, int initiative)> DescByInitiativeThenByGuid
            = Comparer<(Guid guid, int initiative)>.Create((a, b) =>
            {
                int byInit = b.initiative.CompareTo(a.initiative); // DESC
                if (byInit != 0)
                {
                    return byInit;
                }
                return a.guid.CompareTo(b.guid); // ASC estable
            });
    }
}
