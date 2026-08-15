using System;
using Rollgeon.Combat.Cashier;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Arma el peaje del mostrador: a partir de este turno, el jugador que cierre su turno del
    /// mismo lado del mostrador que el Cajero paga <see cref="Damage"/>. Ficha de diseño
    /// "El Cajero" (piso 2), §El peaje.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El nodo arma; el cobro lo hace <see cref="ICashierCounterTollService"/>.</b> El peaje se
    /// paga al terminar el turno del jugador, no en el del jefe, así que no puede resolverse en un
    /// tick del árbol. Lo que el nodo aporta es lo único que el servicio no puede deducir: quién es
    /// el Cajero, quién es el que paga y en qué fila está su mostrador.
    /// </para>
    /// <para>
    /// <b>Re-arma todos los turnos a propósito.</b> Es barato e idempotente, y es lo que hace que
    /// el peaje se recupere solo si algo dejó el servicio en blanco a mitad de pelea (fin de
    /// combate mal disparado, restore de save). Un armado único al primer tick dejaría el
    /// mostrador mudo el resto de la pelea sin que nada lo delate.
    /// </para>
    /// <para>
    /// <b>Va antes del ciclo de ataque en el árbol.</b> Mismo motivo que el gate del arqueo: en el
    /// path no-coroutine un Running del ataque aborta lo que venga después, y el peaje no puede
    /// depender de que el jefe llegue a atacar.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CashierCounterToll : AIActionNode
    {
        [Tooltip("Daño por terminar el turno del mismo lado del mostrador que él. Ficha: 10.")]
        [MinValue(0)]
        public int Damage = 10;

        [Tooltip("Fila (Y de la grilla de la sala) que ocupa el mostrador. Sala del Cajero: 0 — el " +
                 "mostrador parte la sala en Y > 0 (su lado) e Y < 0 (el tuyo).")]
        public int CounterRow;

        public override string NodeName => $"Cajero — Peaje del mostrador ({Damage} en fila {CounterRow})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty) return AIResult.Failed;
            if (Damage <= 0) return AIResult.Failed;

            CashierCounterTollService.ResolveOrCreate()
                .Arm(context.SelfGuid, context.PlayerGuid, CounterRow, Damage);

            return AIResult.Succeeded;
        }
    }
}
