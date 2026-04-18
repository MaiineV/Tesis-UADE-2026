using System;
using System.Collections.Generic;
using Rollgeon.Combat.Initiative;

namespace Rollgeon.Combat.FSM.Tests
{
    /// <summary>
    /// Fake determinista de <see cref="IInitiativeProvider"/>: devuelve el
    /// initiative registrado para cada guid. Guids sin entry devuelven 0.
    /// </summary>
    internal sealed class FakeInitiativeProvider : IInitiativeProvider
    {
        private readonly Dictionary<Guid, int> _rolls = new Dictionary<Guid, int>();

        public void SetRoll(Guid entityGuid, int initiative) => _rolls[entityGuid] = initiative;

        public int RollInitiative(Guid entityGuid)
        {
            return _rolls.TryGetValue(entityGuid, out var v) ? v : 0;
        }
    }
}
