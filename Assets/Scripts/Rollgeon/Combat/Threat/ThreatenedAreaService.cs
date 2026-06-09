using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Implementación POCO de <see cref="IThreatenedAreaService"/> (Sistemas prerequisito Bosses §1).
    /// Mismo patrón que <c>ComboBlockService</c>: se registra como <see cref="IPreloadableService"/>
    /// global, la state interna es run-scoped (se limpia en <c>OnCombatEnd</c> / <c>OnRunEnd</c>).
    /// </summary>
    public sealed class ThreatenedAreaService : IThreatenedAreaService, IPreloadableService, IDisposable
    {
        // Lazy: Odin puede deserializar el servicio bypaseando el ctor (ver ComboBlockService).
        private Dictionary<Guid, ThreatenedArea> _pending;
        private Dictionary<Guid, ThreatenedArea> Pending => _pending ??= new Dictionary<Guid, ThreatenedArea>();

        private static readonly IReadOnlyCollection<GridCoord> EmptyTiles = Array.Empty<GridCoord>();

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Junto al resto de servicios de combate (ver <c>ComboBlockService.Priority</c> = 80).</summary>
        public int Priority => 80;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<IThreatenedAreaService>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }
            Pending.Clear();
        }

        // ======================================================================
        // IThreatenedAreaService
        // ======================================================================

        /// <inheritdoc />
        public void Mark(Guid sourceGuid, IEnumerable<GridCoord> tiles, int damage, AttackKind kind)
        {
            if (sourceGuid == Guid.Empty || tiles == null) return;

            var set = new HashSet<GridCoord>(tiles);
            if (set.Count == 0)
            {
                Pending.Remove(sourceGuid);
                return;
            }

            Pending[sourceGuid] = new ThreatenedArea(sourceGuid, set, damage, kind);
            EventManager.Trigger(EventName.OnThreatenedAreaMarked, sourceGuid);
        }

        /// <inheritdoc />
        public bool HasPending(Guid sourceGuid)
            => sourceGuid != Guid.Empty && Pending.ContainsKey(sourceGuid);

        /// <inheritdoc />
        public IReadOnlyCollection<GridCoord> GetPendingTiles(Guid sourceGuid)
        {
            if (sourceGuid != Guid.Empty && Pending.TryGetValue(sourceGuid, out var area) && area.Tiles != null)
                return area.Tiles;
            return EmptyTiles;
        }

        /// <inheritdoc />
        public bool TryConsume(Guid sourceGuid, out ThreatenedArea pending)
        {
            if (sourceGuid != Guid.Empty && Pending.TryGetValue(sourceGuid, out pending))
            {
                Pending.Remove(sourceGuid);
                return true;
            }
            pending = default;
            return false;
        }

        /// <inheritdoc />
        public void Clear(Guid sourceGuid)
        {
            if (sourceGuid == Guid.Empty) return;
            Pending.Remove(sourceGuid);
        }

        /// <inheritdoc />
        public void ClearAll() => Pending.Clear();

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnScopeEndedExternal(params object[] args) => ClearAll();
    }
}
