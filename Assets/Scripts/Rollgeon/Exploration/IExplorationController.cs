namespace Rollgeon.Exploration
{
    /// <summary>
    /// Controller de la fase de exploración — arbitra las transiciones
    /// Combat/Exploration y el procesamiento de la sala activa.
    /// <para>
    /// 2026-04-22: <c>AdvanceRoom()</c> fue removido. Con el sistema de
    /// puertas físicas (TECHNICAL.md §13.6), la transición la dispara el
    /// player cruzando una puerta via
    /// <see cref="Rollgeon.Dungeon.IDungeonService.EnterRoomByDoor"/>, no
    /// un botón "Proceed" de HUD.
    /// </para>
    /// </summary>
    public interface IExplorationController
    {
        bool IsExploring { get; }
        void BeginExploration();

        /// <summary>
        /// Restaura la fase <c>Exploration</c> post-combate. La sala ya quedó
        /// <see cref="Rollgeon.Dungeon.RoomState.Cleared"/> via
        /// <see cref="Patterns.EventName.OnCombatEnd"/> + DungeonManager, y las
        /// puertas se abren; el player sigue in-place hasta cruzar una puerta.
        /// </summary>
        void ResumeAfterCombat();
    }
}
