using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Items.Active.Blood;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Publica la carga pendiente de Blood D6 (Feature#0084) en la fila de status icons: el
    /// badge muestra el % de bonus armado. Sin carga pendiente no publica nada.
    /// </summary>
    public sealed class BloodD6StatusProvider : IStatusIconProvider
    {
        public const string StateId = "status.bloodd6";

        private readonly StatusIconCatalogSO _catalog;

        public BloodD6StatusProvider(StatusIconCatalogSO catalog) => _catalog = catalog;

        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null) return;
            if (!ServiceLocator.TryGetService<IBloodD6Service>(out var service) || service == null) return;
            if (!service.TryGetPendingBonusPct(ownerGuid, out int bonusPct)) return;

            string name = string.Format(
                LocalizedContent.Name(StateId, "Blood D6 +{0}%"), bonusPct);

            into.Add(new StatusIconState(
                StateId,
                name,
                LocalizedContent.Description(StateId,
                    "El próximo combo de Ataque suma daño extra repartido entre objetivos cercanos."),
                _catalog != null ? _catalog.Resolve(StateId) : null,
                active: true,
                remainingTurns: 0));
        }
    }
}
