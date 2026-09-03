namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Estado del contador M durante la secuencia: <c>Value = Core × (1 + AddSum)</c>, con
    /// <c>Core</c> = ability × Π factores ya aplicados y <c>AddSum</c> = Σ bonos aditivos ya
    /// aplicados. Así el contador cae EXACTO en el M final de la fórmula sin importar en qué
    /// orden lleguen los pasos aditivos (AddM) y multiplicativos (MultM) del journal — los
    /// procs de dado siguen pegados a su dado y los globales en orden de journal.
    /// Struct pura, testeable en EditMode (espíritu de <c>BreakdownFeelMath</c>).
    /// </summary>
    public struct MultiplierCounterState
    {
        public float Core;
        public float AddSum;

        public float Value => Core * (1f + AddSum);

        public static MultiplierCounterState At(float value)
            => new MultiplierCounterState { Core = value, AddSum = 0f };

        public void Multiply(float factor) => Core *= factor;

        public void AddBonus(float bonus) => AddSum += bonus;
    }
}
