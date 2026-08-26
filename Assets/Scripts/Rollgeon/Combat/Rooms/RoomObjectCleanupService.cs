using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Rooms
{
    /// <summary>
    /// Levanta la mesa al terminar la pelea: despawnea los objetos de sala que quedaron en pie.
    /// </summary>
    /// <remarks>
    /// El barrido de fin de combate (<c>CombatDeathWatcher.DespawnRemainingCombatants</c>) recorre el
    /// turn order, y un objeto con <c>HideFromTurnQueue</c> nunca entra ahí. Sin este servicio, matar
    /// al jefe con la mesa en pie dejaba los objetos parados en la sala —tapando sus casillas, que
    /// siguen registradas en el grid— hasta el fin de la run.
    /// </remarks>
    public interface IRoomObjectCleanupService
    {
        /// <summary>Anota un objeto recién colocado. Idempotente.</summary>
        void Track(Guid guid);

        /// <summary>Saca uno que ya se fue por su cuenta (roto, detonado).</summary>
        void Forget(Guid guid);

        /// <summary>Los que siguen anotados, en orden de colocación.</summary>
        IReadOnlyList<Guid> Tracked { get; }

        /// <summary>Despawnea y desanota todo lo anotado.</summary>
        void TearDownAll();
    }

    /// <inheritdoc cref="IRoomObjectCleanupService"/>
    public sealed class RoomObjectCleanupService : IRoomObjectCleanupService, IDisposable
    {
        private readonly List<Guid> _tracked = new List<Guid>();

        private EventManager.EventReceiver _onCombatEnd;
        private EventManager.EventReceiver _onRunEnd;

        public static IRoomObjectCleanupService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<IRoomObjectCleanupService>(out var existing) && existing != null)
                return existing;

            var created = new RoomObjectCleanupService();
            created.RegisterGlobal();
            return created;
        }

        private void RegisterGlobal()
        {
            _onCombatEnd = OnScopeEnded;
            _onRunEnd = OnScopeEnded;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEnd);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEnd);

            ServiceLocator.AddService<IRoomObjectCleanupService>(this, ServiceScope.Global);
            ServiceLocator.AddService<RoomObjectCleanupService>(this, ServiceScope.Global);
        }

        public IReadOnlyList<Guid> Tracked => _tracked;

        public void Track(Guid guid)
        {
            if (guid == Guid.Empty || _tracked.Contains(guid)) return;
            _tracked.Add(guid);
        }

        public void Forget(Guid guid) => _tracked.Remove(guid);

        public void TearDownAll()
        {
            if (_tracked.Count == 0) return;

            ServiceLocator.TryGetService<IEntityVisualService>(out var visuals);
            ServiceLocator.TryGetService<IGridManager>(out var grid);
            ServiceLocator.TryGetService<AttributesManager>(out var attributes);

            foreach (var guid in _tracked)
            {
                visuals?.Despawn(guid);
                grid?.Unregister(guid);

                // Mismo cierre que usa la detonación: la ranura del nodo que los coloca mira Health,
                // así que un objeto despawneado con vida le queda ocupado a sus ojos.
                if (attributes?.GetAttribute<Health>(guid) != null)
                    attributes.SetAttributeValue<Health, int>(guid, 0);
            }

            _tracked.Clear();
        }

        private void OnScopeEnded(params object[] _) => TearDownAll();

        public void Dispose()
        {
            if (_onCombatEnd != null) EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEnd);
            if (_onRunEnd != null) EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEnd);
            _onCombatEnd = null;
            _onRunEnd = null;
            _tracked.Clear();
        }
    }
}
