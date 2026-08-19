using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Status;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Publica el estado Envenenado del player en la fila de status icons, con los turnos
    /// restantes como badge. Sin veneno activo no publica nada — el slot no existe.
    /// </summary>
    public sealed class PoisonStatusProvider : IStatusIconProvider
    {
        public const string StateId = "status.poison";

        private readonly StatusIconCatalogSO _catalog;

        public PoisonStatusProvider(StatusIconCatalogSO catalog) => _catalog = catalog;

        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null) return;
            if (!ServiceLocator.TryGetService<IPoisonService>(out var poison) || poison == null) return;
            if (!poison.IsPoisoned(ownerGuid)) return;

            into.Add(new StatusIconState(
                StateId,
                LocalizedContent.Name(StateId, "Envenenado"),
                LocalizedContent.Description(StateId, "Recibís daño al inicio de cada turno."),
                _catalog != null ? _catalog.Resolve(StateId) : null,
                active: true,
                remainingTurns: poison.GetPoisonTurns(ownerGuid)));
        }
    }
}
