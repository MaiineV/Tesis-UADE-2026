namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Cuándo se resuelve la selección relativa al <c>TryExecute</c> del <see cref="EffectData"/>.
    /// TECHNICAL.md §11.2. Expandible — nuevos modos se appendean sin renumerar.
    /// </summary>
    public enum SelectionTiming
    {
        /// <summary>La selección se resuelve ANTES de la tirada de dados.</summary>
        BeforeRoll = 0,

        /// <summary>La selección se resuelve DESPUÉS de resolver los dados.</summary>
        AfterRoll = 1,
    }
}
