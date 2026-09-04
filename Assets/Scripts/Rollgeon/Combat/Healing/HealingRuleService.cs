using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Combat.Healing
{
    /// <summary>Impl runtime de <see cref="IHealingRuleService"/> (clase plana).</summary>
    public sealed class HealingRuleService : IHealingRuleService, IPreloadableService, IDisposable
    {
        private readonly HashSet<string> _passiveHealingBlockSources = new();
        private readonly Dictionary<string, float> _potionHealMultipliers = new();
        private EventManager.EventReceiver _onRunStart;

        /// <summary>Antes de InventoryService (60), que registra las reglas al agregar items.</summary>
        public int Priority => 55;

        public void Register()
        {
            ServiceLocator.AddService<IHealingRuleService>(this, ServiceScope.Global);
            _onRunStart = _ => ClearRules(); // defensivo: una run nueva arranca sin reglas
            EventManager.Subscribe(EventName.OnRunStart, _onRunStart);
        }

        public void Dispose()
        {
            if (_onRunStart != null)
            {
                EventManager.UnSubscribe(EventName.OnRunStart, _onRunStart);
                _onRunStart = null;
            }
            ClearRules();
        }

        private void ClearRules()
        {
            _passiveHealingBlockSources.Clear();
            _potionHealMultipliers.Clear();
        }

        // ---- IHealingRuleService -----------------------------------------------

        public bool PassiveItemHealingBlocked => _passiveHealingBlockSources.Count > 0;

        public void AddPassiveHealingBlock(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _passiveHealingBlockSources.Add(sourceId);
        }

        public void RemovePassiveHealingBlock(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _passiveHealingBlockSources.Remove(sourceId);
        }

        public float PotionHealMultiplier
        {
            get
            {
                float product = 1f;
                foreach (var factor in _potionHealMultipliers.Values) product *= factor;
                return product;
            }
        }

        public IReadOnlyDictionary<string, float> PotionHealMultiplierSources => _potionHealMultipliers;

        public void AddPotionHealMultiplier(string sourceId, float factor)
        {
            if (string.IsNullOrEmpty(sourceId) || factor <= 0f) return;
            _potionHealMultipliers[sourceId] = factor;
        }

        public void RemovePotionHealMultiplier(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _potionHealMultipliers.Remove(sourceId);
        }
    }
}
