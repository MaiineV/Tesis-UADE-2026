using System;
using System.Collections.Generic;
using Patterns;

namespace Rollgeon.Combat
{
    /// <inheritdoc cref="IShieldPersistenceService"/>
    /// <remarks>
    /// POCO simple, sin <c>IPreloadableService</c>: a diferencia de Poison/Bleed/Stun (que
    /// viven en <c>ExtraServices</c> desde el arranque), este servicio nace y muere con la
    /// run — lo instancia <c>RunController</c> junto al <see cref="ShieldResetHandler"/> que
    /// lo consume, mismo ciclo de vida que <c>ShieldResetHandler</c> ya tiene.
    /// </remarks>
    public sealed class ShieldPersistenceService : IShieldPersistenceService, IDisposable
    {
        private readonly HashSet<Guid> _marked = new HashSet<Guid>();

        private EventManager.EventReceiver _onCombatEndHandler;

        public ShieldPersistenceService()
        {
            _onCombatEndHandler = OnCombatEndExternal;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
        }

        public void Dispose()
        {
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            _marked.Clear();
        }

        /// <inheritdoc />
        public void PersistThroughNextReset(Guid entity)
        {
            if (entity == Guid.Empty) return;
            if (_marked.Add(entity))
                EventManager.Trigger(EventName.OnShieldPersisted, entity);
        }

        /// <inheritdoc />
        public bool TryConsume(Guid entity)
        {
            if (entity == Guid.Empty) return false;
            return _marked.Remove(entity);
        }

        /// <inheritdoc />
        public bool IsPersisted(Guid entity) => entity != Guid.Empty && _marked.Contains(entity);

        /// <inheritdoc />
        public void ClearAll() => _marked.Clear();

        private void OnCombatEndExternal(params object[] args) => ClearAll();
    }
}
