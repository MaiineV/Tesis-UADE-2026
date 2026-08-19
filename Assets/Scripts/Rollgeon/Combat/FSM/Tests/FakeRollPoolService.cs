using System;
using System.Collections.Generic;
using Rollgeon.Combat.Rolls;

namespace Rollgeon.Combat.FSM.Tests
{
    /// <summary>
    /// Fake minimal de <see cref="IRollPoolService"/> — diccionario in-memory.
    /// Reemplaza al viejo FakeEnergyService (Feature#0050).
    /// </summary>
    internal sealed class FakeRollPoolService : IRollPoolService
    {
        public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();
        public int Cap = 15;
        public int RollsPerTurn = 5;
        public bool InCombat = true;

        public bool IsCombatActive => InCombat;

        public void InitializeForEntity(Guid entityId) => Current[entityId] = RollsPerTurn;

        public bool TrySpendRolls(Guid entityId, int count)
        {
            if (count < 0) return false;
            if (count == 0) return true;
            if (!Current.TryGetValue(entityId, out var have)) return false;
            if (count > have) return false;
            Current[entityId] = have - count;
            return true;
        }

        public int Drain(Guid entityId, int amount)
        {
            if (amount <= 0) return 0;
            if (!Current.TryGetValue(entityId, out var have)) return 0;
            int drained = Math.Min(amount, have);
            Current[entityId] = have - drained;
            return drained;
        }

        public void AddRolls(Guid entityId, int amount)
        {
            if (amount <= 0) return;
            Current.TryGetValue(entityId, out var have);
            Current[entityId] = Math.Min(Cap, have + amount);
        }

        public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;

        public int GetMax(Guid entityId) => Cap;

        public int GetRollsPerTurn(Guid entityId) => RollsPerTurn;

        public void AddPerTurnGrantBonus(int amount) => RollsPerTurn += amount;

        public void RestoreCurrent(Guid entityId, int value)
            => Current[entityId] = Math.Clamp(value, 0, Cap);
    }
}
