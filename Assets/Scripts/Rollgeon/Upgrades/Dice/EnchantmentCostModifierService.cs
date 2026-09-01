using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>Impl runtime de <see cref="IEnchantmentCostModifierService"/> (clase plana).</summary>
    public sealed class EnchantmentCostModifierService
        : IEnchantmentCostModifierService, IPreloadableService, IDisposable
    {
        private readonly Dictionary<string, float> _multipliers = new();
        private EventManager.EventReceiver _onRunStart;

        /// <summary>Antes de InventoryService (60), que registra al agregar items.</summary>
        public int Priority => 55;

        public void Register()
        {
            ServiceLocator.AddService<IEnchantmentCostModifierService>(this, ServiceScope.Global);
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

        public void Register(string sourceId, float costMultiplier)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _multipliers[sourceId] = Mathf.Max(0.01f, costMultiplier);
        }

        public void Unregister(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _multipliers.Remove(sourceId);
        }

        public float ResolveMultiplier()
        {
            float m = 1f;
            foreach (var v in _multipliers.Values) m *= v;
            return m;
        }
    }
}
