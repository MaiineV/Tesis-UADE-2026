using System;

namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Máscara de filtrado consumida por queries futuras (<c>TQ_OccupiedSlots</c>,
    /// <c>TQ_AdjacentEnemies</c>, …) para decidir qué categorías de entidades aceptar.
    /// Declarada acá porque la API pública de algunas queries downstream va a recibirla
    /// como parámetro — declararla en esta foundation evita breaking changes más adelante.
    /// TECHNICAL.md §11.2b.
    /// </summary>
    [Flags]
    public enum EntityFilterMask
    {
        None     = 0,
        Allies   = 1 << 0,
        Enemies  = 1 << 1,
        Neutrals = 1 << 2,
        Player   = 1 << 3,
        Props    = 1 << 4,
    }
}
