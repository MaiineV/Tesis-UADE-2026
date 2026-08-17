using System;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// El peaje del mostrador: cobra <see cref="TollDamage"/> al jugador que termina su turno del
    /// mismo lado del mostrador que el Cajero. Ficha de diseño "El Cajero" (piso 2), §El peaje.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué es un servicio y no un nodo del árbol.</b> El árbol tickea en el turno del jefe;
    /// el peaje se cobra al cerrar el turno del <b>jugador</b>. Cobrarlo desde el árbol lo ataría a
    /// que el jefe llegue a actuar: aturdido, muerto en el mismo turno o con la secuencia abortada
    /// por un Running, el mostrador dejaría de cobrar — y el mostrador no es él, es la sala.
    /// </para>
    /// <para>
    /// <b>Por qué está separado de <see cref="ICashierLedgerService"/>.</b> El ledger es
    /// contabilidad keyeada por guid y no sabe nada de la sala; el peaje es una regla posicional
    /// que necesita la fila del mostrador y las coordenadas vivas de los dos. Razones distintas
    /// para cambiar, servicios distintos.
    /// </para>
    /// <para>
    /// <b>Se arma, no se configura.</b> El servicio no sabe quién es el Cajero ni dónde está su
    /// mostrador hasta que <c>AINode_CashierCounterToll</c> se lo dice en el turno del jefe. Fuera
    /// de su sala queda desarmado y es inerte, y el <c>OnCombatEnd</c> lo desarma solo: un peaje
    /// que sobreviva a la pelea cobraría en la sala siguiente sin mostrador a la vista.
    /// </para>
    /// <para>
    /// <b>Cobra una ronda de cada <see cref="ChargesEveryNRounds"/>.</b> Cobrando todas, el peaje
    /// dejaba de ser el precio de una posición: pegarle al Cajero exige distancia 1 y distancia 1
    /// es de su lado, así que acercarse costaba 20 por ronda para siempre y la respuesta correcta
    /// era no acercarse nunca. Con la ronda franca intercalada, el jugador tiene una ventana para
    /// entrar, pegar y volver — y quedarse en la ronda que cobra sigue siendo la decisión que el
    /// peaje quiere cobrar. Misma regla que el hielo de La Generala
    /// (<c>GeneralaAssetBuilder.FrostParityDivisor</c>) y que la columna del Anotador: dos jefes ya
    /// enseñaban "las pares muerden", y un tercero con otra cadencia sería una regla más que
    /// aprender por nada.
    /// </para>
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
        /// overlay: en la ronda franca no pinta nada, que es la única forma que tiene el jugador de
        /// saber que puede cruzar sin pagar.
        /// </summary>
        bool ChargesThisRound { get; }

        /// <summary>
        /// Jefe que cobra el peaje, o <see cref="Guid.Empty"/> si no está armado. Lo necesita el
        /// overlay: el lado que se pinta es el del jefe, y se resuelve con su coordenada viva.
        /// </summary>
        Guid BossGuid { get; }

        /// <summary>
        /// Arma el peaje para esta pelea. Idempotente y pensado para re-llamarse todos los turnos
        /// del jefe: así el cobro se recupera solo de un reset de combate o de un restore de save,
        /// que dejarían el servicio en blanco a mitad de pelea.
        /// </summary>
        /// <param name="bossGuid">El Cajero: define el lado que se cobra y firma el daño.</param>
        /// <param name="payerGuid">Único que paga. Sin esto, un enemigo de refuerzo del lado de él
        /// cobraría peaje también, que no es lo que dice la ficha.</param>
        /// <param name="counterRow">Fila del mostrador, en coordenadas de la sala.</param>
        /// <param name="tollDamage">Daño por terminar el turno de su lado. Ficha: 10.</param>
        /// <param name="chargesEveryNRounds">
        /// Cadencia del cobro. Default 1 (todas las rondas) para que un caller que no le importe la
        /// intermitencia no tenga que pensarla; el jefe pasa 2.
        /// </param>
        void Arm(Guid bossGuid, Guid payerGuid, int counterRow, int tollDamage,
                 int chargesEveryNRounds = 1);

        /// <summary>Apaga el peaje. Lo llama el fin de combate; el jefe nunca lo apaga por su cuenta.</summary>
        void Disarm();
    }
}
