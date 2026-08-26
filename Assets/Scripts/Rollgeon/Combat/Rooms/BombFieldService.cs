using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Rooms
{
    /// <summary>
    /// La mecha de las bombas: qué bombas hay en pie, con qué cruz y cuántos turnos les faltan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existe por una razón concreta: <b>sembrar y detonar tienen que caer en ticks distintos</b>. Con
    /// las dos cosas en el mismo tick la mecha vale exactamente el intervalo con el que se tickea el
    /// nodo —un ciclo entero del jefe— y no hay forma de acortarla. Partido en dos nodos, el estado
    /// no puede vivir en ninguno de los dos, así que vive acá. Mismo criterio que
    /// <c>CroupierWheelService</c>.
    /// </para>
    /// <para>
    /// La vida es la autoridad, no este registro: una bomba que el jugador rompió a mano se descarta
    /// en el próximo <see cref="TickFuses"/> sin dejar fuego. Por eso el chequeo de <see cref="Health"/>
    /// pasa acá y no en el nodo que siembra.
    /// </para>
    /// </remarks>
    public interface IBombFieldService
    {
        /// <summary>
        /// Registra una bomba recién sembrada con su cruz y su mecha en turnos. <b>No-op si ya está
        /// armada</b>: re-sembrar no le refresca el plazo.
        /// </summary>
        /// <returns>
        /// La cruz que la bomba tiene <b>de verdad</b> — la de <paramref name="cross"/> si se armó
        /// acá, o la que ya tenía si estaba armada. Es lo que hay que pintar: la siembra vuelve a
        /// pasar por las que siguen en pie, y las formas rotan, así que marcar la que se le pasó le
        /// dibujaría a una bomba vieja el aspa de la generación nueva. <c>null</c> si no se registró.
        /// </returns>
        IReadOnlyList<GridCoord> Sow(Guid bombGuid, IReadOnlyList<GridCoord> cross, int fuseTurns);

        /// <summary>
        /// Descuenta un turno a cada mecha y reparte el resultado: las que llegaron a cero
        /// (<paramref name="due"/>) y las que el jugador rompió antes (<paramref name="broken"/>).
        /// Las dos salen del registro; sólo las primeras dejan fuego.
        /// </summary>
        void TickFuses(
            AttributesManager attributes,
            List<(Guid Guid, IReadOnlyList<GridCoord> Cross)> due,
            List<(Guid Guid, IReadOnlyList<GridCoord> Cross)> broken);

        /// <summary>Las que siguen en pie, para que el nodo que siembra sepa cuántas reponer.</summary>
        IEnumerable<(Guid Guid, IReadOnlyList<GridCoord> Cross)> Live(AttributesManager attributes);

        /// <summary>
        /// Turnos que le quedan a la mecha de <paramref name="bombGuid"/>. <c>1</c> = estalla en
        /// el próximo turno del jefe.
        /// </summary>
        bool TryGetFuse(Guid bombGuid, out int fuse);

        void Clear();
    }

    /// <inheritdoc cref="IBombFieldService"/>
    public sealed class BombFieldService : IBombFieldService, IDisposable
    {
        private readonly Dictionary<Guid, Entry> _bombs = new Dictionary<Guid, Entry>();

        private EventManager.EventReceiver _onCombatEnd;
        private EventManager.EventReceiver _onRunEnd;

        public static IBombFieldService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<IBombFieldService>(out var existing) && existing != null)
                return existing;

            var created = new BombFieldService();
            created.RegisterGlobal();
            return created;
        }

        private void RegisterGlobal()
        {
            _onCombatEnd = OnScopeEnded;
            _onRunEnd = OnScopeEnded;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEnd);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEnd);

            ServiceLocator.AddService<IBombFieldService>(this, ServiceScope.Global);
            ServiceLocator.AddService<BombFieldService>(this, ServiceScope.Global);
        }

        public IReadOnlyList<GridCoord> Sow(Guid bombGuid, IReadOnlyList<GridCoord> cross, int fuseTurns)
        {
            if (bombGuid == Guid.Empty || cross == null || cross.Count == 0) return null;

            // Una bomba ya armada NO se re-arma: el nodo que siembra vuelve a pasar por las que
            // siguen en pie cada vez que le toca su tiempo, y refrescarles la mecha las volvería
            // eternas. La mecha la fija quien la plantó, una sola vez — y con ella su cruz.
            if (_bombs.TryGetValue(bombGuid, out var armed)) return armed.Cross;

            // Mínimo 1 y no 0: con 0 la bomba nace ya vencida y detona antes de que el jugador tenga
            // un turno para mirarla.
            _bombs[bombGuid] = new Entry(cross, Math.Max(1, fuseTurns));
            return cross;
        }

        public void TickFuses(
            AttributesManager attributes,
            List<(Guid Guid, IReadOnlyList<GridCoord> Cross)> due,
            List<(Guid Guid, IReadOnlyList<GridCoord> Cross)> broken)
        {
            if (_bombs.Count == 0) return;

            // Se recorre sobre una copia de las keys: los dos casos de abajo sacan del diccionario.
            var guids = new List<Guid>(_bombs.Keys);
            foreach (var guid in guids)
            {
                var entry = _bombs[guid];

                var health = attributes?.GetAttribute<Health>(guid);
                if (health == null || health.Value <= 0)
                {
                    broken?.Add((guid, entry.Cross));
                    _bombs.Remove(guid);
                    continue;
                }

                entry.Fuse--;
                if (entry.Fuse > 0)
                {
                    _bombs[guid] = entry;
                    continue;
                }

                due?.Add((guid, entry.Cross));
                _bombs.Remove(guid);
            }
        }

        public IEnumerable<(Guid Guid, IReadOnlyList<GridCoord> Cross)> Live(AttributesManager attributes)
        {
            foreach (var kvp in _bombs)
            {
                var health = attributes?.GetAttribute<Health>(kvp.Key);
                if (health == null || health.Value <= 0) continue;
                yield return (kvp.Key, kvp.Value.Cross);
            }
        }

        /// <summary>
        /// Turnos que le quedan a la mecha de <paramref name="bombGuid"/>. La cuenta la baja
        /// <c>TickFuses</c>, así que <c>1</c> significa "estalla en el próximo turno del jefe".
        /// </summary>
        /// <remarks>
        /// Aparte de <c>Live</c> a propósito: esa promete crucecitas y nada más, y tiene callers
        /// que no quieren saber de la mecha.
        /// </remarks>
        public bool TryGetFuse(Guid bombGuid, out int fuse)
        {
            if (_bombs.TryGetValue(bombGuid, out var entry))
            {
                fuse = entry.Fuse;
                return true;
            }
            fuse = 0;
            return false;
        }

        public void Clear() => _bombs.Clear();

        private void OnScopeEnded(params object[] _) => Clear();

        public void Dispose()
        {
            if (_onCombatEnd != null) EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEnd);
            if (_onRunEnd != null) EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEnd);
            _onCombatEnd = null;
            _onRunEnd = null;
            _bombs.Clear();
        }

        private struct Entry
        {
            public readonly IReadOnlyList<GridCoord> Cross;
            public int Fuse;

            public Entry(IReadOnlyList<GridCoord> cross, int fuse)
            {
                Cross = cross;
                Fuse = fuse;
            }
        }
    }
}
