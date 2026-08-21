using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// La caja del Cajero: le pone reloj a cada moneda del piso y, cuando una se vence sin que
    /// nadie la levante, se la lleva y lo cura <see cref="HealPerCoin"/> — hasta un techo de
    /// <see cref="MaxHealPerFight"/> en toda la pelea. <b>Una por turno</b>, nunca una tanda entera.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué de a una:</b> la ficha lo pide con esas palabras — "se vencen de a una, no todas
    /// juntas: la presión es constante, no un golpe". Y la cuenta lo explica: la sala suelta las
    /// cuatro monedas de la tanda juntas y las cuatro nacen con el mismo reloj, así que cobrarlas
    /// juntas sería casi todo el <see cref="MaxHealPerFight"/> de la pelea en un solo turno. De a
    /// una, la misma tanda se paga a lo largo de cuatro turnos y el jugador ve subir la barra
    /// despacio en vez de un salto que ya no puede contestar. El intervalo no es un número aparte:
    /// el nodo tickea una vez por turno del jefe, así que "una por tick" ya es "una por ronda" y el
    /// reloj sigue siendo uno solo.
    /// </para>
    /// <para>
    /// El vencimiento cumplido no se pierde: una moneda que ya venció y no le tocó turno se queda en
    /// el piso —todavía levantable— y sale en el siguiente. Con cola pendiente eso es exactamente
    /// una moneda por ronda, que es la presión constante que la ficha describe.
    /// </para>
    /// <para>
    /// <b>Por qué el reloj es de este nodo y no del <c>DurationRounds</c> de la moneda:</b> el
    /// servicio de hazards expira igual una moneda cobrada y una vencida (las dos terminan en
    /// <c>OnHazardExpired</c>), así que desde afuera no se puede saber cuál de las dos pasó — y la
    /// diferencia es toda la pelea. Acá se ve: una moneda que desaparece antes de su vencimiento la
    /// levantó el jugador y se olvida sin curar; una que sigue viva pasado el vencimiento la cobra
    /// él. Las monedas nacen permanentes (<c>DurationRounds = 0</c>) justamente para que este nodo
    /// sea el único que las mata.
    /// </para>
    /// <para>
    /// <b>Descubrimiento por barrido, no por registro:</b> el nodo no necesita que quien suelta la
    /// moneda le avise. Barre las instancias vivas de <see cref="Coin"/> y le pone vencimiento a la
    /// que no conoce, así que sirve igual para las monedas de sala, las del empujón y cualquier
    /// fuente futura. Por eso va <b>después</b> de los nodos que sueltan monedas en el Sequence: si
    /// fuera antes, cada moneda viviría una ronda de más.
    /// </para>
    /// <para>
    /// <b>Estado de pelea:</b> vencimientos y curación acumulada son <c>[NonSerialized]</c>, o sea
    /// que viven en la copia runtime del árbol (<c>EnemyDataSO.CreateRuntimeAIRoot</c>) y no en el
    /// asset — el techo de curación arranca en cero en cada pelea nueva sin depender de ningún
    /// evento de teardown. Mismo patrón que <see cref="AINode_Alternate"/> y
    /// <see cref="AINode_SpawnReinforcements"/>.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CajeroCoinVault : AIActionNode
    {
        [Tooltip("Definición del hazard-moneda a vigilar. Tiene que ser la MISMA que sueltan los " +
                 "nodos de monedas, o el nodo no reconoce nada.")]
        public HazardDefinitionSO Coin;

        [Tooltip("Rondas que vive una moneda desde que este nodo la ve por primera vez.")]
        [MinValue(1)]
        public int LifetimeRounds = 3;

        [Tooltip("HP que le devuelve al jefe cada moneda vencida.")]
        [MinValue(0)]
        public int HealPerCoin = 12;

        [Tooltip("Techo de curación por pelea. Alcanzado, las monedas siguen venciéndose pero ya " +
                 "no lo curan.")]
        [MinValue(0)]
        public int MaxHealPerFight = 60;

        /// <summary>
        /// Monedas con reloj, en el orden en que se van a cobrar. Lista y no diccionario: cuando hay
        /// varias vencidas a la vez hay que elegir UNA, y el orden es parte de la regla.
        /// </summary>
        [NonSerialized] private List<CoinClock> _clocks;
        [NonSerialized] private int _healed;

        public override string NodeName =>
            $"Cajero — Caja (de a una: {HealPerCoin} por moneda, techo {MaxHealPerFight})";

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

            // La moneda se va igual con el techo alcanzado: lo que se agota es la curación, no el
            // vencimiento. Dejarla en el piso convertiría el techo en plata gratis.
            hazards.Deactivate(coin.InstanceId);
            Collect(context);

            return AIResult.Succeeded;
        }

        /// <summary>
        /// Le pone reloj a las monedas nuevas y suelta las que ya no están: ésas las levantó el
        /// jugador (o las limpió el teardown) y no curan a nadie.
        /// </summary>
        /// <remarks>
        /// Las nuevas entran ordenadas por casilla y no en el orden en que las devuelve el servicio
        /// de hazards, que no garantiza ninguno. Sin esto, cuál de las cuatro monedas de una tanda se
        /// lleva —o sea qué oro le queda al jugador en el piso— cambiaría entre corridas de la misma
        /// pelea. Mismo motivo que el sort de <see cref="AINode_CajeroCoinRain"/>.
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

        private void Collect(AIContext context)
        {
            if (HealPerCoin <= 0) return;

            int budget = MaxHealPerFight - _healed;
            if (budget <= 0) return;

            int heal = Mathf.Min(HealPerCoin, budget);
            var attrs = context.Attributes;
            if (attrs == null) ServiceLocator.TryGetService<AttributesManager>(out attrs);
            if (attrs == null)
            {
                Debug.LogWarning("[AINode_CajeroCoinVault] AttributesManager no disponible — la " +
                                 "moneda se vence pero el jefe no se cura.");
                return;
            }

            // SelfMaxHp es el cap del spawn (misma fuente que PcOwnerHpBelow). Sin baseline no se
            // clampea: preferimos curar de más antes que comerse la curación entera.
            int maxHp = context.SelfMaxHp > 0 ? context.SelfMaxHp : int.MaxValue;

            int before = attrs.GetAttributeValue<Health, int>(context.SelfGuid);
            attrs.Modify<Health, int>(context.SelfGuid, current =>
            {
                int healed = current + heal;
                return healed > maxHp ? maxHp : healed;
            });
            int landed = Mathf.Max(0, attrs.GetAttributeValue<Health, int>(context.SelfGuid) - before);

            // El techo cuenta lo que ENTRÓ, no lo que se ofreció: una moneda que se vence con el
            // jefe lleno no tiene que gastarle presupuesto de curación que todavía no usó.
            _healed += landed;
            if (landed <= 0) return;

            EventManager.Trigger(
                EventName.OnFloatingNumberRequested,
                context.SelfGuid,
                FloatingNumberType.Heal,
                (float)landed,
                Vector3.zero);
        }
    }
}
