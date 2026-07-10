using Rollgeon.Dungeon;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Seam opcional para redirigir la casilla del primer spawn de un enemigo.
    /// Lo registran modos especiales (hoy: Tutorial Mode, que planta al melee
    /// cerca del jugador para enseñar el movimiento). Ausente del ServiceLocator
    /// = spawn points del layout, comportamiento default.
    /// </summary>
    public interface IEnemySpawnCoordOverride
    {
        /// <summary>
        /// <c>true</c> si <paramref name="coord"/> debe reemplazar a
        /// <paramref name="defaultCoord"/> para el spawn <paramref name="spawnIndex"/>
        /// de <paramref name="instance"/>.
        /// </summary>
        bool TryOverrideSpawnCoord(RoomInstance instance, int spawnIndex, GridCoord defaultCoord, out GridCoord coord);
    }
}
