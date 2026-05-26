namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Estado lifecycle de una <see cref="RoomInstance"/>. TECHNICAL.md §13.6.
    /// <list type="bullet">
    /// <item><c>Uncleared</c> — sala con encuentro pendiente. Doors lockean al entrar.</item>
    /// <item><c>Cleared</c> — encuentro resuelto. Doors quedan abiertas; no hay respawn de enemigos.</item>
    /// <item><c>Locked</c> — sala bloqueada por un gate externo (boss key, evento, etc.).</item>
    /// </list>
    /// </summary>
    public enum RoomState
    {
        Uncleared = 0,
        Cleared = 1,
        Locked = 2
    }
}