using System;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// Contabilidad del Cajero (piso 2): la caja donde secuestra el oro del arqueo, el rastrillo
    /// que le sube el escalón solo con el paso de las rondas, el soborno que se lo baja, las
    /// fichas que sueltan sus golpes y el flag de "me pegaron". Es el único estado del jefe que
    /// vive fuera del árbol de AI, porque tiene que sobrevivir entre turnos y reaccionar a
    /// eventos del jugador (pisar una ficha, matar al jefe).
    /// </summary>
    /// <remarks>
    /// Escribe <c>IEconomyService</c> en exactamente tres lugares —arqueo, soborno y devoluciones—
    /// y en ninguna otra ruta. Scope global con reset en <c>OnCombatEnd</c>/<c>OnRunEnd</c>, así que
    /// la caja no se filtra a la pelea siguiente; si el jugador muere con oro secuestrado, la banca
    /// gana.
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

        /// <summary>
        /// Rondas que le quedan de vigencia al soborno (0 = no hay soborno activo). Es
        /// <see cref="DamageStepDown"/> con la cuenta atrás visible: el descuento es binario, pero
        /// el jugador necesita saber cuándo se le vence para decidir si vale la pena ir a buscar
        /// otra ficha.
        /// </summary>
        int BribeRoundsLeft { get; }

        /// <summary>
        /// El rastrillo: escalones que el jefe se subió solo por el paso de las rondas,
        /// <b>sin mirar el oro del jugador</b>. Sube +1 cada <see cref="RakeRoundsPerStep"/>
        /// rondas de combate y no baja nunca — sólo lo contrarresta el soborno.
        /// </summary>
        /// <remarks>
        /// Es lo que convierte juntar fichas en mantenimiento obligatorio en vez de codicia
        /// opcional: sin rastrillo, un jugador pobre deja al Cajero clavado en el escalón más
        /// barato toda la pelea y el jefe deja de existir como amenaza.
        /// </remarks>
        int DamageStepUp { get; }

        /// <summary>Costo en oro de un soborno. Default = 35 (ficha).</summary>
        int BribeCost { get; set; }

        /// <summary>Rondas que dura el descuento de un soborno. Default = 3 (ficha).</summary>
        int BribeRounds { get; set; }

        /// <summary>
        /// Cada cuántas rondas el rastrillo suma un escalón. Default = 3 (ficha), que es la
        /// misma ventana que compra un soborno: pagar cada 3 rondas te deja en cero, dejar de
        /// pagar te hunde. Cero o negativo apaga el rastrillo.
        /// </summary>
        int RakeRoundsPerStep { get; set; }

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
        /// <remarks>
        /// Las dos ventanas miden lo mismo a propósito: un soborno por ciclo de rastrillo mantiene
        /// el escalón donde lo puso el oro. <b>Hoy ninguna acción del jugador lo llama</b> — el
        /// soborno real de la pelea es pisar una ficha (<see cref="RegisterChip"/>); esto queda como
        /// el precio de lista por si entra un botón.
        /// </remarks>
        bool TryBribe();

        /// <summary>
        /// Registra una ficha viva: cuando el hazard <paramref name="hazardInstanceId"/> se dispare
        /// (alguien la pisa) el servicio le paga <paramref name="value"/> de oro. Si expira sin
        /// cobrarse, la ficha se descarta ("rueda de vuelta a la caja") sin pagarle a nadie.
        /// <paramref name="ownerGuid"/> es quien la soltó: si es él el que la pisa (el jefe kitea
        /// sobre su propia columna), no se cobra.
        /// </summary>
        /// <remarks>
        /// <b>Levantar una ficha también soborna</b>, sin cobrar <see cref="BribeCost"/>. Sin eso
        /// sería una recompensa envenenada: lo único que el jefe suelta te paga en oro, y el oro es
        /// justo lo que le sube el escalón.
        /// </remarks>
        void RegisterChip(Guid hazardInstanceId, int value, Guid ownerGuid);

        /// <summary>Valor de una ficha viva, o 0 si ese id no es una ficha del Cajero.</summary>
        int GetChipValue(Guid hazardInstanceId);

        /// <summary>
        /// Último escalón que el jefe resolvió al marcar, tal cual lo va a pegar. <c>null</c> antes
        /// de la primera marca.
        /// </summary>
        CashierTierSnapshot? LastTier { get; }

        /// <summary>
        /// Lo llama <c>AINode_TelegraphMarkGoldScaled</c> con el escalón que acaba de resolver, para
        /// que la lectura del HUD muestre el daño <b>real</b>.
        /// </summary>
        /// <remarks>
        /// La UI podría recalcularlo con su propia copia de la tabla, y ahí es exactamente donde se
        /// separaría del golpe: la tabla vive en el asset del jefe y el efectivo depende además del
        /// rastrillo y del soborno. Una lectura que miente es peor que no tener lectura.
        /// </remarks>
        void ReportTier(int rank, int damage, int gold, int stepUp, int stepDown);
    }

    /// <summary>
    /// Foto del escalón con el que el Cajero marcó la última columna: qué va a pegar y de dónde sale
    /// ese número. Inmutable — el servicio es dueño del estado mutable.
    /// </summary>
    public readonly struct CashierTierSnapshot
    {
        /// <summary>Índice del escalón efectivo en la tabla (0 = el más barato).</summary>
        public readonly int Rank;

        /// <summary>Daño de la columna con ese escalón.</summary>
        public readonly int Damage;

        /// <summary>Oro que tenía el jugador cuando se resolvió.</summary>
        public readonly int Gold;

        /// <summary>Escalones que sumó el rastrillo.</summary>
        public readonly int StepUp;

        /// <summary>Escalones que restó el soborno.</summary>
        public readonly int StepDown;

        public CashierTierSnapshot(int rank, int damage, int gold, int stepUp, int stepDown)
        {
            Rank = rank;
            Damage = damage;
            Gold = gold;
            StepUp = stepUp;
            StepDown = stepDown;
        }
    }
}
