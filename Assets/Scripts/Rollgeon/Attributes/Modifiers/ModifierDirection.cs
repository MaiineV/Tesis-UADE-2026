namespace Rollgeon.Attributes.Modifiers
{
    /// <summary>
    /// Direccion de aplicacion del modificador. La pipeline de dano (§12)
    /// consulta <see cref="Outgoing"/> en la fuente, <see cref="Incoming"/> en
    /// el destino, y <see cref="Intrinsic"/> al leer el stat directo.
    /// Especificado en TECHNICAL.md §3.2.
    /// </summary>
    public enum ModifierDirection
    {
        /// <summary>Aplica cuando la entidad es ORIGEN de la operacion (dano saliente, heal saliente).</summary>
        Outgoing,

        /// <summary>Aplica cuando la entidad es DESTINO de la operacion (recibe dano, recibe heal).</summary>
        Incoming,

        /// <summary>No depende de direccion — aplica al stat mismo (ej: +10 Health max, tick de veneno).</summary>
        Intrinsic,
    }
}
