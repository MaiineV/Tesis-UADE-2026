namespace Rollgeon.Combos.Counters
{
    /// <summary>
    /// Superficie pública del servicio de <b>Combo Counters</b> (TECHNICAL.md §5.5).
    /// <para>
    /// Balatro-style: cada ejecución exitosa de un combo incrementa su contador run-scoped.
    /// El multiplicador de bonus derivado se expone via <see cref="GetBonusMultiplier"/> para
    /// que el <c>AttackResolver</c> (§12 / T100b) lo consuma downstream. Esta tarea entrega
    /// únicamente la <b>infra</b> — los thresholds y curva de balance los determina el dato
    /// del <see cref="Rollgeon.Balance.ComboCountersConfig"/> en el <c>RulesetSO</c> activo.
    /// </para>
    /// <para>
    /// <b>Scope.</b> El servicio vive en <c>ServiceScope.Global</c> (registered in bootstrap).
    /// El <see cref="RunComboCounterState"/> subyacente vive en <c>ServiceScope.Run</c> y se
    /// crea al recibir <c>OnRunStart</c> / se limpia con <c>ClearScope(Run)</c> en <c>OnRunEnd</c>.
    /// Fuera de run, el service degrada a <c>0</c> / <c>1.0f</c> (no-op).
    /// </para>
    /// </summary>
    public interface IComboCountersService
    {
        /// <summary>Cantidad actual de matches del combo <paramref name="comboId"/> en la run activa. <c>0</c> si nunca matcheó o fuera de run.</summary>
        int GetCount(string comboId);

        /// <summary>
        /// Incrementa el contador del combo en <c>+1</c> y dispara
        /// <see cref="Patterns.EventName.OnComboCounterIncremented"/>. No-op fuera de run.
        /// </summary>
        void IncrementCount(string comboId);

        /// <summary>
        /// Multiplicador de bonus derivado del contador actual, según
        /// <see cref="Rollgeon.Balance.ComboCountersConfig"/>. Devuelve <c>1.0f</c> si no hay
        /// ruleset, si el contador es <c>0</c>, o fuera de run.
        /// <para>
        /// Fórmula: <c>multiplier = 1 + min(MaxBonus, Count * PerUseBonus)</c>.
        /// </para>
        /// </summary>
        float GetBonusMultiplier(string comboId);
    }
}
