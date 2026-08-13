using System;
using System.Collections.Generic;
using Patterns;

namespace Rollgeon.Combat.BossHand
{
    /// <summary>
    /// Implementación POCO de <see cref="IBossDiceHandService"/> — diccionario en memoria
    /// <c>Guid → (mano, rerolls)</c>. Main-thread only, igual que <c>WeaknessRegistry</c>.
    /// </summary>
    /// <remarks>
    /// Se resuelve vía <see cref="ResolveOrCreate"/> (lazy self-register, mismo patrón que
    /// <c>ThreatTelegraphOverlay</c>) en vez de exigir una entry en
    /// <c>ServiceBootstrap.ExtraServices</c>: el boss que la usa es contenido nuevo y no queremos
    /// que su mano dependa de wiring manual en escena para funcionar.
    /// </remarks>
    public sealed class BossDiceHandService : IBossDiceHandService, IDisposable
    {
        private readonly Dictionary<Guid, BossDiceHand> _hands = new Dictionary<Guid, BossDiceHand>();
        private readonly Dictionary<Guid, int> _rerolls = new Dictionary<Guid, int>();

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>
        /// Devuelve el servicio registrado o crea + registra uno (Global). Lazy para no depender
        /// de wiring manual en <c>ServiceBootstrap.ExtraServices</c>.
        /// </summary>
        public static IBossDiceHandService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<IBossDiceHandService>(out var existing) && existing != null)
                return existing;

            var created = new BossDiceHandService();
            created.RegisterGlobal();
            return created;
        }

        private void RegisterGlobal()
        {
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<IBossDiceHandService>(this, ServiceScope.Global);
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
            ClearAll();
        }

        // ======================================================================
        // IBossDiceHandService
        // ======================================================================

        /// <inheritdoc />
        public void SetHand(Guid ownerGuid, IReadOnlyList<int> values, string comboId, bool armed)
        {
            if (ownerGuid == Guid.Empty) return;

            // Copia propia: el caller re-tira sobre su array de trabajo entre turnos.
            int count = values?.Count ?? 0;
            var snapshot = new int[count];
            for (int i = 0; i < count; i++) snapshot[i] = values[i];

            _hands[ownerGuid] = new BossDiceHand(snapshot, comboId, armed);
        }

        /// <inheritdoc />
        public bool ArmHand(Guid ownerGuid)
        {
            if (ownerGuid == Guid.Empty) return false;
            if (!_hands.TryGetValue(ownerGuid, out var hand)) return false;
            if (hand.Armed) return false;

            _hands[ownerGuid] = new BossDiceHand(hand.Values, hand.ComboId, armed: true);
            return true;
        }

        /// <inheritdoc />
        public bool TryGetHand(Guid ownerGuid, out BossDiceHand hand)
        {
            if (ownerGuid == Guid.Empty)
            {
                hand = default;
                return false;
            }
            return _hands.TryGetValue(ownerGuid, out hand);
        }

        /// <inheritdoc />
        public void SetRerollsPerRound(Guid ownerGuid, int rerolls)
        {
            if (ownerGuid == Guid.Empty) return;
            _rerolls[ownerGuid] = rerolls < 0 ? 0 : rerolls;
        }

        /// <inheritdoc />
        public int GetRerollsPerRound(Guid ownerGuid)
            => _rerolls.TryGetValue(ownerGuid, out var value) ? value : 0;

        /// <inheritdoc />
        public void Clear(Guid ownerGuid)
        {
            _hands.Remove(ownerGuid);
            _rerolls.Remove(ownerGuid);
        }

        /// <inheritdoc />
        public void ClearAll()
        {
            _hands.Clear();
            _rerolls.Clear();
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnScopeEndedExternal(params object[] args) => ClearAll();
    }
}
