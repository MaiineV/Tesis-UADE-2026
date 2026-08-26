using System;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// Cobra <see cref="TollDamage"/> al jugador que cierra su turno del mismo lado del mostrador que
    /// el Cajero, fuera del árbol. Inerte hasta que <c>AINode_CashierCounterToll</c> lo arma, y
    /// <c>OnCombatEnd</c> lo desarma para que no cobre en la sala siguiente.
    /// </summary>
    public interface ICashierCounterTollService
    {
        /// <summary>Daño del peaje mientras esté armado, 0 si no lo está.</summary>
        int TollDamage { get; }

        /// <summary>Fila (Y de la grilla) que ocupa el mostrador. Los dos lados son Y mayor y Y menor.</summary>
        int CounterRow { get; }

        bool IsArmed { get; }

        /// <summary>Cada cuántas rondas cobra el peaje. 1 = todas. 2 = la par cobra y la impar es franca. 0 se trata como 1.</summary>
        int ChargesEveryNRounds { get; }

        /// <summary><c>true</c> si está armado y esta ronda es de las que cobran: en la franca el overlay no pinta nada.</summary>
        bool ChargesThisRound { get; }

        /// <summary>Jefe que cobra, o <see cref="Guid.Empty"/> si no está armado: el lado que pinta el overlay se resuelve con su coordenada viva.</summary>
        Guid BossGuid { get; }

        /// <summary>
        /// Idempotente y pensado para re-llamarse todos los turnos, así el cobro se recupera solo.
        /// <paramref name="payerGuid"/> es el único que paga — si no, un refuerzo también pagaría.
        /// </summary>
        void Arm(Guid bossGuid, Guid payerGuid, int counterRow, int tollDamage,
                 int chargesEveryNRounds = 1);

        void Disarm();
    }
}
