using System;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Payload opcional pasado por el <c>CombatController</c> al pushear
    /// <see cref="CombatHUDView"/> en <c>OnCombatStart</c>. Transporta el target
    /// inicial del enemy panel y el room instance del encuentro.
    /// Plan §3.9 / §4.1.
    /// </summary>
    public sealed class CombatHUDPayload : IScreenPayload
    {
        /// <summary>Guid del room instance donde corre el combate. Informativo para telemetria.</summary>
        public Guid RoomInstanceId;

        /// <summary>Nombre display del encuentro (opcional, UI/telemetria).</summary>
        public string EncounterDisplayName;
    }
}
