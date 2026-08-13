namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Single: el jugador elige N targets individuales (N = <see cref="SelectionSettings.GetSelectionCount"/>).
    /// Aoe: se elige UNA celda ancla dentro de las válidas y el efecto se expande alrededor
    /// según <see cref="AoeShape"/> (la expansión re-aplica los filtros SlotState + EntityFilter).
    /// </summary>
    public enum TargetMode
    {
        Single = 0,
        Aoe = 1,
    }
}
