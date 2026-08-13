using System;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// Contabilidad del Cajero (piso 2): la caja donde secuestra el oro del arqueo, el soborno
    /// que le baja un escalón, las fichas que sueltan sus golpes y el flag de "me pegaron".
    /// Es el único estado del jefe que vive fuera del árbol de AI, porque tiene que sobrevivir
    /// entre turnos y reaccionar a eventos del jugador (pisar una ficha, matar al jefe).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sólo lectura sobre el oro del jugador, salvo lo que la ficha manda explícitamente.</b>
    /// El servicio toca <c>IEconomyService</c> en exactamente tres lugares: el arqueo
    /// (<see cref="CollectTax"/>, "guarda el 40% de tu oro"), el soborno
    /// (<see cref="TryBribe"/>, "entregarle 35 de oro") y las devoluciones —
    /// ficha cobrada + caja abierta al vencerlo. Ninguna otra ruta escribe oro.
    /// </para>
    /// <para>
    /// <b>Scope.</b> Global (como <c>HazardService</c>), con reset propio en
    /// <c>OnCombatEnd</c>/<c>OnRunEnd</c>: la caja no se filtra a la pelea siguiente. Si el
    /// jugador muere con oro secuestrado, la banca gana — no hay devolución.
    /// </para>
    /// </remarks>
    public interface ICashierLedgerService
    {
        /// <summary>Oro del jugador retenido en la caja, pendiente de devolución al vencer al jefe.</summary>
        int VaultedGold { get; }

        /// <summary>
        /// Multiplicador que se aplica al valor de las fichas al soltarlas. 1 antes del arqueo,
        /// <c>ChipValueMultiplierAfterAudit</c> después ("las fichas valen el doble").
        /// </summary>
        int ChipValueMultiplier { get; }

        /// <summary>Escalones de descuento activos por soborno (0 = sin descuento).</summary>
        int DamageStepDown { get; }

        /// <summary>Costo en oro de un soborno. Default = 35 (ficha).</summary>
        int BribeCost { get; set; }

        /// <summary>Rondas que dura el descuento de un soborno. Default = 3 (ficha).</summary>
        int BribeRounds { get; set; }

        /// <summary>
        /// <c>true</c> (y limpia el flag) si <paramref name="entityGuid"/> recibió daño desde la
        /// última consulta. El nodo de fichas lo usa para "si le pegaste este turno, suelta una".
        /// </summary>
        bool ConsumeDamageTaken(Guid entityGuid);

        /// <summary>
        /// Arqueo de caja: guarda <paramref name="percent"/> (0..1) del oro del jugador en la caja
        /// de <paramref name="ownerGuid"/> y devuelve cuánto guardó (0 si el jugador está seco o
        /// no hay economía). El heal del jefe lo aplica el nodo con este retorno.
        /// </summary>
        int CollectTax(Guid ownerGuid, float percent);

        /// <summary>Setea el multiplicador de fichas (el arqueo lo sube a 2).</summary>
        void SetChipValueMultiplier(int multiplier);

        /// <summary>
        /// Soborno: cobra <see cref="BribeCost"/> y arma <see cref="BribeRounds"/> rondas de
        /// <see cref="DamageStepDown"/> = 1. Devuelve <c>false</c> si el jugador no puede pagar.
        /// Lo llama la acción del jugador — el jefe nunca se soborna solo.
        /// </summary>
        bool TryBribe();

        /// <summary>
        /// Registra una ficha viva: cuando el hazard <paramref name="hazardInstanceId"/> se dispare
        /// (alguien la pisa) el servicio le paga <paramref name="value"/> de oro. Si expira sin
        /// cobrarse, la ficha se descarta ("rueda de vuelta a la caja") sin pagarle a nadie.
        /// <paramref name="ownerGuid"/> es quien la soltó: si es él el que la pisa (el jefe kitea
        /// sobre su propia columna), no se cobra.
        /// </summary>
        void RegisterChip(Guid hazardInstanceId, int value, Guid ownerGuid);

        /// <summary>Valor de una ficha viva, o 0 si ese id no es una ficha del Cajero.</summary>
        int GetChipValue(Guid hazardInstanceId);
    }
}
