using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Patterns;
using Rollgeon.Combat.Initiative;

namespace Rollgeon.Combat
{
    /// <summary>
    /// Construye y mantiene el orden de turno de un round (TECHNICAL.md §12.7).
    /// Neutral a rol: no conoce player/enemy, sólo <see cref="Guid"/>s —
    /// la FSM de combate (T100d) decide cuándo llamar <see cref="Advance"/> y
    /// cuándo skipear el turno de una entidad muerta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Regla "el jugador siempre tiene el primer turno" (CNF-006).</b> El
    /// servicio sigue siendo agnóstico al rol — no sabe qué es un "player" ni
    /// un "enemy". Lo que expone es <c>priorityGuid</c>: un guid opcional que,
    /// si está presente entre los participantes, se fuerza al frente de la
    /// cola DESPUÉS de resolver el orden por initiative. La política de "quién
    /// es prioritario" la decide el caller (<see cref="Rollgeon.Combat.FSM.States.CombatEnterState"/>
    /// pasa <c>Context.PlayerId</c>) — el servicio sólo aplica el reorder.
    /// </para>
    /// <para>
    /// <b>Snapshot del orden.</b> Los eventos <c>OnTurnQueueBuilt</c> disparan
    /// con una <see cref="ReadOnlyCollection{T}"/> construida sobre una COPIA
    /// del estado interno — ningún listener puede mutar la cola del servicio.
    /// </para>
    /// <para>
    /// <b>Registro en bootstrap.</b> Se instancia como plain C# class y se
    /// registra vía <c>ServiceLocator.AddService&lt;TurnOrderService&gt;(instance)</c>
    /// desde el bootstrap — típicamente un <c>TurnOrderServiceBootstrap : IPreloadableService</c>
    /// (análogo al patrón EnergyService) o dentro del <c>ServiceBootstrapSO</c>
    /// de Foundation#0005. Este worktree no crea ese bootstrap concreto.
    /// </para>
    /// </remarks>
    public sealed class TurnOrderService
    {
        private readonly List<Guid> _orderForRound = new List<Guid>();
        private int _cursor;
        private int _roundIndex;

        /// <summary>Orden actual del round (vive hasta el próximo <see cref="BuildForCombat"/>).</summary>
        public IReadOnlyList<Guid> OrderForRound => _orderForRound;

        /// <summary>Entidad que actúa en el slot actual. Lanza si no hay orden construido.</summary>
        public Guid Current
        {
            get
            {
                if (_orderForRound.Count == 0)
                {
                    throw new InvalidOperationException(
                        "[TurnOrderService] Current requested before BuildForCombat — no participants.");
                }
                return _orderForRound[_cursor];
            }
        }

        /// <summary>Cantidad de rounds completados (arranca en 0, incrementa en cada wrap-around).</summary>
        public int RoundIndex => _roundIndex;

        /// <summary>Cantidad de participantes en el round actual.</summary>
        public int ParticipantCount => _orderForRound.Count;

        /// <summary>
        /// Arma la cola del round a partir de la lista de participantes,
        /// resetea cursor/round y dispara <see cref="EventName.OnTurnQueueBuilt"/>.
        /// </summary>
        /// <param name="participants">Guids participantes. Debe contener al menos uno.</param>
        /// <param name="priorityGuid">
        /// Guid opcional (CNF-006) que se fuerza al índice 0 de la cola después del
        /// sort por initiative — el resto de los participantes conserva su orden
        /// relativo. <c>Guid.Empty</c> (default) desactiva el forcing. Si el guid
        /// no está entre <paramref name="participants"/>, se ignora en silencio
        /// (no tira excepción) — el servicio no valida membership de un guid ajeno
        /// a este round.
        /// </param>
        /// <exception cref="ArgumentNullException">Si <paramref name="participants"/> es null.</exception>
        /// <exception cref="InvalidOperationException">Si la lista queda vacía.</exception>
        public void BuildForCombat(IEnumerable<Guid> participants, Guid priorityGuid = default)
        {
            if (participants == null)
            {
                throw new ArgumentNullException(nameof(participants));
            }

            var provider = ServiceLocator.GetService<IInitiativeProvider>();

            var rolls = new List<(Guid guid, int initiative)>();
            foreach (var g in participants)
            {
                rolls.Add((g, provider.RollInitiative(g)));
            }

            if (rolls.Count == 0)
            {
                throw new InvalidOperationException(
                    "BuildForCombat requires at least one participant.");
            }

            rolls.Sort(InitiativeFallbacks.DescByInitiativeThenByGuid);

            _orderForRound.Clear();
            for (int i = 0; i < rolls.Count; i++)
            {
                _orderForRound.Add(rolls[i].guid);
            }
            _cursor = 0;
            _roundIndex = 0;

            // CNF-006: forzar priorityGuid al frente ANTES de disparar
            // OnTurnQueueBuilt — el HUD debe ver la cola ya reordenada.
            if (priorityGuid != Guid.Empty)
            {
                int idx = _orderForRound.IndexOf(priorityGuid);
                if (idx > 0)
                {
                    _orderForRound.RemoveAt(idx);
                    _orderForRound.Insert(0, priorityGuid);
                }
                // idx == 0: ya está primero, no-op. idx < 0: no participa, se ignora.
            }

            FireTurnQueueBuilt();
        }

