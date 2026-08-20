using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Tiles
{
    /// <summary>
    /// Snapshot inmutable de una instancia viva de casilla especial. Mismo criterio que
    /// <c>HazardInstanceInfo</c>: la lista de coords es una copia — mutar la instancia
    /// después no cambia lo que el caller ya tiene.
    /// </summary>
    public readonly struct SpecialTileInfo
    {
        public readonly Guid InstanceId;
        public readonly SpecialTileDefinitionSO Definition;
        public readonly IReadOnlyList<GridCoord> Coords;

        /// <summary>Rondas de vida restantes; 0 = permanente.</summary>
        public readonly int RemainingRounds;

        /// <summary><see cref="Guid.Empty"/> = escenario (autoría de sala).</summary>
        public readonly Guid OwnerGuid;

        /// <summary>Instancia par (portales). <see cref="Guid.Empty"/> si no hay link.</summary>
        public readonly Guid LinkedInstanceId;

        public SpecialTileInfo(Guid instanceId, SpecialTileDefinitionSO definition,
            IReadOnlyList<GridCoord> coords, int remainingRounds, Guid ownerGuid, Guid linkedInstanceId)
        {
            InstanceId = instanceId;
            Definition = definition;
            Coords = coords;
            RemainingRounds = remainingRounds;
            OwnerGuid = ownerGuid;
            LinkedInstanceId = linkedInstanceId;
        }
    }
}
