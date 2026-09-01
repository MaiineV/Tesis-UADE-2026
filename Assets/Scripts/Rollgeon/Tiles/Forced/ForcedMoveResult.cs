using System;
using Rollgeon.Grid;

namespace Rollgeon.Tiles.Forced
{
    /// <summary>Por qué terminó una resolución de movimiento forzado.</summary>
    public enum ForcedMoveStop
    {
        /// <summary>Recorrió toda la distancia pedida (más las continuaciones de tiles).</summary>
        CompletedDistance = 0,

        /// <summary>
        /// Chocó contra una celda no transitable u ocupada. <see cref="ForcedMoveResult.BlockerGuid"/>
        /// distingue pared (Empty) de ocupante.
        /// </summary>
        Obstacle = 1,

        /// <summary>La unidad murió a mitad de la cadena (pinchos, fuego...).</summary>
        Death = 2,

        /// <summary>Un portal sin salida usable cortó la cadena (sin par, o salida ocupada).</summary>
        PortalBlocked = 3,
    }

    /// <summary>Resultado de <see cref="IForcedMovementService.Push"/>.</summary>
    public readonly struct ForcedMoveResult
    {
        /// <summary>Celda final. Si murió, la última que ocupó antes de desregistrarse.</summary>
        public readonly GridCoord FinalCoord;

        /// <summary>Celdas efectivamente recorridas (pasos de empuje + deslizamientos).</summary>
        public readonly int TilesTraveled;

        public readonly ForcedMoveStop StoppedBy;

        public readonly bool TargetDied;

        /// <summary>
        /// Celda a la que no pudo entrar. Solo significativa con
        /// <see cref="ForcedMoveStop.Obstacle"/>. Se captura en el momento del choque: con
        /// portales la unidad puede terminar reubicada fuera de la línea de empuje, así que no
        /// se puede reconstruir desde <see cref="FinalCoord"/>.
        /// </summary>
        public readonly GridCoord BlockedAt;

        /// <summary>
        /// Ocupante de <see cref="BlockedAt"/>. <c>Guid.Empty</c> = pared / fuera de grilla
        /// (celda no transitable sin nadie encima).
        /// </summary>
        public readonly Guid BlockerGuid;

        public bool BlockedByWall => StoppedBy == ForcedMoveStop.Obstacle && BlockerGuid == Guid.Empty;
        public bool BlockedByEntity => StoppedBy == ForcedMoveStop.Obstacle && BlockerGuid != Guid.Empty;

        public ForcedMoveResult(GridCoord finalCoord, int tilesTraveled, ForcedMoveStop stoppedBy, bool targetDied)
            : this(finalCoord, tilesTraveled, stoppedBy, targetDied, default, Guid.Empty)
        {
        }

        public ForcedMoveResult(GridCoord finalCoord, int tilesTraveled, ForcedMoveStop stoppedBy, bool targetDied,
            GridCoord blockedAt, Guid blockerGuid)
        {
            FinalCoord = finalCoord;
            TilesTraveled = tilesTraveled;
            StoppedBy = stoppedBy;
            TargetDied = targetDied;
            BlockedAt = blockedAt;
            BlockerGuid = blockerGuid;
        }
    }
}
