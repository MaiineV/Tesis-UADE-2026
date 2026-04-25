using System;
using System.Collections.Generic;
using Rollgeon.Effects.Selection;

namespace Rollgeon.Entities
{
    /// <summary>
    /// Query service for entity lookups via <c>ServiceLocator</c>.
    /// </summary>
    public interface IEntityQueryService
    {
        IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid);
        IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid);

        /// <summary>
        /// Devuelve la categoría de <paramref name="target"/> relativa a <paramref name="owner"/>
        /// (Ally, Enemy, Neutral, Player, Prop). Usado por <see cref="SelectionSettings"/> para
        /// filtrar slots ocupados según <see cref="EntityFilterMask"/>.
        /// </summary>
        EntityFilterMask GetRelationship(Guid owner, Guid target);
    }
}
