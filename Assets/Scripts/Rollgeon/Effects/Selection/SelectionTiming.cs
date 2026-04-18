namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Cuándo se resuelve la selección relativa al <c>TryExecute</c> del <see cref="EffectData"/>.
    /// TECHNICAL.md §11.2. Expandible — nuevos modos se appendean sin renumerar.
    /// </summary>
    public enum SelectionTiming
    {
        /// <summary>El caller (behavior dispatcher) resuelve la selección ANTES de llamar
        /// <c>TryExecute</c>, poblando <see cref="EffectContext.SelectionResult"/>.</summary>
        BeforeResolve = 0,

        /// <summary>El <c>ApplyEffect</c> del efecto resuelve la selección via
        /// <see cref="SelectionSettings.TargetQuery"/> durante su propia ejecución.</summary>
        DuringResolve = 1,
    }
}
