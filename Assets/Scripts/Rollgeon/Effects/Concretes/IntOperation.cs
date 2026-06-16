namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Operadores aritméticos para mutación directa de un atributo int.
    /// Usado por <see cref="EffModifyIntAttribute"/>.
    /// </summary>
    /// <remarks>
    /// NO confundir con <c>Rollgeon.Attributes.Modifiers.ModifierOperation</c> — esa
    /// es para capas de modifier (buffs/debuffs aplicados encima del base value).
    /// Este enum es para mutación directa del valor base. Diferencias: incluye Divide
    /// (que ModifierOperation no tiene) y omite Min/Max/Percent/And/Or/Xor/Replace
    /// (que aplican a layering, no a una operación atómica de set).
    /// </remarks>
    public enum IntOperation
    {
        Add        = 0,
        Subtract   = 1,
        Multiply   = 2,
        Divide     = 3,
        Set        = 4,
    }
}
