using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Status;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Publica el estado Sangrado (Feature#0085) en la fila de status icons, con la
    /// duración del stack más duradero como badge y el conteo de stacks en el nombre
    /// ("Sangrado ×N"). Sin stacks activos no publica nada — el slot no existe.
    /// </summary>
    public sealed class BleedStatusProvider : IStatusIconProvider
    {
        public const string StateId = "status.bleed";

        private readonly StatusIconCatalogSO _catalog;

        public BleedStatusProvider(StatusIconCatalogSO catalog) => _catalog = catalog;

        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null) return;
            if (!ServiceLocator.TryGetService<IBleedService>(out var bleed) || bleed == null) return;
            if (!bleed.IsBleeding(ownerGuid)) return;

            int stacks = bleed.GetStacks(ownerGuid);
            string name = string.Format(
                LocalizedContent.Name(StateId, "Sangrado ×{0}"), stacks);

            into.Add(new StatusIconState(
                StateId,
                name,
                LocalizedContent.Description(StateId, "Recibís daño al inicio de cada turno por cada stack de Sangrado."),
                _catalog != null ? _catalog.Resolve(StateId) : null,
                active: true,
                remainingTurns: bleed.GetMaxRemainingTurns(ownerGuid)));
        }
    }
}
