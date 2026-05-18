using Rollgeon.Combat.Actions;

namespace Rollgeon.Dice
{
    /// <summary>
    /// Value class (plain C#, mutable in place) que representa el estado del
    /// presupuesto de rerolls para la <see cref="Rollgeon.Combat.Actions.ActionDefinitionSO"/>
    /// actualmente activa. TECHNICAL.md §6.5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por que clase y no struct.</b> El servicio mantiene exactamente una
    /// instancia viva por accion abierta y la mutamos varias veces (un reroll
    /// por click). Una struct obligaria a reasignarla tras cada mutacion y
    /// duplicaria la superficie de API sin aportar inmutabilidad real.
    /// </para>
    /// <para>
    /// <b>Semantica de <see cref="FreeRollsRemaining"/>.</b> Cuenta las tiradas
    /// gratis que todavia quedan disponibles, <b>incluyendo el primer roll</b>.
    /// El primer click de "Roll" consume una unidad igual que un reroll. El
    /// servicio inicializa con <c>FreeRollsRemaining = action.FreeRollCount</c>
    /// al llamar <c>StartBudget</c> — el contador HUD muestra "rolls disponibles".
    /// </para>
    /// </remarks>
    public sealed class RerollBudget
    {
        /// <summary>
        /// Tiradas gratis disponibles. Se decrementa con <see cref="ConsumeFree"/>.
        /// Nunca negativo.
        /// </summary>
        public int FreeRollsRemaining { get; internal set; }

        /// <summary>
        /// Rerolls pagos ya consumidos (gastaron energia). Se incrementa con
        /// <see cref="ConsumePaid"/>. Queda disponible post-<c>EndBudget</c>
        /// como dato de telemetria hasta el siguiente <c>StartBudget</c>.
        /// </summary>
        public int PaidRollsUsed { get; internal set; }

        /// <summary>
        /// Accion para la que se abrio este presupuesto. Seteada por el servicio
        /// en <c>StartBudget</c>. Null tras <c>EndBudget</c>.
        /// </summary>
        public ActionDefinitionSO Action { get; internal set; }

        /// <summary>
        /// Intenta consumir una tirada gratis. Devuelve <c>true</c> si habia al
        /// menos una y la decremento; <c>false</c> si ya no quedaban.
        /// </summary>
        public bool ConsumeFree()
        {
            if (FreeRollsRemaining <= 0) return false;
            FreeRollsRemaining--;
            return true;
        }

        /// <summary>
        /// Registra que se consumio una tirada paga. El debit de energia lo
        /// hace el servicio; esta clase solo lleva la cuenta.
        /// </summary>
        public void ConsumePaid()
        {
            PaidRollsUsed++;
        }

        /// <summary>
        /// Resetea contadores + la referencia a la accion. Llamado por
        /// <c>RerollBudgetService.EndBudget</c>.
        /// </summary>
        public void Reset()
        {
            FreeRollsRemaining = 0;
            PaidRollsUsed = 0;
            Action = null;
        }
    }
}
