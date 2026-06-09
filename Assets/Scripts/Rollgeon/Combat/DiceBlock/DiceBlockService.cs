using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;

namespace Rollgeon.Combat.DiceBlock
{
    /// <summary>
    /// Implementación POCO de <see cref="IDiceBlockService"/> (Sistemas prerequisito Bosses §2).
    /// Calca el lifecycle de <c>ComboBlockService</c>: registro global, state run-scoped, y
    /// auto-release suscribiéndose a <see cref="EventName.OnTurnFinished"/> filtrado por el player.
    /// </summary>
    public sealed class DiceBlockService : IDiceBlockService, IPreloadableService, IDisposable
    {
        // Lazy por la misma razón que ComboBlockService (Odin bypassea ctor al deserializar).
        private HashSet<int> _blocked;
        private HashSet<int> Blocked => _blocked ??= new HashSet<int>();

        private EventManager.EventReceiver _onTurnFinishedHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        private Func<Guid> _playerGuidResolver;

        public int Priority => 80;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _playerGuidResolver = DefaultPlayerGuidResolver;
            SubscribeHandlers();
            ServiceLocator.AddService<IDiceBlockService>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests — inyecta el resolver del player guid.</summary>
        public void ConfigureForTests(Func<Guid> playerGuidResolver)
        {
            _playerGuidResolver = playerGuidResolver ?? DefaultPlayerGuidResolver;
            SubscribeHandlers();
        }

        private void SubscribeHandlers()
        {
            _onTurnFinishedHandler = OnTurnFinishedExternal;
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        public void Dispose()
        {
            if (_onTurnFinishedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
                _onTurnFinishedHandler = null;
            }
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
            Blocked.Clear();
        }

        // ======================================================================
        // IDiceBlockService
        // ======================================================================

        /// <inheritdoc />
        public void Block(int index)
        {
            if (index < 0) return;
            if (Blocked.Add(index))
                EventManager.Trigger(EventName.OnDiceBlockChanged, ResolvePlayerGuid());
        }

        /// <inheritdoc />
        public void Unblock(int index)
        {
            if (Blocked.Remove(index))
                EventManager.Trigger(EventName.OnDiceBlockChanged, ResolvePlayerGuid());
        }

        /// <inheritdoc />
        public bool IsBlocked(int index) => index >= 0 && Blocked.Contains(index);

        /// <inheritdoc />
        public IReadOnlyCollection<int> BlockedIndices => Blocked;

        /// <inheritdoc />
        public void Clear()
        {
            if (Blocked.Count == 0) return;
            Blocked.Clear();
            EventManager.Trigger(EventName.OnDiceBlockChanged, ResolvePlayerGuid());
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnTurnFinishedExternal(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid turnGuid)) return;
            var playerGuid = ResolvePlayerGuid();
            if (playerGuid == Guid.Empty || turnGuid != playerGuid) return;
            Clear();
        }

        private void OnScopeEndedExternal(params object[] args) => Clear();

        private Guid ResolvePlayerGuid()
            => _playerGuidResolver != null ? _playerGuidResolver() : Guid.Empty;

        private static Guid DefaultPlayerGuidResolver()
        {
            if (ServiceLocator.TryGetService<IPlayerService>(out var svc) && svc != null)
                return svc.PlayerGuid;
            return Guid.Empty;
        }
    }
}
