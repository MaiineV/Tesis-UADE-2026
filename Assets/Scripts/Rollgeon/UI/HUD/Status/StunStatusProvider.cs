using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Status;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Publica el Aturdimiento del player en la fila de status icons. El StunService
    /// existía sin UI — este provider es su primera cara visible.
    /// </summary>
    public sealed class StunStatusProvider : IStatusIconProvider
    {
        public const string StateId = "status.stun";

        private readonly StatusIconCatalogSO _catalog;

        public StunStatusProvider(StatusIconCatalogSO catalog) => _catalog = catalog;

        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null) return;
            if (!ServiceLocator.TryGetService<IStunService>(out var stun) || stun == null) return;
            if (!stun.IsStunned(ownerGuid)) return;

            into.Add(new StatusIconState(
                StateId,
                LocalizedContent.Name(StateId, "Aturdido"),
                LocalizedContent.Description(StateId, "Perdés tu próximo turno."),
                _catalog != null ? _catalog.Resolve(StateId) : null,
                active: true,
                remainingTurns: stun.GetStunTurns(ownerGuid)));
        }
    }
}
