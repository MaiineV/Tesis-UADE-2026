namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Forma del área AoE alrededor del ancla (solo aplica con <see cref="TargetMode.Aoe"/>).
    /// Radius: celdas a distancia Manhattan &lt;= <see cref="SelectionSettings.AoeRadius"/>.
    /// Custom: patrón bool-grid relativo al ancla (<see cref="SelectionSettings.PatternFlat"/>).
    /// </summary>
    public enum AoeShape
    {
        Radius = 0,
        Custom = 1,
    }
}
