namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Condición opcional que un <see cref="Triggers.Concretes.ModifyResourceTrigger"/>
    /// evalúa además del <see cref="ComboFilter"/> antes de aplicar la operación.
    /// Cubre los casos que el filtro de combo no expresa: una condición negativa
    /// (no hubo combo) o una condición sobre el valor del dado carrier.
    /// </summary>
    public enum TriggerCondition
    {
        /// <summary>Sin condición extra (default — comportamiento del trigger genérico base).</summary>
        None,

        /// <summary>Dispara solo si el dado carrier NO participó de ningún combo en el evento.</summary>
        NoComboMatched,

        /// <summary>Dispara solo si el dado carrier muestra su cara máxima (ej. un d6 en 6).</summary>
        DieOnMaxFace,
    }
}
