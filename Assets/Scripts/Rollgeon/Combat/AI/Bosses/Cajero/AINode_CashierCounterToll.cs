using System;
using Rollgeon.Combat.Cashier;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Arma el peaje del mostrador: a partir de este turno, el jugador que cierre su turno del
    /// mismo lado del mostrador que el Cajero paga <see cref="Damage"/>.
    /// </summary>
    /// <remarks>
    /// El nodo sólo arma; el cobro lo hace <see cref="ICashierCounterTollService"/> al cerrar el
    /// turno del jugador, fuera del árbol. Re-arma todos los turnos porque es idempotente y un
    /// armado único al primer tick dejaría el mostrador mudo si algo deja el servicio en blanco a
    /// mitad de pelea. Va antes del ciclo de ataque en el árbol: en el path no-coroutine un Running
    /// del ataque aborta lo que venga después. <see cref="ChargesEveryNRounds"/> llega en 0 en un
    /// asset viejo (Odin no corre los inicializadores de campo) y <c>Arm</c> lo clampea a 1.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CashierCounterToll : AIActionNode, IAIOpeningNode
    {
        [Tooltip("Daño por terminar el turno del mismo lado del mostrador que él. Ficha: 10.")]
        [MinValue(0)]
        public int Damage = 10;

        [Tooltip("Fila (Y de la grilla de la sala) que ocupa el mostrador. Sala del Cajero: 0 — el " +
                 "mostrador parte la sala en Y > 0 (su lado) e Y < 0 (el tuyo).")]
        public int CounterRow;

        [Tooltip("Cada cuántas rondas cobra. 1 = todas. 2 = la par cobra y la impar es franca, que " +
                 "es la ventana para acercarse a pegarle: su melee exige distancia 1 y distancia 1 " +
                 "es de su lado.")]
        [MinValue(1)]
        public int ChargesEveryNRounds = 2;

        public override string NodeName =>
            $"Cajero — Peaje del mostrador ({Damage} en fila {CounterRow}, 1 de cada {ChargesEveryNRounds} rondas)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty) return AIResult.Failed;
            if (Damage <= 0) return AIResult.Failed;

            CashierCounterTollService.ResolveOrCreate()
                .Arm(context.SelfGuid, context.PlayerGuid, CounterRow, Damage, ChargesEveryNRounds);

            return AIResult.Succeeded;
        }

        /// <summary>
        /// Arma en la apertura: el overlay del mostrador nace junto con el servicio, así que sin
        /// esto el lado que cobra no se pinta hasta que el jefe juega.
        /// </summary>
        public void Opening(AIContext context) => Tick(context);
    }
}
