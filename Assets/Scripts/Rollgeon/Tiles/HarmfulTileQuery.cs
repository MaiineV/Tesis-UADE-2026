using Patterns;
using Rollgeon.Grid;

namespace Rollgeon.Tiles
{
    /// <summary>
    /// Qué casilla le cuesta algo a quien se pare ahí. Único punto de esa lectura: la comparten los
    /// reacomodos de los jefes, que eligen a dónde saltar.
    /// </summary>
    /// <remarks>
    /// Sale de la data y no de una lista de <see cref="SpecialTileType"/>: una casilla nueva que
    /// haga daño queda cubierta sin tocar esto, y una que no lo haga (hielo, portal, zona segura)
    /// no se prohíbe sola por ser especial.
    /// </remarks>
    public static class HarmfulTileQuery
    {
        /// <summary>
        /// <c>true</c> si pararse en una casilla de esta definición cuesta vida.
        /// </summary>
        /// <remarks>
        /// <see cref="SpecialTileDefinitionSO.AIVirtualEnterDamage"/> cuenta aunque no sea daño real:
        /// es exactamente el campo con el que la data declara "esto la IA lo tiene que evitar" — el
        /// charco eléctrico no pega, paraliza.
        /// </remarks>
        public static bool DealsDamage(SpecialTileDefinitionSO definition)
        {
            if (definition == null) return false;

            return definition.EnterDamage > 0
                   || definition.TurnStartDamage > 0
                   || definition.StatusTickDamage > 0
                   || definition.AIVirtualEnterDamage > 0;
        }

        /// <summary>
        /// <c>true</c> si hay al menos una casilla especial dañina sobre <paramref name="coord"/>.
        /// </summary>
        /// <remarks>
        /// Recorre todas las instancias y no <c>TryGetTileAt</c>, que devuelve una sola: las casillas
        /// se solapan, así que fuego debajo de un telegraph daría limpio. Sin servicio devuelve
        /// <c>false</c> — degrada a "sin filtro", nunca a "todo prohibido".
        /// </remarks>
        public static bool IsHarmfulAt(GridCoord coord)
        {
            if (!ServiceLocator.TryGetService<ISpecialTileService>(out var tiles) || tiles == null)
                return false;

            foreach (var instance in tiles.ActiveInstances())
            {
                if (!DealsDamage(instance.Definition) || instance.Coords == null) continue;

                for (int i = 0; i < instance.Coords.Count; i++)
                    if (instance.Coords[i].Equals(coord)) return true;
            }

            return false;
        }
    }
}
