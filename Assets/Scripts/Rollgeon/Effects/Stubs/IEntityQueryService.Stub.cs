using System;
using System.Collections.Generic;

namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [STUB] — consumido por <see cref="Selection.Queries.TQ_AllEnemies"/> via
    /// <c>ServiceLocator.TryGetService&lt;IEntityQueryService&gt;()</c>. La foundation real
    /// de Entidades / Combat registra el concrete y este stub se borra. Hasta entonces,
    /// las queries que piden el service obtienen <c>false</c> y devuelven lista vacía
    /// con warning, cumpliendo la regla "fallback defensivo" de §11.2b.
    /// </summary>
    public interface IEntityQueryService
    {
        /// <summary>Devuelve todas las entidades enemigas del owner dado.</summary>
        IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid);

        /// <summary>
        /// Devuelve todas las entidades aliadas del owner (mismo bando, excluye al
        /// propio owner). Consumido por <c>SupportHealBehavior</c> (Content#0099) para
        /// elegir target de cura. Stub ampliado — concretos downstream completan.
        /// </summary>
        IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid);
    }
}
