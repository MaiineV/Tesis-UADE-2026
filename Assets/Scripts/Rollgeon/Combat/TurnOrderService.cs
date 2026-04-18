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
    /// <b>Regla "jugador primero en su turno" (GDD §12.7).</b> Esta regla NO
    /// se implementa acá: el servicio es agnóstico al rol. El jugador tiene
    /// <c>Speed</c> como cualquier otra entidad, y su slot sale del roll de
    /// initiative como los enemigos. "Primero en su turno" = dentro de su slot
    /// individual (cuando es su turno, actúa el player, no hay nadie "antes"
    /// en el mismo tick). No forzar al player al tope de la cola.
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
        /// <exception cref="ArgumentNullException">Si <paramref name="participants"/> es null.</exception>
        /// <exception cref="InvalidOperationException">Si la lista queda vacía.</exception>
        public void BuildForCombat(IEnumerable<Guid> participants)
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

        /// <summary>Limpia el estado al cerrar combate.</summary>
        public void Reset()
        {
            _orderForRound.Clear();
            _cursor = 0;
            _roundIndex = 0;
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
