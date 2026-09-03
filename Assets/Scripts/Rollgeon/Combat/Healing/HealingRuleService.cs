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
        private EventManager.EventReceiver _onRunStart;

        /// <summary>Antes de InventoryService (60), que registra las reglas al agregar items.</summary>
        public int Priority => 55;

        public void Register()
        {
            ServiceLocator.AddService<IHealingRuleService>(this, ServiceScope.Global);
            _onRunStart = _ => _passiveHealingBlockSources.Clear(); // defensivo: una run nueva arranca sin reglas
            EventManager.Subscribe(EventName.OnRunStart, _onRunStart);
        }

        public void Dispose()
        {
            if (_onRunStart != null)
            {
                EventManager.UnSubscribe(EventName.OnRunStart, _onRunStart);
                _onRunStart = null;
            }
            _passiveHealingBlockSources.Clear();
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
    }
}
