using System;
using Rollgeon.Heroes;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Payload passed from <see cref="ClassSelectionScreen"/> to
    /// <see cref="BuildSelectionScreen"/> carrying the selected hero, run id,
    /// and ruleset id. UI#0013a.
    /// </summary>
    public sealed class BuildSelectionPayload : IScreenPayload
    {
        public ClassHeroSO SelectedHero;
        public Guid RunId;
        public string RulesetId;
    }
}
