namespace Rollgeon.Attributes.Modifiers
{
    /// <summary>
    /// Enum serializable que reemplaza al delegate <c>Func&lt;T, T, T&gt;</c>.
    /// Un delegate no lo serializa ni Unity ni Odin; con un enum el save captura
    /// el valor entero y al restaurar se resuelve via <see cref="OperationResolver"/>.
    /// Especificado en TECHNICAL.md §3.1 / §3.3.
    /// </summary>
    public enum ModifierOperation
    {
        /// <summary>Numeric: result = value + amount.</summary>
        Add,
        /// <summary>Numeric: result = value - amount.</summary>
        Subtract,
        /// <summary>Numeric: result = value * amount.</summary>
        Multiply,
        /// <summary>Numeric / bool / ref: result = amount (fuerza el valor).</summary>
        Override,
        /// <summary>Numeric: result = min(value, amount). Techo.</summary>
        Min,
        /// <summary>Numeric: result = max(value, amount). Piso.</summary>
        Max,
        /// <summary>Numeric: result = value + value * amount. Amount es fraccion (0.2 = +20%).</summary>
        Percent,
        /// <summary>Bool: idem Override.</summary>
        Set,
        /// <summary>Bool: result = value AND amount.</summary>
        And,
        /// <summary>Bool: result = value OR amount.</summary>
        Or,
        /// <summary>Bool: result = value XOR amount.</summary>
        Xor,
        /// <summary>Ref / struct: reemplaza la referencia completa. Comportamiento equivalente a Override para tipos simples.</summary>
        Replace,
    }
}
