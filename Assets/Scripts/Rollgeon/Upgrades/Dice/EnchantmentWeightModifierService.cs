using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>Impl runtime de <see cref="IEnchantmentWeightModifierService"/> (clase plana).</summary>
    public sealed class EnchantmentWeightModifierService
        : IEnchantmentWeightModifierService, IPreloadableService, IDisposable
    {
        private readonly Dictionary<string, float> _multipliers = new();
        private EventManager.EventReceiver _onRunStart;

        /// <summary>Antes de InventoryService (60), que registra al agregar items.</summary>
        public int Priority => 55;

        public void Register()
        {
            ServiceLocator.AddService<IEnchantmentWeightModifierService>(this, ServiceScope.Global);
            _onRunStart = _ => _multipliers.Clear();
            EventManager.Subscribe(EventName.OnRunStart, _onRunStart);
        }

        public void Dispose()
        {
            if (_onRunStart != null)
            {
                EventManager.UnSubscribe(EventName.OnRunStart, _onRunStart);
                _onRunStart = null;
            }
            _multipliers.Clear();
        }

        public void Register(string sourceId, float cursedWeightMultiplier)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _multipliers[sourceId] = Mathf.Max(0.01f, cursedWeightMultiplier);
        }

        public void Unregister(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _multipliers.Remove(sourceId);
        }

        public float ResolveCursedMultiplier()
        {
            float m = 1f;
            foreach (var v in _multipliers.Values) m *= v;
            return m;
        }
    }
}
