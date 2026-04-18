using System;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Payload opcional pasado por el <c>CombatController</c> al pushear
    /// <see cref="CombatHUDView"/> en <c>OnCombatStart</c>. Transporta el target
    /// inicial del enemy panel y el room instance del encuentro.
    /// Plan §3.9 / §4.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Si no hay payload o es de otro tipo, el HUD arranca sin enemy target
    /// hasta que <c>CombatController</c> llame <c>SetEnemyTarget(Guid)</c> o
    /// el primer <c>OnTurnStarted</c> llegue.
    /// </para>
    /// </remarks>
    public sealed class CombatHUDPayload : IScreenPayload
    {
        /// <summary>Guid del enemy al que apunta inicialmente el panel. <see cref="Guid.Empty"/> = sin target.</summary>
        public Guid EnemyTargetGuid;

        /// <summary>Guid del room instance donde corre el combate. Informativo para telemetria.</summary>
        public Guid RoomInstanceId;

        /// <summary>Nombre display del encuentro (opcional, UI/telemetria).</summary>
        public string EncounterDisplayName;
    }
}
