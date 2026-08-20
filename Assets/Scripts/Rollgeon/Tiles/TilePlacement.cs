using System;

namespace Rollgeon.Tiles
{
    /// <summary>
    /// Opciones de <see cref="ISpecialTileService.Place"/> (autoría de sala). El default
    /// completo es válido: casilla de escenario (sin owner), duración del SO.
    /// </summary>
    public struct TilePlacementOptions
    {
        /// <summary><see cref="Guid.Empty"/> = escenario.</summary>
        public Guid Owner;

        /// <summary>Override de duración en rondas; 0 = usar <c>DefaultDurationRounds</c> del SO.</summary>
        public int DurationRounds;

        /// <summary>Instancia ya colocada a la que linkear (portales). Opcional.</summary>
        public Guid LinkTo;
    }

    /// <summary>
    /// Requisitos de una casilla creada en runtime (GDD, sección "Runtime"): owner y
    /// duración son obligatorios sin excepción — el feedback visual y el cleanup los
    /// garantiza el lifecycle del servicio.
    /// </summary>
    public struct RuntimeTileRequest
    {
        /// <summary>Quién la creó (jefe, player vía ítem, evento). Obligatorio.</summary>
        public Guid Owner;

        /// <summary>Rondas de vida. Obligatorio salvo <see cref="Permanent"/> explícito.</summary>
        public int DurationRounds;

        /// <summary>Escape explícito para variantes permanentes creadas en runtime
        /// (Charco Eléctrico permanente). Evita el "0 = permanente" accidental.</summary>
        public bool Permanent;

        /// <summary>Instancia par a linkear (portales). Opcional.</summary>
        public Guid LinkTo;
    }

    /// <summary>Por qué se rechazó una creación runtime (GDD: validación de casilla libre válida).</summary>
    public enum TilePlacementError
    {
        None = 0,
        NullDefinition = 1,
        MissingOwner = 2,
        MissingDuration = 3,
        CoordNotWalkable = 4,
        CoordOccupiedByUnit = 5,
        CoordHasSpecialTile = 6,

        /// <summary>El owner no puede crear este tipo (Zona de Seguridad runtime = solo jefes).</summary>
        OwnerNotAuthorized = 7,
    }
}
