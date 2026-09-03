namespace Rollgeon.Items
{
    /// <summary>
    /// Estado por item de la categoría "bono al combo menos usado" (Rezagado): qué combo
    /// quedó asignado a cada item con <see cref="ItemSO.LeastUsedComboBonus"/>.
    /// </summary>
    public interface ILeastUsedComboService
    {
        /// <summary>Combo asignado al item, o <c>null</c> si todavía no se asignó.</summary>
        string GetAssignedCombo(string itemId);
    }
}
