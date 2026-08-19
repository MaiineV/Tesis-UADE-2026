namespace Rollgeon.Tiles
{
    /// <summary>
    /// Tipo de efecto de una casilla especial (GDD, sección 7). Una casilla física tiene
    /// UNA sola categoría activa — invariante del GDD (sección 9), y la razón por la que
    /// esto es un campo y no una lista polimórfica. Cada categoría se resuelve con un
    /// <see cref="ITileEffectHandler"/> registrado en el motor: agregar una reservada es
    /// enum + handler, sin refactor.
    /// </summary>
    public enum TileEffectCategory
    {
        // --- Activas ------------------------------------------------------------
        Damage = 0,
        Heal = 1,
        ApplyStatus = 2,
        /// <summary>Fortaleza: modificador mientras permanece. Sin handler de pulso — lo
        /// consulta on-demand el provider de daño saliente.</summary>
        StatModifier = 3,
        /// <summary>Impulso. Inerte hasta que exista tirada real de movimiento.</summary>
        MoveRangeBonus = 4,
        /// <summary>Hielo: la unidad sigue deslizándose en la dirección de entrada.</summary>
        ForcedSlide = 5,
        /// <summary>Portal: teleport al par conectado + reposición.</summary>
        Teleport = 6,
        /// <summary>Advertencia sin efecto propio; al vencer ejecuta el payload anunciado.</summary>
        Telegraph = 7,
        /// <summary>Zona de Seguridad: protege de tipos de casilla declarados. Sin handler —
        /// es un filtro del motor.</summary>
        ConditionalProtection = 8,

        // --- Reservadas (GDD sección 7, "sin uso todavía") ------------------------
        Root = 9,
        Kill = 10,
        Spawn = 11,
        TileTransformation = 12,
        VisionModifier = 13,
        TargetingModifier = 14,
        Cover = 15,
        ResourceChange = 16,
    }
}
