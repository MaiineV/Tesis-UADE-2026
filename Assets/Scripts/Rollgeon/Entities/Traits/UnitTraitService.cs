using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Entities.Traits
{
    /// <summary>
    /// Implementación POCO de <see cref="IUnitTraitService"/>: un dict Guid → traits,
    /// registro global vía <see cref="IPreloadableService"/>.
    /// </summary>
    /// <remarks>
    /// El estado se limpia en <c>OnRunEnd</c>, NO por combate: limpiar en <c>OnCombatEnd</c>
    /// borraría los traits del player a mitad de run (se registran una sola vez al spawn del
    /// hero). Los guids de enemigos son únicos por spawn, así que las entries huérfanas de
    /// combates pasados son inertes y acotadas a la run.
    /// </remarks>
    public sealed class UnitTraitService : IUnitTraitService, IPreloadableService, IDisposable
    {
        // Lazy: si Odin deserializa este servicio desde una lista polimórfica bypassea el ctor.
        private Dictionary<Guid, UnitTraits> _traits;
        private Dictionary<Guid, UnitTraits> Traits => _traits ??= new Dictionary<Guid, UnitTraits>();

        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Antes de Grid (75): no depende de nadie y los spawners corren mucho después.</summary>
        public int Priority => 74;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            SubscribeHandlers();

            ServiceLocator.AddService<IUnitTraitService>(this, ServiceScope.Global);
            // También por tipo concreto, como el resto de los POCO services.
            ServiceLocator.AddService<UnitTraitService>(this, ServiceScope.Global);
        }

        private void SubscribeHandlers()
        {
            // Idempotencia: Register puede correr más de una vez sobre la misma instancia.
            UnsubscribeHandlers();

            _onRunEndHandler = OnRunEndedExternal;
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        private void UnsubscribeHandlers()
        {
            if (_onRunEndHandler == null) return;
            EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
            _onRunEndHandler = null;
        }

        public void Dispose()
        {
            UnsubscribeHandlers();
            Traits.Clear();
        }

        // ======================================================================
        // IUnitTraitService
        // ======================================================================

        /// <inheritdoc />
        public void Register(Guid entity, UnitTraits traits)
        {
            if (entity == Guid.Empty) return;
            Traits[entity] = traits;
        }

        /// <inheritdoc />
        public void Unregister(Guid entity)
        {
            if (entity == Guid.Empty) return;
            Traits.Remove(entity);
        }

        /// <inheritdoc />
        public UnitTraits Get(Guid entity)
        {
            if (entity == Guid.Empty) return UnitTraits.DefaultGround;
            return Traits.TryGetValue(entity, out var traits) ? traits : UnitTraits.DefaultGround;
        }

        /// <inheritdoc />
        public bool TryGet(Guid entity, out UnitTraits traits)
        {
            if (entity != Guid.Empty && Traits.TryGetValue(entity, out traits)) return true;
            traits = UnitTraits.DefaultGround;
            return false;
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnRunEndedExternal(params object[] args) => Traits.Clear();
    }
}
