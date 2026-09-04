using System;
using Patterns;
using Rollgeon.Combat.Cashier;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>
    /// La maldición del reloj de las monedas del Cajero. Activa cuando hay al menos una en el
    /// piso: la fuente es el mismo ledger que les lleva el vencimiento, así que el bloque del
    /// panel aparece cuando hay algo que perder de verdad y no desde el turno 1, antes de que
    /// caiga la primera.
    /// </summary>
    /// <remarks>
    /// Resuelve el ledger sin crearlo: es lazy y sólo existe mientras el Cajero está en la sala,
    /// así que en cualquier otra pelea la tarjeta simplemente no sale.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Entities/Coin Clock Curse", fileName = "BC_CoinClock")]
    public class CoinClockCurseSO : BossCurseSO
    {
        public override bool IsActive(Guid bossGuid)
            => ServiceLocator.TryGetService<ICashierLedgerService>(out var ledger) && ledger != null
               && ledger.ChipsOnFloor > 0;
    }
}
