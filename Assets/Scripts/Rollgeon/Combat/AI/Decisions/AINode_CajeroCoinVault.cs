using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// La caja del Cajero: le pone reloj a cada moneda del piso y, cuando una se vence sin que
    /// nadie la levante, se la lleva. <b>Una por turno</b>, nunca una tanda entera.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La moneda vencida <b>no le devuelve nada</b>: la plata simplemente se pierde. Lo que el
    /// jugador deja vencer es lo que dejó de ganar, y ése es todo el precio.
    /// </para>
    /// <para>
    /// De a una y no una tanda entera: el nodo tickea una vez por turno del jefe, así que "una por
    /// tick" ya es "una por ronda" y las cuatro monedas de una tanda —que nacen con el mismo
    /// reloj— se pierden a lo largo de cuatro turnos, dándole al jugador la chance de llegar a
    /// las últimas.
    /// </para>
    /// <para>
    /// El reloj es de este nodo y no del <c>DurationRounds</c> de la moneda porque el servicio de
    /// hazards expira igual una moneda cobrada y una vencida (las dos terminan en
    /// <c>OnHazardExpired</c>) y desde afuera no se puede distinguir. Las monedas nacen permanentes
    /// (<c>DurationRounds = 0</c>) justamente para que este nodo sea el único que las mata.
    /// </para>
    /// <para>
    /// Descubre por barrido de las instancias vivas de <see cref="Coin"/>, sin que quien la suelta le
    /// avise, así que va <b>después</b> de los nodos que sueltan monedas en el Sequence: si fuera
    /// antes, cada moneda viviría una ronda de más.
    /// </para>
    /// <para>
    /// Los vencimientos son <c>[NonSerialized]</c>: viven en la copia runtime del árbol y no en el
    /// asset, así que los relojes arrancan limpios en cada pelea nueva sin depender de ningún
    /// evento de teardown.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CajeroCoinVault : AIActionNode, IAIIntentNode
    {
        [Tooltip("Definición del hazard-moneda a vigilar. Tiene que ser la MISMA que sueltan los " +
                 "nodos de monedas, o el nodo no reconoce nada.")]
        public HazardDefinitionSO Coin;

        [Tooltip("Rondas que vive una moneda desde que este nodo la ve por primera vez.")]
        [MinValue(1)]
        public int LifetimeRounds = 2;

        /// <summary>
        /// Monedas con reloj, en el orden en que se van a cobrar. Lista y no diccionario: cuando hay
        /// varias vencidas a la vez hay que elegir UNA, y el orden es parte de la regla.
        /// </summary>
        [NonSerialized] private List<CoinClock> _clocks;

        public override string NodeName =>
            $"Cajero — Caja (vence una por turno a las {LifetimeRounds} rondas)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || Coin == null) return AIResult.Failed;
            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null)
                return AIResult.Failed;

            _clocks ??= new List<CoinClock>();

            Discover(hazards, context.RoundIndex);

            int due = FirstDue(context.RoundIndex);
            if (due < 0) return AIResult.Failed;

            var coin = _clocks[due];
            _clocks.RemoveAt(due);

            hazards.Deactivate(coin.InstanceId);

            return AIResult.Succeeded;
        }

        /// <summary>No describe UNA cosa: describe el reloj de cada moneda del piso.</summary>
        public bool TryDescribeIntent(AIContext context, out AIIntent intent)
        {
            intent = default;
            return false;
        }

        /// <summary>Una por moneda y dirigida a ella: el hover la encuentra por subject, y el panel del jefe la descarta.</summary>
        public void DescribeIntents(AIContext context, List<AIIntent> into)
        {
            if (context == null || into == null || _clocks == null) return;

            int queued = 0;
            for (int i = 0; i < _clocks.Count; i++)
            {
                // Se cobra una por turno: sin el lugar en la cola, cuatro vencidas dirían lo mismo.
                int left = _clocks[i].Deadline - context.RoundIndex;
                if (left <= 0) left = queued++;

                into.Add(new AIIntent(
                    AIIntentTextKeys.CashierVault, "Se la lleva la caja",
                    damage: 0, kind: AttackKind.Environmental,
                    turnsAway: left,
                    subjectGuid: _clocks[i].InstanceId));
            }
        }

        /// <summary>
        /// Le pone reloj a las monedas nuevas y suelta las que ya no están: ésas las levantó el
        /// jugador (o las limpió el teardown) y ya cobró su valor.
        /// </summary>
        /// <remarks>
        /// Las nuevas entran ordenadas por casilla y no en el orden en que las devuelve el servicio
        /// de hazards, que no garantiza ninguno: sin esto, cuál de las monedas de una tanda se lleva
        /// cambiaría entre corridas de la misma pelea.
        /// </remarks>
        private void Discover(IHazardService hazards, int roundIndex)
        {
            var live = new HashSet<Guid>();
            List<CoinClock> found = null;

            foreach (var info in hazards.ActiveInstances())
            {
                if (info.Definition != Coin) continue;

                live.Add(info.InstanceId);
                if (IsTracked(info.InstanceId)) continue;

                (found ??= new List<CoinClock>()).Add(
                    new CoinClock(info.InstanceId, roundIndex + LifetimeRounds, FirstTile(info)));
            }

            for (int i = _clocks.Count - 1; i >= 0; i--)
                if (!live.Contains(_clocks[i].InstanceId)) _clocks.RemoveAt(i);

            if (found == null) return;

            found.Sort(CompareByTile);
            _clocks.AddRange(found);
        }

        private bool IsTracked(Guid instanceId)
        {
            for (int i = 0; i < _clocks.Count; i++)
                if (_clocks[i].InstanceId == instanceId) return true;
            return false;
        }

        /// <summary>
        /// Índice de la primera moneda vencida, o −1 si ninguna lo está. La primera y no la más
        /// vieja por vencimiento: todas las de una misma tanda vencen juntas, así que el desempate lo
        /// pone el orden de la lista — y ese orden ya es "la que lleva más tiempo en el piso".
        /// </summary>
        private int FirstDue(int roundIndex)
        {
            for (int i = 0; i < _clocks.Count; i++)
                if (roundIndex >= _clocks[i].Deadline) return i;
            return -1;
        }

        private static int CompareByTile(CoinClock a, CoinClock b)
        {
            int c = a.Tile.X.CompareTo(b.Tile.X);
            return c != 0 ? c : a.Tile.Y.CompareTo(b.Tile.Y);
        }

        /// <summary>
        /// Casilla de la moneda. Es de una sola casilla por definición, pero el servicio expone una
        /// colección: se toma la primera y no se asume que haya exactamente una.
        /// </summary>
        private static GridCoord FirstTile(HazardInstanceInfo info)
        {
            if (info.Tiles == null) return default;
            foreach (var tile in info.Tiles) return tile;
            return default;
        }

        /// <summary>El reloj de una moneda concreta del piso.</summary>
        private readonly struct CoinClock
        {
            public readonly Guid InstanceId;
            public readonly int Deadline;

            /// <summary>Sólo para ordenar el cobro. Ver <see cref="Discover"/>.</summary>
            public readonly GridCoord Tile;

            public CoinClock(Guid instanceId, int deadline, GridCoord tile)
            {
                InstanceId = instanceId;
                Deadline = deadline;
                Tile = tile;
            }
        }
    }
}
