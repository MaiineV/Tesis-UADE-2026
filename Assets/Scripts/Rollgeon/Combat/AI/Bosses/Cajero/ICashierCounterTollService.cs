using System;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// El peaje del mostrador: cobra <see cref="TollDamage"/> al jugador que termina su turno del
    /// mismo lado del mostrador que el Cajero.
    /// </summary>
    /// <remarks>
    /// El cobro cae al cerrar el turno del <b>jugador</b>, fuera del árbol. Es inerte hasta que
    /// <c>AINode_CashierCounterToll</c> lo arma, y <c>OnCombatEnd</c> lo desarma para que no cobre
    /// en la sala siguiente.
    /// </remarks>
    public interface ICashierCounterTollService
    {
        /// <summary>Daño del peaje mientras esté armado, 0 si no lo está.</summary>
        int TollDamage { get; }

        /// <summary>Fila (Y de la grilla) que ocupa el mostrador. Los dos lados son Y mayor y Y menor.</summary>
        int CounterRow { get; }

        /// <summary><c>true</c> cuando hay jefe, pagador y daño — o sea, cuando el peaje puede cobrar.</summary>
        bool IsArmed { get; }

        /// <summary>
        /// Cada cuántas rondas cobra el peaje. 1 = todas. 2 = la ronda par cobra y la impar es
        /// franca. 0 se trata como 1.
        /// </summary>
        int ChargesEveryNRounds { get; }

        /// <summary>
        /// <c>true</c> si el peaje está armado <b>y</b> esta ronda es de las que cobran. Lo mira el
        /// overlay: en la ronda franca no pinta nada.
        /// </summary>
        bool ChargesThisRound { get; }

        /// <summary>
        /// Jefe que cobra el peaje, o <see cref="Guid.Empty"/> si no está armado. El overlay lo
        /// necesita: el lado que pinta se resuelve con la coordenada viva del jefe.
        /// </summary>
        Guid BossGuid { get; }

        /// <summary>
        /// Arma el peaje para esta pelea. Idempotente y pensado para re-llamarse todos los turnos
        /// del jefe: así el cobro se recupera solo si algo deja el servicio en blanco.
        /// </summary>
        /// <param name="bossGuid">El Cajero: define el lado que se cobra y firma el daño.</param>
        /// <param name="payerGuid">Único que paga: si no, un refuerzo de su lado también pagaría.</param>
        /// <param name="counterRow">Fila del mostrador, en coordenadas de la sala.</param>
        /// <param name="tollDamage">Daño por terminar el turno de su lado. Autorado en
        /// <c>CajeroAssetBuilder.CounterTollDamage</c>.</param>
        /// <param name="chargesEveryNRounds">
        /// Cadencia del cobro. Default 1 (todas las rondas); el jefe autora la suya en
        /// <c>CajeroAssetBuilder.CounterTollEveryNRounds</c>.
        /// </param>
        void Arm(Guid bossGuid, Guid payerGuid, int counterRow, int tollDamage,
                 int chargesEveryNRounds = 1);

        /// <summary>Apaga el peaje. Lo llama el fin de combate; el jefe nunca lo apaga por su cuenta.</summary>
        void Disarm();
    }
}
