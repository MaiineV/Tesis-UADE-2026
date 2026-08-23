using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Status;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Publica el estado "recién teletransportado" del player en la fila de status icons,
    /// con los turnos restantes como badge. Sin cooldown activo no publica nada.
    /// </summary>
    public sealed class TeleportCooldownStatusProvider : IStatusIconProvider
    {
        public const string StateId = "status.tp_delay";

        private readonly StatusIconCatalogSO _catalog;

        public TeleportCooldownStatusProvider(StatusIconCatalogSO catalog) => _catalog = catalog;

        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null) return;
            if (!ServiceLocator.TryGetService<ITeleportCooldownService>(out var cooldown) || cooldown == null) return;
            if (!cooldown.IsOnCooldown(ownerGuid)) return;

            into.Add(new StatusIconState(
                StateId,
                LocalizedContent.Name(StateId, "Recién teletransportado"),
                LocalizedContent.Description(StateId, "No podés volver a usar un portal hasta que pase el efecto."),
                _catalog != null ? _catalog.Resolve(StateId) : null,
                active: true,
                remainingTurns: cooldown.GetTurns(ownerGuid)));
        }
    }
}
