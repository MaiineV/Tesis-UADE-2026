using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Damage
{
    /// <summary>Impl runtime de <see cref="IBaseDamageOverrideService"/> (clase plana).</summary>
    public sealed class BaseDamageOverrideService : IBaseDamageOverrideService, IPreloadableService, IDisposable
    {
        private readonly struct Entry
        {
            public readonly EffectIntReader Reader;
            public readonly int Priority;
            public readonly long Order;

            public Entry(EffectIntReader reader, int priority, long order)
            {
                Reader = reader;
                Priority = priority;
                Order = order;
            }
        }

        private readonly Dictionary<string, Entry> _entries = new();
        private EventManager.EventReceiver _onRunStart;
        private long _orderCounter;

        /// <summary>Antes de InventoryService (60), que registra overrides al agregar items.</summary>
        public int Priority => 55;

        public void Register()
        {
            ServiceLocator.AddService<IBaseDamageOverrideService>(this, ServiceScope.Global);
            _onRunStart = _ => _entries.Clear(); // defensivo: una run nueva arranca sin overrides
            EventManager.Subscribe(EventName.OnRunStart, _onRunStart);
        }

        public void Dispose()
        {
            if (_onRunStart != null)
            {
                EventManager.UnSubscribe(EventName.OnRunStart, _onRunStart);
                _onRunStart = null;
            }
            _entries.Clear();
        }

        // ---- IBaseDamageOverrideService ---------------------------------------

        public bool HasOverride => _entries.Count > 0;

        public void Register(string sourceId, EffectIntReader baseValue, int priority)
        {
            if (string.IsNullOrEmpty(sourceId) || baseValue == null) return;
            _entries[sourceId] = new Entry(baseValue, priority, ++_orderCounter);
            if (_entries.Count > 1)
                Debug.LogWarning($"[BaseDamageOverrideService] {_entries.Count} overrides de daño base " +
                                 "registrados a la vez — la categoría es excluyente por diseño (GDD); " +
                                 "gana el de mayor priority.");
        }

        public void Unregister(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _entries.Remove(sourceId);
        }

        public bool TryGetBaseDamage(Guid playerGuid, out int value)
        {
            value = 0;
            if (_entries.Count == 0) return false;

            Entry best = default;
            bool found = false;
            foreach (var e in _entries.Values)
            {
                if (!found || e.Priority > best.Priority
                    || (e.Priority == best.Priority && e.Order > best.Order))
                {
                    best = e;
                    found = true;
                }
            }
            if (!found || best.Reader == null) return false;

            var ctx = new EffectContext { SourceGuid = playerGuid, TargetGuid = playerGuid };
            value = Math.Max(0, best.Reader.Read(ctx));
            return true;
        }
    }
}
