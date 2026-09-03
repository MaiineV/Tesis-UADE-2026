using System;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>Scope global con reset en <c>OnCombatEnd</c>/<c>OnRunEnd</c>: si el jugador muere con oro secuestrado, la banca gana.</summary>
    public interface ICashierLedgerService
    {
        /// <summary>Oro del jugador retenido; se le devuelve al vencer al jefe.</summary>
        int VaultedGold { get; }

        /// <summary>Multiplica el valor de las fichas al soltarlas: 1 antes del arqueo, <c>ChipValueMultiplierAfterAudit</c> después.</summary>
        int ChipValueMultiplier { get; }

        /// <summary>Escalones de descuento activos por soborno (0 = sin descuento).</summary>
        int DamageStepDown { get; }

        /// <summary>Rondas de vigencia que le quedan al soborno (0 = no hay soborno activo).</summary>
        int BribeRoundsLeft { get; }

        /// <summary>Escalones que el jefe se subió solo por el paso de las rondas, sin mirar el oro del jugador: no baja nunca, sólo lo contrarresta el soborno.</summary>
        int DamageStepUp { get; }

        /// <summary>Costo en oro de un soborno. Default = 35 (ficha).</summary>
        int BribeCost { get; set; }

        /// <summary>Rondas que dura el descuento de un soborno. Default = 3 (ficha).</summary>
        int BribeRounds { get; set; }

        /// <summary>Cada cuántas rondas el rastrillo suma un escalón. Default = 3; cero o negativo lo apaga.</summary>
        int RakeRoundsPerStep { get; set; }

        /// <summary><c>true</c> —y limpia el flag— si <paramref name="entityGuid"/> recibió daño desde la última consulta.</summary>
        bool ConsumeDamageTaken(Guid entityGuid);

        /// <summary>
        /// Le cobra al jugador <paramref name="percent"/> (0..1) del oro que lleve encima y devuelve
        /// cuánto le sacó (0 si está seco o no hay economía). Nunca cobra menos de
        /// <paramref name="minimum"/>, salvo que al jugador le quede menos que eso: entonces cobra
        /// lo que haya.
        /// </summary>
        /// <param name="refundOnDeath">
        /// Con <c>true</c> lo cobrado entra a la caja de <paramref name="ownerGuid"/>, y matarlo se
        /// lo devuelve entero al jugador. Con <c>false</c> la plata sale del juego.
        /// </param>
        int CollectTax(Guid ownerGuid, float percent, int minimum = 0, bool refundOnDeath = true);

        void SetChipValueMultiplier(int multiplier);

        /// <summary>Cobra <see cref="BribeCost"/> y arma <see cref="BribeRounds"/> rondas de <see cref="DamageStepDown"/> = 1; <c>false</c> si el jugador no puede pagar.</summary>
        bool TryBribe();

        /// <summary>
        /// Paga <paramref name="value"/> cuando el hazard se dispare; si expira sin cobrarse no paga
        /// a nadie, y si el que la pisa es su dueño tampoco. Levantar una ficha también soborna.
        /// </summary>
        void RegisterChip(Guid hazardInstanceId, int value, Guid ownerGuid);

        /// <summary>Valor de una ficha viva, o 0 si ese id no es una ficha del Cajero.</summary>
        int GetChipValue(Guid hazardInstanceId);

        /// <summary>Último escalón que el jefe resolvió al marcar, tal cual lo va a pegar. <c>null</c> antes de la primera marca.</summary>
        CashierTierSnapshot? LastTier { get; }

        /// <summary>Lo llama <c>AINode_TelegraphMarkGoldScaled</c> con el escalón resuelto, para que el HUD no recalcule el daño con su propia copia de la tabla.</summary>
        void ReportTier(int rank, int damage, int gold, int stepUp, int stepDown);
    }

    public readonly struct CashierTierSnapshot
    {
        /// <summary>Índice del escalón efectivo en la tabla (0 = el más barato).</summary>
        public readonly int Rank;

        public readonly int Damage;

        public readonly int Gold;

        public readonly int StepUp;

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
