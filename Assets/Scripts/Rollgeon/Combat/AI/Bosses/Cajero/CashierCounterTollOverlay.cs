using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// Pinta el lado del mostrador que cobra peaje. Sin esto, el jugador ve una mesa larga que por
    /// algún lado se puede cruzar y no tiene forma de saber que quedarse del lado del jefe cuesta
    /// <see cref="ICashierCounterTollService.TollDamage"/> al cerrar el turno.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el único consumidor de UI que tiene el peaje.</b> El servicio existía, cobraba y
    /// animaba el golpe — pero recién <i>después</i> de cobrar. El jugador aprendía la regla
    /// perdiendo vida, sin nada que le avisara antes.
    /// </para>
    /// <para>
    /// <b>Lee posiciones vivas, igual que el cobro.</b> El lado se resuelve con las coordenadas del
    /// momento, así que el área pintada sigue al jefe si el kiteo lo mete por una abertura — que es
    /// exactamente el caso en el que un overlay horneado al armar mentiría.
    /// </para>
    /// <para>
    /// <b>La fila del mostrador no se pinta.</b> Parado en una abertura no estás de ningún lado y no
    /// pagás: <see cref="CashierCounterTollService.IsSameSide"/> devuelve <c>false</c> con
    /// <c>side == 0</c>. Pintarla convertiría el único lugar seguro de la sala en zona de peligro.
    /// </para>
    /// <para>
    /// <b>En la ronda franca desaparece.</b> El peaje cobra una ronda de cada
    /// <see cref="ICashierCounterTollService.ChargesEveryNRounds"/>, y el overlay se apaga entero en
    /// la que no cobra en vez de atenuarse: "verde = cuesta, sin verde = pasá" se lee de un vistazo
    /// y no pide comparar dos tonos del mismo color. Se repinta en
    /// <see cref="EventName.OnTurnQueueBuilt"/>, que es el evento del wrap de ronda, así que el
    /// cambio cae exactamente cuando la regla cambia.
    /// </para>
    /// </remarks>
    public sealed class CashierCounterTollOverlay : IDisposable
    {
        /// <summary>
        /// Verde fieltro de mesa, el color del cuerpo del Cajero. Distinto del naranja del telegraph
        /// y del latón del Croupier: esto no es un golpe que viene, es una regla del terreno.
        /// </summary>
        public static readonly Color TollTint = new Color(0.17f, 0.44f, 0.29f, 0.45f);

        // XOR sobre el último byte del guid del jefe, mismo truco que CroupierSectorTelegraph: una
        // fuente derivada y estable, que no pisa el área que el propio jefe marca con su columna.
        private const int OverlaySalt = 0xD0;

        private EventManager.EventReceiver _onTurnQueueBuilt;
        private EventManager.EventReceiver _onTurnStarted;
        private EventManager.EventReceiver _onScopeEnded;
        private bool _disposed;

        private Guid _paintedFor = Guid.Empty;

        public CashierCounterTollOverlay()
        {
            _onTurnQueueBuilt = Repaint;
            _onTurnStarted = Repaint;
            _onScopeEnded = ClearExternal;

            // Se repinta al arrancar cada turno y cada ronda, y no en cada movimiento: el jefe se
            // mueve dentro de su propio turno (su KeepDistance lo puede cruzar de lado), así que el
            // OnTurnStarted del jugador —que llega inmediatamente después— ya lo agarra, y es
            // justo el instante en que el jugador necesita ver el lado para decidir a dónde va.
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, _onTurnQueueBuilt);
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStarted);
            EventManager.Subscribe(EventName.OnCombatEnd, _onScopeEnded);
            EventManager.Subscribe(EventName.OnRunEnd, _onScopeEnded);
        }

        /// <summary>Devuelve el registrado o crea y registra uno nuevo (Global).</summary>
        public static CashierCounterTollOverlay ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<CashierCounterTollOverlay>(out var existing) && existing != null)
                return existing;

            var created = new CashierCounterTollOverlay();
            ServiceLocator.AddService<CashierCounterTollOverlay>(created, ServiceScope.Global);
            return created;
        }

        /// <summary>Fuente del overlay derivada del jefe. Pública para asserts de tests.</summary>
        public static Guid OverlayGuid(Guid bossGuid)
        {
            if (bossGuid == Guid.Empty) return Guid.Empty;

            var bytes = bossGuid.ToByteArray();
            bytes[15] = (byte)(bytes[15] ^ OverlaySalt);
            return new Guid(bytes);
        }

        // ======================================================================
        // Pintado
        // ======================================================================

        /// <summary>
        /// Recalcula y repinta el lado del jefe. Público para que los tests no dependan de qué
        /// evento lo dispara.
        /// </summary>
        public void Repaint(params object[] args)
        {
            if (_disposed) return;

            if (!TryResolveSide(out var bossGuid, out var tiles))
            {
                Clear();
                return;
            }

            var source = OverlayGuid(bossGuid);
            ThreatTelegraphOverlay.ResolveOrCreate().Show(source, tiles, TollTint);
            _paintedFor = source;
        }

        /// <summary>Baja el overlay. Idempotente.</summary>
        public void Clear()
        {
            if (_paintedFor == Guid.Empty) return;

            // TryGet y no ResolveOrCreate: limpiar no debe ser la razón por la que nace un overlay.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(_paintedFor);

            _paintedFor = Guid.Empty;
        }

        /// <summary>
        /// Las casillas del lado del jefe, o <c>false</c> si no hay nada que pintar (peaje sin armar,
        /// jefe muerto, sala sin bounds).
        /// </summary>
        /// <remarks>
        /// Separado del pintado para poder testear la geometría sin overlay ni GameObjects: la regla
        /// del lado es lo único que este componente decide.
        /// </remarks>
        public static bool TryResolveSide(out Guid bossGuid, out HashSet<GridCoord> tiles)
        {
            bossGuid = Guid.Empty;
            tiles = null;

            if (!ServiceLocator.TryGetService<ICashierCounterTollService>(out var toll) || toll == null) return false;

            // ChargesThisRound y no IsArmed: el peaje cobra una ronda de cada dos, y en la franca no
            // se pinta nada. Es lo único que le dice al jugador cuándo puede cruzar — un lado verde
            // que a veces no cobra le enseña a desconfiar del overlay, que es peor que no tenerlo.
            if (!toll.ChargesThisRound) return false;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;

            // Sin coordenada del jefe no hay lado: CombatDeathWatcher lo saca de la grilla al morir,
            // así que esto es también lo que apaga el overlay solo al terminar la pelea.
            bossGuid = toll.BossGuid;
            if (bossGuid == Guid.Empty || !grid.TryGetPosition(bossGuid, out var bossCoord)) return false;

            tiles = new HashSet<GridCoord>();
            foreach (var cell in ThreatAreaShape.RoomTiles(grid))
            {
                if (CashierCounterTollService.IsSameSide(cell.Y, bossCoord.Y, toll.CounterRow))
                    tiles.Add(cell);
            }
            return tiles.Count > 0;
        }

        // ======================================================================
        // Lifecycle
        // ======================================================================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Unsubscribe(EventName.OnTurnQueueBuilt, ref _onTurnQueueBuilt);
            Unsubscribe(EventName.OnTurnStarted, ref _onTurnStarted);

            if (_onScopeEnded != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onScopeEnded);
                EventManager.UnSubscribe(EventName.OnRunEnd, _onScopeEnded);
                _onScopeEnded = null;
            }
            Clear();
        }

        private static void Unsubscribe(EventName name, ref EventManager.EventReceiver handler)
        {
            if (handler == null) return;
            EventManager.UnSubscribe(name, handler);
            handler = null;
        }

        private void ClearExternal(params object[] args) => Clear();
    }
}
