using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Localization;
using Rollgeon.Player;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Publica la pasiva de clase del hero actual como un estado del player.
    /// </summary>
    /// <remarks>
    /// Lee el hero del <c>IPlayerService</c> en cada Collect en vez de cachearlo: el hero
    /// cambia entre runs y el HUD sobrevive a eso. Sin servicio o sin pasiva no publica
    /// nada, y la fila queda vacía — no es un error, hay clases sin pasiva.
    /// </remarks>
    public sealed class ClassPassiveStatusProvider : IStatusIconProvider
    {
        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null) return;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var players) || players == null) return;

            var passive = players.CurrentHero?.Passive;
            if (passive == null) return;

            bool active = passive.IsActiveFor(ownerGuid);

            // El ícono inactivo es opcional: sin él la pasiva se lee igual, cambia el marco.
            var icon = active
                ? passive.ActiveIcon
                : (passive.InactiveIcon != null ? passive.InactiveIcon : passive.ActiveIcon);
            if (icon == null) return;

            string id = string.IsNullOrEmpty(passive.PassiveId) ? "passive" : passive.PassiveId;
            into.Add(new StatusIconState(
                id,
                LocalizedContent.Name(id, passive.DisplayName ?? string.Empty),
                LocalizedContent.Description(id, passive.Description ?? string.Empty),
                icon,
                active,
                // Una pasiva no vence: el tooltip habla de activada/desactivada y el ícono
                // no muestra número.
                remainingTurns: null));
        }
    }
}
