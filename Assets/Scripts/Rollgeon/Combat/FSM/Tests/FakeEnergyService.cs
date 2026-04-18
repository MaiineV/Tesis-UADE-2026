using System;
using System.Collections.Generic;
using Rollgeon.Combat.Energy;

namespace Rollgeon.Combat.FSM.Tests
{
    /// <summary>
    /// Fake minimal de <see cref="IEnergyService"/> — diccionario in-memory.
    /// Plan §3.4.
    /// </summary>
    internal sealed class FakeEnergyService : IEnergyService
    {
        public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();
        public int MaxPerEntity = 4;

        public int RegenerateCallCount { get; private set; }
        public readonly List<Guid> RegenerateCalledFor = new List<Guid>();

        public void InitializeForEntity(Guid entityId) => Current[entityId] = MaxPerEntity;

        public bool SpendEnergy(Guid entityId, int cost)
        {
            if (cost < 0) return false;
            if (!Current.TryGetValue(entityId, out var have)) return false;
            if (cost > have) return false;
            Current[entityId] = have - cost;
            return true;
        }

        public void RegenerateAtTurnEnd(Guid entityId)
        {
            RegenerateCallCount++;
            RegenerateCalledFor.Add(entityId);
            if (Current.TryGetValue(entityId, out var have))
            {
                Current[entityId] = Math.Min(MaxPerEntity, have + 1);
            }
        }

        public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;

        public int GetMax(Guid entityId) => MaxPerEntity;
    }
}
