namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Resuelve en runtime la cantidad de targets de una selección
    /// (<see cref="SelectionSettings.GetSelectionCount"/> con
    /// <see cref="SelectionSettings.IsConstantSelectionCount"/> == false).
    /// Mismo patrón que el <c>IReader&lt;int&gt;</c> de Bot-Game.
    /// <para>
    /// Las implementaciones DEBEN ser defensivas ante un <see cref="ReadInfo"/> default
    /// (guid vacío / servicios no registrados): hay call sites sin owner disponible
    /// (ej. <c>ActionDragPolicy</c>) que esperan un mínimo seguro, nunca una excepción.
    /// </para>
    /// </summary>
    public interface ISelectionCountReader
    {
        int Read(ReadInfo info);
    }
}
