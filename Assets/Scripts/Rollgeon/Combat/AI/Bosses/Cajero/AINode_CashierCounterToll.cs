using System;
using Rollgeon.Combat.Cashier;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// El nodo sólo arma; el cobro lo hace <see cref="ICashierCounterTollService"/> al cerrar el
    /// turno del jugador, fuera del árbol. Re-arma todos los turnos porque es idempotente y el
    /// mostrador queda mudo si algo deja el servicio en blanco a mitad de pelea. Va antes del ciclo
    /// de ataque: en el path no-coroutine un Running del ataque aborta lo que venga después.
    /// </summary>
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

        /// <summary>Arma en la apertura: el overlay del mostrador nace junto con el servicio, así que sin esto el lado que cobra no se pinta hasta que el jefe juega.</summary>
        public void Opening(AIContext context) => Tick(context);
    }
}
