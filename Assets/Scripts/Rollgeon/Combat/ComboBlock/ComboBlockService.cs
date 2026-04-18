using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.ComboBlock
{
    /// <summary>
    /// Implementacion POCO (no <see cref="MonoBehaviour"/>) de <see cref="IComboBlockService"/>.
    /// Content#0103, plan §4.2. Se registra como <see cref="IPreloadableService"/> global —
    /// la state interna es run-scoped (se limpia en <c>OnCombatEnd</c> / <c>OnRunEnd</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hook de TickDuration.</b> Se suscribe a <see cref="EventName.OnTurnFinished"/>
    /// (CombatTurnFSM — T100d). Cuando el Guid del evento coincide con
    /// <c>IPlayerService.PlayerGuid</c>, llama <see cref="TickDuration"/>. Si el servicio
    /// de player no esta registrado, <see cref="TickDuration"/> nunca corre (comportamiento
    /// defensivo — el bloqueo simplemente no expira). Cuando F#0008 mergee y haya un
    /// <c>PlayerGuid</c> estable, el tick funciona automaticamente.
    /// </para>
    /// <para>
    /// <b>Lifecycle events.</b> Tambien se suscribe a <see cref="EventName.OnCombatEnd"/>
    /// y <see cref="EventName.OnRunEnd"/> → <see cref="Clear"/> (sin disparar
    /// <c>OnComboUnblocked</c>, por diseno).
    /// </para>
    /// </remarks>
    public sealed class ComboBlockService : IComboBlockService, IPreloadableService, IDisposable
    {
        private readonly Dictionary<string, int> _remainingTurns = new Dictionary<string, int>();

        private EventManager.EventReceiver _onTurnFinishedHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>
        /// Resolver del PlayerGuid; en produccion delega a
        /// <c>ServiceLocator.TryGetService&lt;IPlayerService&gt;()</c>. Inyectable en tests
        /// para forzar un Guid sin tener que montar un fake service.
        /// </summary>
        private Func<Guid> _playerGuidResolver;

        /// <summary>Despues de core services, antes de behaviors.</summary>
        public int Priority => 80;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _playerGuidResolver = DefaultPlayerGuidResolver;

            _onTurnFinishedHandler = OnTurnFinishedExternal;
            _onCombatEndHandler = OnCombatEndExternal;
            _onRunEndHandler = OnRunEndExternal;

            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<IComboBlockService>(this, ServiceScope.Global);
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
            _remainingTurns.Clear();
        }

        // ======================================================================
        // Test hook
        // ======================================================================

        /// <summary>
        /// Hook para EditMode tests: suscribe los handlers y permite inyectar un
        /// <paramref name="playerGuidResolver"/> alternativo (null → default que consulta
        /// <c>IPlayerService</c>).
        /// </summary>
        public void ConfigureForTests(Func<Guid> playerGuidResolver)
        {
            _playerGuidResolver = playerGuidResolver ?? DefaultPlayerGuidResolver;

            _onTurnFinishedHandler = OnTurnFinishedExternal;
            _onCombatEndHandler = OnCombatEndExternal;
            _onRunEndHandler = OnRunEndExternal;

            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        // ======================================================================
        // IComboBlockService
        // ======================================================================

        /// <inheritdoc />
        public void Block(string comboId, int durationTurns)
        {
            if (string.IsNullOrEmpty(comboId)) return;
            if (durationTurns <= 0) return;

            if (_remainingTurns.TryGetValue(comboId, out var current))
            {
                if (durationTurns > current) _remainingTurns[comboId] = durationTurns;
            }
            else
            {
                _remainingTurns[comboId] = durationTurns;
            }

            EventManager.Trigger(EventName.OnComboBlocked, comboId, durationTurns);
        }

        /// <inheritdoc />
        public bool IsBlocked(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return false;
            return _remainingTurns.TryGetValue(comboId, out var n) && n > 0;
        }

        /// <inheritdoc />
        public int GetRemainingTurns(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return 0;
            return _remainingTurns.TryGetValue(comboId, out var n) ? n : 0;
        }

        /// <inheritdoc />
        public void TickDuration()
        {
            if (_remainingTurns.Count == 0) return;

            // Snapshot keys — vamos a mutar el diccionario durante el loop.
            var keys = new List<string>(_remainingTurns.Keys);
            var unblocked = new List<string>();
            foreach (var id in keys)
            {
                int remaining = _remainingTurns[id] - 1;
                if (remaining <= 0)
                {
                    _remainingTurns.Remove(id);
                    unblocked.Add(id);
                }
                else
                {
                    _remainingTurns[id] = remaining;
                }
            }

            foreach (var id in unblocked)
            {
                EventManager.Trigger(EventName.OnComboUnblocked, id);
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            _remainingTurns.Clear();
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, int> ActiveBlocks => _remainingTurns;

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnTurnFinishedExternal(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if (!(args[0] is Guid turnGuid)) return;
            if (_playerGuidResolver == null) return;
            var playerGuid = _playerGuidResolver();
            if (playerGuid == Guid.Empty || turnGuid != playerGuid) return;
            TickDuration();
        }

        private void OnCombatEndExternal(params object[] args) => Clear();
        private void OnRunEndExternal(params object[] args) => Clear();

        private static Guid DefaultPlayerGuidResolver()
        {
            if (ServiceLocator.TryGetService<IPlayerService>(out var svc) && svc != null)
            {
                return svc.PlayerGuid;
            }
            return Guid.Empty;
        }
    }
}
