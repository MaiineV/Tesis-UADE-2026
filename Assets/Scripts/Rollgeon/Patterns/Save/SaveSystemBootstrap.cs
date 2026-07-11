using System;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Patterns.Save
{
    /// <summary>
    /// Conecta el <see cref="SaveSystem"/> al ciclo de vida real del juego (§15.1):
    /// cada trigger captura el estado completo y flushea (el flush sólo escribe si
    /// <see cref="SaveSettingsSO.FlushOn"/> habilita ese trigger).
    /// <para>
    /// <b>Prioridad.</b> <see cref="DefaultPriority"/> = 200 — último entre los
    /// ExtraServices: <c>EventManager</c> invoca handlers en orden de suscripción, así
    /// que la captura de <c>OnRunStart</c> corre después de que los servicios
    /// run-scoped (combo counters, unlock tracker) crearon y registraron su estado.
    /// </para>
    /// <para>
    /// <b>No llama <c>SaveSystem.Clear()</c>.</b> El clear de run nueva vive como
    /// primera línea de <c>RunBootstrapper.StartRun</c> — acá ya sería tarde: los
    /// <c>Register()</c> de los servicios run-scoped ocurren antes de OnRunStart y
    /// auto-restaurarían estado de la run anterior.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SaveSystemBootstrap : IPreloadableService, IDisposable
    {
        public const int DefaultPriority = 200;

        [NonSerialized] private bool _subscribed;
        [NonSerialized] private bool _runActive;
        [NonSerialized] private EventManager.EventReceiver _onRunStart;
        [NonSerialized] private EventManager.EventReceiver _onFloorChanged;
        [NonSerialized] private EventManager.EventReceiver _onRoomEntered;
        [NonSerialized] private EventManager.EventReceiver _onRunEnd;

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        // ====================================================================
        // IPreloadableService
        // ====================================================================

        /// <inheritdoc />
        public void Register()
        {
            if (_subscribed) return;
            _subscribed = true;

            _onRunStart = _ => { _runActive = true; CaptureAndFlush(SaveTrigger.RunStart); };
            _onFloorChanged = _ => CaptureAndFlush(SaveTrigger.FloorEnd);
            _onRoomEntered = _ => CaptureAndFlush(SaveTrigger.RoomEnd);
            _onRunEnd = _ => { CaptureAndFlush(SaveTrigger.RunEnd); _runActive = false; };

            EventManager.Subscribe(EventName.OnRunStart, _onRunStart);
            EventManager.Subscribe(EventName.OnFloorChanged, _onFloorChanged);
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEntered);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEnd);

            // En editor, wantsToQuit también dispara al frenar Play Mode — inocuo:
            // captura + flush de estado consistente.
            Application.wantsToQuit += OnWantsToQuit;

            ServiceLocator.AddService<SaveSystemBootstrap>(this, ServiceScope.Global);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_subscribed) return;
            _subscribed = false;

            EventManager.UnSubscribe(EventName.OnRunStart, _onRunStart);
            EventManager.UnSubscribe(EventName.OnFloorChanged, _onFloorChanged);
            EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEntered);
            EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEnd);
            _onRunStart = _onFloorChanged = _onRoomEntered = _onRunEnd = null;

            Application.wantsToQuit -= OnWantsToQuit;
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static void CaptureAndFlush(SaveTrigger trigger)
        {
            SaveSystem.CaptureAll();
            SaveSystem.Flush(trigger);
        }

        private bool OnWantsToQuit()
        {
            // Solo con run activa: el Exit flush existe para preservar progreso
            // mid-run. En el menú (post victoria/derrota, save ya borrado por
            // EndRun) escribir acá recrearía un save inválido y re-encendería el
            // Continue. Exit siempre flushea sincrono (§15.3.1) — nunca bloquea.
            if (_runActive)
            {
                CaptureAndFlush(SaveTrigger.Exit);
            }
            return true;
        }
    }
}
