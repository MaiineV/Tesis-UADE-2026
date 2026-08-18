using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// Pinta el lado del mostrador que cobra <see cref="ICashierCounterTollService.TollDamage"/> a
    /// quien cierre el turno ahí.
    /// </summary>
    /// <remarks>
    /// Lee posiciones vivas, igual que el cobro: un overlay horneado al armar mentiría en cuanto el
    /// kiteo mete al jefe por una abertura. La fila del mostrador no se pinta —
    /// <see cref="CashierCounterTollService.IsSameSide"/> devuelve <c>false</c> con <c>side == 0</c>,
    /// así que pararse en una abertura no cuesta nunca.
    /// </remarks>
    public sealed class CashierCounterTollOverlay : IDisposable
    {
        /// <summary>
        /// Verde fieltro de mesa. Distinto del naranja del telegraph: esto no es un golpe que viene,
        /// es una regla del terreno.
        /// </summary>
        public static readonly Color TollTint = new Color(0.17f, 0.44f, 0.29f, 0.45f);

        // XOR sobre el último byte del guid del jefe: fuente derivada y estable que no pisa el área
        // que el propio jefe marca con su columna.
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

            // Por turno y por ronda, no por movimiento: el jefe cambia de lado dentro de su propio
            // turno, y el OnTurnStarted del jugador —el siguiente— ya lo agarra actualizado.
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

            // ChargesThisRound y no IsArmed: si la cadencia deja una ronda franca, un lado verde que
            // no cobra enseña a desconfiar del overlay.
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
