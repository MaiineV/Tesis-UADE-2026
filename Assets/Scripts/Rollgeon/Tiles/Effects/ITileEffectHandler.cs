using System;
using Rollgeon.Grid;

namespace Rollgeon.Tiles.Effects
{
    /// <summary>
    /// Resuelve una <see cref="TileEffectCategory"/> concreta. El motor mantiene un registry
    /// categoría → handler: implementar una categoría reservada es escribir un handler y
    /// registrarlo — el motor no cambia.
    /// </summary>
    public interface ITileEffectHandler
    {
        TileEffectCategory Category { get; }

        void Apply(in TileEffectContext ctx);
    }

    /// <summary>Contexto de un disparo de efecto de casilla.</summary>
    public readonly struct TileEffectContext
    {
        public readonly Guid InstanceId;
        public readonly SpecialTileDefinitionSO Definition;
        public readonly Guid TargetGuid;
        public readonly GridCoord Coord;

        /// <summary>El trigger concreto que disparó (OnEnter vs OnTurnStart eligen daños distintos).</summary>
        public readonly TileTrigger Trigger;

        public readonly TileMovementKind MovementKind;
        public readonly Guid OwnerGuid;

        public TileEffectContext(Guid instanceId, SpecialTileDefinitionSO definition, Guid targetGuid,
            GridCoord coord, TileTrigger trigger, TileMovementKind movementKind, Guid ownerGuid)
        {
            InstanceId = instanceId;
            Definition = definition;
            TargetGuid = targetGuid;
            Coord = coord;
            Trigger = trigger;
            MovementKind = movementKind;
            OwnerGuid = ownerGuid;
        }
    }
}
