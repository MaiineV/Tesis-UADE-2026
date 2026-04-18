using System;
using System.Collections.Generic;
using Rollgeon.Effects.Stubs;

namespace Rollgeon.Entities.Behaviors.Tests
{
    /// <summary>
    /// Fake in-memory de <see cref="IEntityQueryService"/> para tests edit-mode.
    /// Mapas preconfigurados por <paramref name="ownerGuid"/> — populado en cada test setup.
    /// </summary>
    internal sealed class FakeEntityQueryService : IEntityQueryService
    {
        public readonly Dictionary<Guid, List<Entity>> Allies = new Dictionary<Guid, List<Entity>>();
        public readonly Dictionary<Guid, List<Entity>> Enemies = new Dictionary<Guid, List<Entity>>();

        public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid)
        {
            return Enemies.TryGetValue(ownerGuid, out var list) ? list : new List<Entity>();
        }

        public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid)
        {
            return Allies.TryGetValue(ownerGuid, out var list) ? list : new List<Entity>();
        }
    }
}