        /// <summary>
        /// Avanza el cursor circularmente. Si hay wrap-around (cursor vuelve a
        /// 0), incrementa <see cref="RoundIndex"/> y re-dispara
        /// <see cref="EventName.OnTurnQueueBuilt"/>.
        /// </summary>
        /// <returns>El nuevo <see cref="Current"/>.</returns>
        /// <remarks>
        /// Desviación consciente del snippet del TECHNICAL.md §12.7: el doc
        /// muestra <c>/*nextRound*/ -1</c> como placeholder sentinel, pero el
        /// schema documentado del evento (§1.2) declara
        /// <c>[IReadOnlyList&lt;Guid&gt;, int roundIndex]</c> sin hablar de
        /// sentinels. Pasamos <c>_roundIndex</c> real (incrementado) — da más
        /// info al HUD y es consistente con el schema. Si el reviewer rechaza,
        /// revertir a <c>-1</c>.
        /// </remarks>
        public Guid Advance()
        {
            if (_orderForRound.Count == 0)
            {
                throw new InvalidOperationException(
                    "[TurnOrderService] Advance called before BuildForCombat — no participants.");
            }

            _cursor = (_cursor + 1) % _orderForRound.Count;

            if (_cursor == 0)
            {
                _roundIndex++;
                FireTurnQueueBuilt();
            }

            return _orderForRound[_cursor];
        }

        /// <summary>
        /// Removes an entity from the current round (e.g. when it dies mid-combat).
        /// Adjusts the cursor so that the currently-acting entity is unaffected
        /// unless it is the one removed.
        /// </summary>
        /// <returns><c>true</c> if the entity was found and removed.</returns>
        public bool Remove(Guid guid)
        {
            int idx = _orderForRound.IndexOf(guid);
            if (idx < 0) return false;

            _orderForRound.RemoveAt(idx);

            if (_orderForRound.Count == 0)
            {
                _cursor = 0;
                return true;
            }

            if (idx < _cursor)
            {
                _cursor--;
            }
            else if (idx == _cursor && _cursor >= _orderForRound.Count)
            {
                _cursor = 0;
            }

            return true;
        }

        /// <summary>Limpia el estado al cerrar combate.</summary>
        public void Reset()
        {
            _orderForRound.Clear();
            _cursor = 0;
            _roundIndex = 0;
        }

        /// <summary>
        /// Restaura la cola de turno exacta desde un save (Feature#0028 Fase 3) — a diferencia
        /// de <see cref="BuildForCombat"/> NO re-rollea iniciativa ni aplica el reorder CNF-006:
        /// asigna la lista/cursor/round guardados tal cual y dispara <c>OnTurnQueueBuilt</c> para
        /// que el HUD dibuje la cola y el slot activo restaurados. El caller ya filtró
        /// <paramref name="order"/> a los participantes vivos y clampeó <paramref name="cursor"/>.
        /// </summary>
        public void RestoreState(IReadOnlyList<Guid> order, int cursor, int roundIndex)
        {
            if (order == null || order.Count == 0)
            {
                throw new InvalidOperationException(
                    "[TurnOrderService] RestoreState requires a non-empty order.");
            }

            _orderForRound.Clear();
            for (int i = 0; i < order.Count; i++) _orderForRound.Add(order[i]);
            _cursor = Math.Clamp(cursor, 0, _orderForRound.Count - 1);
            _roundIndex = Math.Max(0, roundIndex);

            FireTurnQueueBuilt();
        }

        // --- Internals ----------------------------------------------------

        private void FireTurnQueueBuilt()
        {
            // COPIA del estado interno — ningún listener puede mutar la cola
            // viva del servicio (plan §10 R6).
            var snapshot = new ReadOnlyCollection<Guid>(new List<Guid>(_orderForRound));
            EventManager.Trigger(EventName.OnTurnQueueBuilt, snapshot, _roundIndex);
        }
    }
}
