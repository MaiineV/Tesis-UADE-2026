using Rollgeon.Grid;

namespace Rollgeon.Tiles.Forced
{
    /// <summary>Por qué terminó una resolución de movimiento forzado.</summary>
    public enum ForcedMoveStop
    {
        /// <summary>Recorrió toda la distancia pedida (más las continuaciones de tiles).</summary>
        CompletedDistance = 0,

        /// <summary>Chocó contra una celda no transitable u ocupada.</summary>
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

        public ForcedMoveResult(GridCoord finalCoord, int tilesTraveled, ForcedMoveStop stoppedBy, bool targetDied)
        {
            FinalCoord = finalCoord;
            TilesTraveled = tilesTraveled;
            StoppedBy = stoppedBy;
            TargetDied = targetDied;
        }
    }
}
