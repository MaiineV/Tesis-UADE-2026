using System;
using System.Collections.Generic;

namespace Rollgeon.Entities
{
    /// <summary>
    /// Query service for entity lookups. Consumed by target queries
    /// (<c>TQ_AllEnemies</c>, etc.) via <c>ServiceLocator</c>.
    /// </summary>
    public interface IEntityQueryService
    {
        IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid);
        IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid);
    }
}
