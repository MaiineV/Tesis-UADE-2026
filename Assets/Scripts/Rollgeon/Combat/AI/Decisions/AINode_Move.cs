using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Mueve al enemigo hacia un target configurable manteniendo una distancia deseada
    /// (<see cref="DesiredRange"/>). Se acerca si está lejos y, si <see cref="Retreat"/>,
    /// retrocede (kite) si está demasiado cerca. TECHNICAL.md §7.5 + §17.§B.
    /// </summary>
    /// <remarks>
    /// Con <see cref="TargetSelector"/> null el resolver usa <c>TargetSelector_AlwaysPlayer</c>, y
    /// con <see cref="DesiredRange"/> null se cae a <see cref="StopAdjacent"/> (rango 1).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Move : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public AIIntReader MaxSteps;

        [OdinSerialize]
        [Tooltip("A quién apuntar. Null = player (TargetSelector_AlwaysPlayer).")]
        public BaseEnemyTargetSelector TargetSelector;

        [OdinSerialize]
        [Tooltip("Distancia Manhattan al target que el enemigo intenta mantener. " +
                 "Null = legacy: StopAdjacent ? 1 : 0.")]
        public AIIntReader DesiredRange;

        [Tooltip("Si true y está más cerca que DesiredRange, retrocede (kite). " +
                 "Si false, demasiado cerca = no se mueve.")]
        public bool Retreat;

        [Tooltip("Si true, 'ya estoy a DesiredRange' no alcanza para no-opear: también exige " +
                 "línea de visión clara al target. Sin esto, un enemigo puede quedar trabado " +
                 "para siempre a la distancia justa pero sin ver a su target (ej. el jugador " +
                 "parado en el medio tapándole la vista al Healer hacia su aliado herido) — " +
                 "el nodo cree que ya llegó y nunca reintenta reposicionarse. Default false: " +
                 "no cambia nada para los árboles ya autorados que usan este nodo.")]
        public bool RequireLineOfSight;

        [Tooltip("DEPRECADO — usar DesiredRange. Solo fallback cuando DesiredRange es null: " +
                 "true => rango 1 (frena adyacente), false => rango 0.")]
        public bool StopAdjacent = true;

        public override string NodeName => "Move Toward Target";

        /// <summary>Key compartida por todos los AINode_Move del árbol: mover es UNA acción por turno.</summary>
        // Público: el AITreeValidator (editor asmdef) lo usa para vetar ActionNames reservados.
        public const string ActionKey = "__move";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            // Ya movió este turno → no-op transparente (Succeeded, no Failed — un Failed
            // abortaría el While padre) para que el loop siga drenando energía.
            if (context.HasExecuted(ActionKey)) return AIResult.Succeeded;
            if (context.Grid == null || context.Movement == null) return AIResult.Failed;

            // "El selector no encontró a nadie" y "el target no está en el grid" son benignos, no
            // errores: no hay a dónde caminar este turno y punto. Succeeded (no-op) — con Failed,
            // un AINode_Sequence padre abortaba el turno ENTERO y se comía los nodos HERMANOS (el
            // ShowGuardAura del Guardian, el intento de curación del Healer). Es la continuación
            // del fix BUG-061/PUL-014, que ya convirtió los otros casos benignos de este nodo en
            // Succeeded y dejó éstos afuera por olvido.
            // A propósito NO se redirige al jugador como fallback: eso alteraría la intención
            // autorada del nodo (un Healer backline terminaría caminando hacia el jugador). Los
            // árboles que quieren perseguir sin aliados ya lo expresan con su propia rama Else.
            var targetGuid = EnemyTargetResolver.Resolve(TargetSelector, context, context.SelfGuid);
            if (targetGuid == Guid.Empty) return AIResult.Succeeded;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Failed;
            if (!context.Grid.TryGetPosition(targetGuid, out var targetCoord))
                return AIResult.Succeeded;

            int desiredRange = DesiredRange?.Read(context) ?? (StopAdjacent ? 1 : 0);
            // Rect-a-rect (Fase B): la distancia se mide desde la celda más cercana de cada
            // footprint; para dos 1×1 es el Manhattan de siempre.
            var selfFp = context.Grid.GetFootprint(context.SelfGuid);
            var targetFp = context.Grid.GetFootprint(targetGuid);
            int currentDist = GridFootprint.ManhattanDistance(selfCoord, selfFp, targetCoord, targetFp);

            // BUG-061/PUL-014: "no hace falta moverse" y "no HAY forma de moverse" son casos
            // benignos, no errores — Succeeded (no-op) para que la Sequence siga y el
            // enemigo pueda atacar si ya está en rango. Antes devolvían Failed y un
            // AINode_Sequence sin Selector que lo absorbiera abortaba el turno ENTERO,
            // incluido el ataque (un enemigo en una isla de 1 celda del NavGraph quedaba
            // trabado para siempre). Ver AIWrapFallible/Isolate en los builders de bosses:
            // son Selector[Move, Wait] — con Wait siempre Succeeded, este cambio no altera
            // su resultado final, solo evita el salto a Wait.
            bool alreadyInBand = currentDist == desiredRange;
            // RequireLineOfSight: "en la banda" no alcanza si desde acá no se ve al target —
            // sin este chequeo el nodo se auto-engaña ("ya llegué") y nunca reintenta.
            if (alreadyInBand && RequireLineOfSight
                && !GridLineOfSight.HasClearLine(context.Grid, selfCoord, targetCoord, context.SelfGuid, targetGuid))
            {
                alreadyInBand = false;
            }
            if (alreadyInBand) return AIResult.Succeeded;                     // ya en la banda (y con LoS si se pidió)
            if (currentDist < desiredRange && !Retreat) return AIResult.Succeeded; // muy cerca, kite off

            int maxSteps = MaxSteps?.Read(context) ?? 3;

            if (RequireLineOfSight)
            {
                // El IAIPathPlanner no tiene noción de línea de visión (AIPathRequest no lleva ese
                // dato) — con RequireLineOfSight se resuelve acá mismo, sin pasar por el planner,
                // priorizando candidatos con LoS clara por sobre ajustar la distancia exacta: ver
                // al target es el propósito entero de este approach.
                var reachableLos = (context.Movement as IPathedMovementService)
                        ?.GetReachableAnchors(context.SelfGuid, maxSteps)
                    ?? context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
                if (reachableLos == null || reachableLos.Count == 0) return AIResult.Succeeded;

                var bestLos = selfCoord;
                bool bestHasLos = false;
                int bestLosErr = Mathf.Abs(currentDist - desiredRange);
                foreach (var candidate in reachableLos)
                {
                    int dist = GridFootprint.ManhattanDistance(candidate, selfFp, targetCoord, targetFp);
                    int err = Mathf.Abs(dist - desiredRange);
                    bool hasLos = GridLineOfSight.HasClearLine(
                        context.Grid, candidate, targetCoord, context.SelfGuid, targetGuid);

                    bool better = (hasLos && !bestHasLos) || (hasLos == bestHasLos && err < bestLosErr);
                    if (!better) continue;

                    bestHasLos = hasLos;
                    bestLosErr = err;
                    bestLos = candidate;
                }

                if (bestLos == selfCoord) return AIResult.Succeeded; // ningún tile alcanzable mejora
                if (!context.Movement.Move(context.SelfGuid, bestLos)) return AIResult.Succeeded;
            }
            // Con planner en el contexto, la decisión es suya (conciencia de casillas
            // especiales; con sala limpia su resultado es idéntico al scoring de abajo).
            else if (context.PathPlanner != null)
            {
                if (!AIPathMoveExecutor.TryPlanAndMove(context, targetCoord, maxSteps, desiredRange,
                        Pathing.MoveIntent.Approach))
                {
                    return AIResult.Succeeded; // NoMove del planner (isla, sin tile mejor): no-op
                }
            }
            else
            {
                // GetReachableAnchors respeta el footprint (un 2×2 no entra por un pasillo
                // de 1); los fakes de tests sin la interfaz aditiva degradan al BFS 1×1.
                var reachable = (context.Movement as IPathedMovementService)
                        ?.GetReachableAnchors(context.SelfGuid, maxSteps)
                    ?? context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
                if (reachable == null || reachable.Count == 0) return AIResult.Succeeded; // sin candidato BFS

                // Score único: minimizar |dist(target) - desiredRange|. Cubre acercarse,
                // frenar en la banda y alejar (kite) con la misma pasada. Strict '<' =>
                // determinista y, ante empate con quedarse quieto, no se mueve.
                // Veto de retroceso + fase 2, en paridad con AIPathPlanner (ver TerrainPathCost y
                // UnstickApproach ahí para el criterio completo). Sólo al acercarse: el mapa de
                // terreno es null cuando ya está en banda o demasiado cerca, y el veto queda inerte.
                var terrain = currentDist > desiredRange
                    ? GridPathDistance.ComputeFrom(context.Grid, targetCoord, context.SelfGuid, targetGuid, occupantCost: 0)
                    : null;
                int originTerrain = PathCostOf(terrain, selfCoord);

                var best = selfCoord;
                int bestErr = Mathf.Abs(currentDist - desiredRange);
                foreach (var candidate in reachable)
                {
                    int err = Mathf.Abs(GridFootprint.ManhattanDistance(candidate, selfFp, targetCoord, targetFp) - desiredRange);
                    if (err >= bestErr) continue;
                    if (PathCostOf(terrain, candidate) > originTerrain) continue;
                    bestErr = err;
                    best = candidate;
                }

                // Fase 2 (desbloqueo): si Manhattan no encontró nada mejor, puede ser que la línea
                // recta esté tapada (pared, mesa, otro enemigo) y CUALQUIER casilla alcanzable
                // empeore el score aunque sea la que empieza a rodear. Ahí decide el costo de
                // camino REAL. Paridad exacta con AIPathPlanner.UnstickApproach — ver su remarks
                // para el criterio y el porqué de no filtrar por error de Manhattan.
                if (best == selfCoord && currentDist > desiredRange)
                {
                    // Acá SÍ con penalidad de ocupante: rodear aliados es el punto del desbloqueo.
                    var pathCost = GridPathDistance.ComputeFrom(context.Grid, targetCoord, context.SelfGuid, targetGuid);
                    int bestCost = PathCostOf(pathCost, selfCoord);
                    foreach (var candidate in reachable)
                    {
                        int c = PathCostOf(pathCost, candidate);
                        if (c >= bestCost) continue; // '<' estricto: empate ⇒ no mover
                        bestCost = c;
                        best = candidate;
                    }
                }

                if (best == selfCoord) return AIResult.Succeeded; // ningún tile alcanzable mejora la banda

                if (!context.Movement.Move(context.SelfGuid, best))
                    return AIResult.Succeeded; // Move rechazó el destino (ocupado, etc.)
            }

            // Solo el movimiento efectivo consume la acción — los Failed de arriba
            // (ya en banda, sin tile mejor) dejan la acción disponible.
            context.MarkExecuted(ActionKey);

            var wait = context.VisualService?.WaitForMoveComplete(context.SelfGuid);
            if (wait != null)
            {
                context.PendingWait = wait;
                return AIResult.Running;
            }
            return AIResult.Succeeded;
        }

        /// <summary>Costo de camino de una celda; <see cref="int.MaxValue"/> si no hay ruta (o si
        /// no se computó el mapa, lo que deja el veto inerte).</summary>
        private static int PathCostOf(Dictionary<GridCoord, int> pathCost, GridCoord c)
        {
            if (pathCost == null) return int.MaxValue;
            return pathCost.TryGetValue(c, out var v) ? v : int.MaxValue;
        }
    }
}
