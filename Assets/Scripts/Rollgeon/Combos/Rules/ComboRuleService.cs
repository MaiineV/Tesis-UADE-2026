using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Combos.Rules
{
    /// <summary>Impl runtime de <see cref="IComboRuleService"/> (clase plana).</summary>
    public sealed class ComboRuleService : IComboRuleService, IPreloadableService, IDisposable
    {
        private readonly HashSet<string> _ladderSkippedStepSources = new();
        private EventManager.EventReceiver _onRunStart;

        /// <summary>Antes de InventoryService (60), que registra las reglas al agregar items.</summary>
        public int Priority => 55;

        public void Register()
        {
            ServiceLocator.AddService<IComboRuleService>(this, ServiceScope.Global);
            _onRunStart = _ => _ladderSkippedStepSources.Clear(); // defensivo: una run nueva arranca con reglas estándar
            EventManager.Subscribe(EventName.OnRunStart, _onRunStart);
        }

        public void Dispose()
        {
            if (_onRunStart != null)
            {
                EventManager.UnSubscribe(EventName.OnRunStart, _onRunStart);
                _onRunStart = null;
            }
            _ladderSkippedStepSources.Clear();
        }

        // ---- IComboRuleService -------------------------------------------------

        public bool LadderAllowsSkippedStep => _ladderSkippedStepSources.Count > 0;

        public void AddLadderSkippedStep(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _ladderSkippedStepSources.Add(sourceId);
        }

        public void RemoveLadderSkippedStep(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _ladderSkippedStepSources.Remove(sourceId);
        }
    }
}
