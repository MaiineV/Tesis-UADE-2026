using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// El candado: los dados que el jefe te tiene trabados este turno.
    /// </summary>
    public sealed class DiceBlockStatusProvider : IStatusIconProvider
    {
        public const string StateId = "status.dice_block";

        private readonly StatusIconCatalogSO _catalog;

        public DiceBlockStatusProvider(StatusIconCatalogSO catalog) => _catalog = catalog;

        /// <remarks>
        /// Ignora <paramref name="ownerGuid"/> a propósito, y es el único provider que lo hace: el
        /// candado es un estado del JUGADOR, pero la tarjeta cuelga del jefe que lo puso, así que
        /// el guid que llega acá es el del jefe. Con un jefe por sala no hay ambigüedad posible.
        /// </remarks>
        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null) return;
            if (!ServiceLocator.TryGetService<IDiceBlockService>(out var dice) || dice == null) return;

            int blocked = dice.BlockedIndices?.Count ?? 0;
            if (blocked <= 0) return;

            into.Add(new StatusIconState(
                StateId,
                LocalizedContent.Name(StateId, "Candado"),
                LocalizedContent.Description(StateId,
                    "Uno de tus dados queda trabado. Sortea otro cada turno."),
                _catalog != null ? _catalog.Resolve(StateId) : null,
                active: true,
                remainingTurns: null,
                stackCount: blocked));
        }
    }
}
