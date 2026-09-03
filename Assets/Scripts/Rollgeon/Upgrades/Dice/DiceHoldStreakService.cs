using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Implementación canónica de <see cref="IDiceHoldStreakService"/>: un contador por
    /// bag slot. Consumidor de la mecánica de "guardado" para el reader
    /// <c>ReadCarrierHoldStreak</c> (Ancla).
    /// </summary>
    /// <remarks>
    /// Reset en <c>OnCombatStart</c> / <c>OnCombatEnd</c> además de en cada roll fresco:
    /// un combate cortado a mitad de mano no debe arrastrar streaks a la próxima pelea.
    /// </remarks>
    public sealed class DiceHoldStreakService : IDiceHoldStreakService, IPreloadableService, IDisposable
    {
        public const int DefaultPriority = 90;

        private readonly List<int> _streaks = new List<int>(8);
        private bool _subscribed;

        public int Priority => DefaultPriority;

        public void Register()
        {
            ServiceLocator.AddService<IDiceHoldStreakService>(this, ServiceScope.Global);
            SubscribeEvents();
        }

        public void Dispose()
        {
            UnsubscribeEvents();
            _streaks.Clear();
        }

        public void SubscribeEventsForTests() => SubscribeEvents();
        public void UnsubscribeEventsForTests() => UnsubscribeEvents();

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnCombatStart, OnCombatBoundary);
            EventManager.Subscribe(EventName.OnCombatEnd, OnCombatBoundary);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnCombatStart, OnCombatBoundary);
            EventManager.UnSubscribe(EventName.OnCombatEnd, OnCombatBoundary);
            _subscribed = false;
        }

        private void OnCombatBoundary(params object[] args) => _streaks.Clear();

        public int GetStreak(int bagSlot)
            => bagSlot >= 0 && bagSlot < _streaks.Count ? _streaks[bagSlot] : 0;

        public void OnFreshRoll() => _streaks.Clear();

        public void OnReroll(IReadOnlyList<bool> keep)
        {
            if (keep == null) { _streaks.Clear(); return; }

            // Slots nuevos (bag más grande que la última mano) arrancan en 0.
            while (_streaks.Count < keep.Count) _streaks.Add(0);
            if (_streaks.Count > keep.Count) _streaks.RemoveRange(keep.Count, _streaks.Count - keep.Count);

            for (int i = 0; i < keep.Count; i++)
                _streaks[i] = keep[i] ? _streaks[i] + 1 : 0;
        }
    }
}
