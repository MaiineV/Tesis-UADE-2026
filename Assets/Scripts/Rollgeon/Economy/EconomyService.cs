using Patterns;
using UnityEngine;

namespace Rollgeon.Economy
{
    /// <summary>
    /// Implementación minimal de <see cref="IEconomyService"/> — contador de oro
    /// in-memory + trigger de <c>OnGoldChanged</c>. MVP antes del sistema de
    /// atributos real (§1.3).
    /// </summary>
    public sealed class EconomyService : IEconomyService
    {
        private int _gold;

        public int CurrentGold => _gold;

        public EconomyService(int startingGold)
        {
            _gold = Mathf.Max(0, startingGold);
            EventManager.Trigger(EventName.OnGoldChanged, _gold, _gold);
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            _gold += amount;
            EventManager.Trigger(EventName.OnGoldChanged, _gold, amount);
        }

        public bool Spend(int amount)
        {
            if (amount <= 0) return true;
            if (_gold < amount) return false;
            _gold -= amount;
            EventManager.Trigger(EventName.OnGoldChanged, _gold, -amount);
            return true;
        }

        public bool CanAfford(int amount) => amount <= 0 || _gold >= amount;
    }
}
