using System;
using Rollgeon.Grid;
using Rollgeon.Tiles;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Chequeo liviano de "¿esta celda le hace daño a esta unidad si la pisa?", compartido por
    /// los nodos de reposicionamiento que arman su propia búsqueda de candidatos en vez de pasar
    /// por <see cref="Pathing.IAIPathPlanner"/> (que ya evita hazards vía su Pareto de HP
    /// proyectado). <see cref="AINode_MoveToAlign"/>/<see cref="AINode_MoveToLineOfSight"/>
    /// resuelven alineación/LoS a mano porque el planner no tiene esa noción — pero eso significa
    /// que tampoco heredan gratis su hazard-awareness, así que un enemigo podía terminar
    /// parándose en su propio fuego para conseguir el ángulo (BUG de playtest).
    /// </summary>
    /// <remarks>
    /// No es el sistema completo de <see cref="Pathing.AIPathPlanner"/> (HP proyectado, filtro de
    /// supervivencia, Pareto por celda) — solo "¿esta celda puntual duele?", para poder preferir
    /// un candidato limpio sobre uno que arde, sin construir un segundo planner. Dirección de
    /// entrada fija en <see cref="Cardinal.South"/> (mismo default que usa <c>AIPathPlanner</c>
    /// para su primera label): ningún hazard actual del GDD depende de la dirección de entrada
    /// para SU propio daño (solo el deslizamiento del Hielo la usa, y eso no es daño).
    /// </remarks>
    internal static class AIMovementHazard
    {
        public static bool IsDamaging(ISpecialTileAIQuery tiles, Guid selfGuid, GridCoord coord)
        {
            if (tiles == null) return false;
            if (!tiles.TryGetTileFor(coord, selfGuid, Cardinal.South, out var view)) return false;
            return view.EnterDamage > 0 || view.StayDamage > 0;
        }
    }
}
