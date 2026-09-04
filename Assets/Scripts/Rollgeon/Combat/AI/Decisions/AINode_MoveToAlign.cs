using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Movimiento "Sniper": busca la casilla alcanzable este turno que quede en la MISMA fila
    /// o columna que el jugador (línea de tiro recta), priorizando la que caiga lo más cerca
    /// posible de <see cref="DesiredRange"/>. Si ya está alineado <b>y dentro de esa distancia</b>,
    /// no se mueve. Si ninguna
    /// casilla alcanzable alinea, se acerca en general al jugador por DISTANCIA DE CAMINO real
    /// (BFS sobre todo el grafo, no acotado al turno) para ir ganando terreno turno a turno,
    /// rodeando obstáculos, hasta que una alineación quede a tiro.
    /// </summary>
    /// <remarks>
    /// No existía en el proyecto ningún <c>MoveIntent</c> de alineación (solo Approach/Kite en
    /// <see cref="Pathing.IAIPathPlanner"/>) — el resto de los Ranged (Skirmisher, Sniper stock)
    /// dependen de la suerte del pathing normal para caer alineados. Este nodo resuelve la
    /// alineación a mano (el planner no tiene noción de "misma fila/columna").
    /// </remarks>
    /// <remarks>
    /// El "acercarse en general" NO delega en <see cref="Pathing.IAIPathPlanner"/>: su fast path
    /// sin casillas especiales (<c>AIPathPlanner.LegacyPlan</c>) puntúa por distancia Manhattan
    /// en línea recta dentro de lo alcanzable este turno — el MISMO criterio miope que una BFS
    /// propia, no un A* real al target. Contra un obstáculo ancho (mesa 4×2 en playtest), "seguir
    /// derecho" y "bordear" empatan en ese puntaje mientras el obstáculo siga tapando, así que ni
    /// el planner ni una heurística propia en línea recta detectan que rodear es más corto —
    /// terminaba yendo derecho contra la mesa o oscilando de lado a lado sin converger (BUGs de
    /// playtest). La distancia de CAMINO real (BFS completo, sin tope de pasos, solo para
    /// puntuar candidatos) sí lo resuelve: decrece monótonamente apenas un desvío acorta el
    /// camino real, aunque la distancia Manhattan cruda no mejore ese turno.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_MoveToAlign : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public AIIntReader MaxSteps;

        [OdinSerialize]
        [Tooltip("Distancia Manhattan preferida una vez alineado — entre las casillas alineadas " +
                 "alcanzables, elige la más cercana a este valor (para no pegarse innecesariamente). " +
                 "También es el tope para considerarse 'ya en posición': alineado pero MÁS LEJOS " +
                 "que esto sigue acercándose. Vacío = sin tope (comportamiento viejo).")]
        public AIIntReader DesiredRange;

        [Tooltip("Si está activo, alinear no alcanza: la casilla también necesita línea de visión " +
                 "libre al jugador (para el Sniper — el Charger no la necesita, carga a ciegas). " +
                 "Sin esto, un enemigo tapado por un obstáculo se considera 'ya alineado' y no " +
                 "busca reposicionarse aunque no tenga tiro.")]
        public bool RequireLineOfSight;

        public override string NodeName => "Move To Align (row/column)";

        /// <summary>Key propia: alinearse es una acción distinta de acercarse o kitear.</summary>
        public const string ActionKey = "__move_to_align";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            // Ya se movió para alinearse este turno → no-op transparente (Succeeded), mismo
            // criterio que AINode_KeepDistance: un Failed acá abortaría el While padre.
            if (context.HasExecuted(ActionKey)) return AIResult.Succeeded;
            if (context.Grid == null || context.Movement == null) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
                return AIResult.Failed;

            // "Ya llegué" = alineado + (tiro libre, si se pide) + DENTRO de DesiredRange.
            // El chequeo de distancia no estaba (BUG de playtest, Charger/bolas de pool): un
            // enemigo que comparte fila o columna con el jugador se consideraba en posición a
            // CUALQUIER distancia, así que no se movía, el gate de ataque y el de telegraph le
            // fallaban por estar lejos, y caía en Wait todos los turnos hasta que el jugador se
            // movía y rompía la alineación. Se llega ahí seguido parándose DETRÁS de otro enemigo
            // sobre la misma línea: los aliados no cortan LoS (ver GridLineOfSight.Blocks), así
            // que el de atrás se creía perfectamente en posición.
            // Con DesiredRange sin autorar (null) se conserva el comportamiento viejo: sin tope.
            int desiredRange = DesiredRange?.Read(context) ?? int.MaxValue;
            bool selfAligned = selfCoord.X == playerCoord.X || selfCoord.Y == playerCoord.Y;
            bool selfHasLos = !RequireLineOfSight
                || HasLineOfSight(context.Grid, selfCoord, playerCoord, context.SelfGuid, context.PlayerGuid);
            if (selfAligned && selfHasLos && selfCoord.Manhattan(playerCoord) <= desiredRange)
                return AIResult.Succeeded;

            int maxSteps = MaxSteps?.Read(context) ?? 3;

            var reachable = (context.Movement as IPathedMovementService)
                    ?.GetReachableAnchors(context.SelfGuid, maxSteps)
                ?? context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
            if (reachable == null || reachable.Count == 0) return AIResult.Succeeded;

            // Candidato alineado (misma fila/columna) Y con tiro libre, si se pide — el único
            // objetivo "de verdad" de este nodo. Dos niveles: preferí uno que no le haga daño
            // pisarlo (BUG de playtest: el Sniper se paraba en SU PROPIO fuego para conseguir el
            // ángulo); si ninguno alcanzable está limpio, usar el mejor igual antes que congelarse.
            ServiceLocator.TryGetService<ISpecialTileAIQuery>(out var hazardTiles);
            GridCoord? bestAligned = null;
            int bestAlignedScore = int.MaxValue;
            GridCoord? bestAlignedSafe = null;
            int bestAlignedSafeScore = int.MaxValue;

            // Semilla con la casilla ACTUAL cuando ya es un candidato válido (alineada + LoS) pero
            // quedó fuera de banda: así sólo se mueve a una estrictamente mejor. Sin la semilla,
            // ahora que "alineado" ya no corta arriba, empataría con otra casilla igual de buena y
            // oscilaría entre las dos turno a turno.
            if (selfAligned && selfHasLos)
            {
                bestAlignedScore = Mathf.Abs(selfCoord.Manhattan(playerCoord) - desiredRange);
                if (!AIMovementHazard.IsDamaging(hazardTiles, context.SelfGuid, selfCoord))
                    bestAlignedSafeScore = bestAlignedScore;
            }

            foreach (var candidate in reachable)
            {
                bool aligned = candidate.X == playerCoord.X || candidate.Y == playerCoord.Y;
                if (aligned && RequireLineOfSight
                    && !HasLineOfSight(context.Grid, candidate, playerCoord, context.SelfGuid, context.PlayerGuid))
                    aligned = false;
                if (!aligned) continue;

                int score = Mathf.Abs(candidate.Manhattan(playerCoord) - desiredRange);
                if (score < bestAlignedScore) { bestAlignedScore = score; bestAligned = candidate; }

                if (!AIMovementHazard.IsDamaging(hazardTiles, context.SelfGuid, candidate)
                    && score < bestAlignedSafeScore)
                {
                    bestAlignedSafeScore = score;
                    bestAlignedSafe = candidate;
                }
            }

            GridCoord? target = bestAlignedSafe ?? bestAligned;
            bool moved;
            if (target != null)
            {
                moved = context.Movement.Move(context.SelfGuid, target.Value);
            }
            else
            {
                // Sin candidato alineado+LoS a mano este turno: acercarse por distancia de
                // CAMINO real (no Manhattan) para rodear obstáculos de forma convergente.
                moved = TryApproachByPathDistance(context, reachable, selfCoord, playerCoord);
            }

            if (!moved) return AIResult.Succeeded; // nada mejoró la situación este turno

            context.MarkExecuted(ActionKey);

            var wait = context.VisualService?.WaitForMoveComplete(context.SelfGuid);
            if (wait != null)
            {
                context.PendingWait = wait;
                return AIResult.Running;
            }
            return AIResult.Succeeded;
        }

        /// <summary>
        /// De los candidatos alcanzables este turno, mueve al que tenga la MENOR distancia de
        /// camino real hasta el jugador (<see cref="GridPathDistance"/> — BFS desde el jugador
        /// sobre todo el grafo walkable, sin tope de pasos; la física real la sigue limitando
        /// <c>maxSteps</c> vía <paramref name="reachable"/>). Rompe empates que la distancia
        /// Manhattan no puede: un candidato "fuera de eje" que empieza a bordear un obstáculo
        /// ancho tiene menor distancia de CAMINO que uno que sigue pegado contra la pared, aunque
        /// ambos midan exactamente lo mismo en línea recta.
        /// </summary>
        private static bool TryApproachByPathDistance(AIContext context, IReadOnlyCollection<GridCoord> reachable,
            GridCoord selfCoord, GridCoord playerCoord)
        {
            var pathDist = GridPathDistance.ComputeFrom(context.Grid, playerCoord, context.SelfGuid, context.PlayerGuid);

            int currentDist = pathDist.TryGetValue(selfCoord, out var d0) ? d0 : int.MaxValue;
            GridCoord? best = null;
            int bestDist = currentDist;

            foreach (var candidate in reachable)
            {
                if (!pathDist.TryGetValue(candidate, out var dist)) continue; // sin camino conocido
                if (dist < bestDist) { bestDist = dist; best = candidate; }
            }

            return best != null && context.Movement.Move(context.SelfGuid, best.Value);
        }

        /// <summary>
        /// Delegado en <see cref="GridLineOfSight.HasClearLine"/> — la LOS única del proyecto.
        /// Sobre pares alineados (el único caso que llega acá) Bresenham camina exactamente la
        /// línea recta que antes caminaba la copia privada, con el mismo criterio de bloqueo.
        /// </summary>
        private static bool HasLineOfSight(IGridManager grid, GridCoord from, GridCoord to, Guid selfGuid, Guid targetGuid)
            => GridLineOfSight.HasClearLine(grid, from, to, selfGuid, targetGuid);
    }
}
