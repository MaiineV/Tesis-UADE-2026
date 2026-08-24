using System;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Movimiento "Ranged": mantiene una distancia óptima al player. Si está demasiado
    /// cerca, busca el tile alcanzable en este turno que maximice la distancia Manhattan
    /// hasta <see cref="IdealDistance"/>. Si ya está a distancia ideal o más, no se mueve.
    /// TECHNICAL.md §17.§B (kiting).
    /// </summary>
    /// <remarks>
    /// BUG-061/PUL-014 (fix): devuelve <c>Succeeded</c> — no <c>Failed</c> — en el caso benigno
    /// "no hay nada que kitear" (ya estoy a distancia ideal, no hay tile mejor, el planner
    /// devolvió NoMove). Antes devolvía <c>Failed</c> y, sin un <c>Selector(KeepDistance, Wait)</c>
    /// que lo absorbiera, un <see cref="AINode_Sequence"/> puesto <b>antes</b> del ataque le
    /// abortaba el turno ENTERO al enemigo cuando el player estaba lejos (o cuando el propio
    /// enemigo quedaba aislado en el NavGraph). Solo los guard clauses de error real
    /// (contexto/servicios ausentes, posición sin resolver) siguen devolviendo <c>Failed</c>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_KeepDistance : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public AIIntReader MaxSteps;

        [OdinSerialize]
        [Tooltip("Distancia Manhattan al player que el enemigo intenta mantener. Si la actual " +
                 "ya es >= ideal, no se mueve.")]
        public AIIntReader IdealDistance;

        public override string NodeName => "Keep Distance From Player";

        /// <summary>Key propia (≠ AINode_Move): kitear es una acción distinta de acercarse.</summary>
        internal const string ActionKey = "__keep_distance";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            // Ya kiteó este turno → no-op transparente (Succeeded, no Failed — un Failed
            // abortaría el While padre) para que el loop siga drenando energía.
            if (context.HasExecuted(ActionKey)) return AIResult.Succeeded;
            if (context.Grid == null || context.Movement == null) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
                return AIResult.Failed;

            int idealDist = IdealDistance?.Read(context) ?? 4;
            int currentDist = selfCoord.Manhattan(playerCoord);
            // BUG-061/PUL-014: mismo criterio que AINode_Move — "no hace falta kitear" y "no
            // HAY forma de kitear" son benignos, Succeeded (no-op) para que la Sequence siga.
            // Evidencia de que es seguro (grep de builders + assets, ver reporte del agente):
            // el ÚNICO uso NO envuelto en Selector[…, Wait] es ED_RangedEnemy/ED_Healer, donde
            // este nodo va ANTES del segundo chequeo de rango dentro del While-body — Failed
            // hoy le come el ataque de ese turno (el propio <remarks> de la clase ya lo
            // advertía). El resto (Anotador vía Fallback, Cajero/Tahur/Generala no usan
            // KeepDistance) está en Selector[KeepDistance, Wait]; con AINode_Wait siempre
            // Succeeded, este cambio no altera su resultado final. El While-body de
            // RangedEnemy/Healer decrementa energía incondicionalmente ANTES de este nodo
            // (primer hijo del Sequence), así que el loop sigue terminando por el contador de
            // energía (MaxEnergy chico) y no por MaxIterations.
            if (currentDist >= idealDist) return AIResult.Succeeded;

            int maxSteps = MaxSteps?.Read(context) ?? 3;

            // Con planner en el contexto, kitea con conciencia de casillas especiales.
            if (context.PathPlanner != null)
            {
                if (!AIPathMoveExecutor.TryPlanAndMove(context, playerCoord, maxSteps, idealDist,
                        Pathing.MoveIntent.Kite))
                {
                    return AIResult.Succeeded; // NoMove del planner: no-op
                }
            }
            else
            {
                var reachable = context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
                if (reachable == null || reachable.Count == 0) return AIResult.Succeeded; // sin candidato BFS

                var best = selfCoord;
                int bestScore = currentDist;
                foreach (var candidate in reachable)
                {
                    int dist = Mathf.Min(candidate.Manhattan(playerCoord), idealDist);
                    if (dist <= bestScore) continue;
                    bestScore = dist;
                    best = candidate;
                }

                if (best == selfCoord) return AIResult.Succeeded; // ningún tile alcanzable mejora el kite

                if (!context.Movement.Move(context.SelfGuid, best))
                    return AIResult.Succeeded; // Move rechazó el destino (ocupado, etc.)
            }

            // Solo el movimiento efectivo consume la acción — los Failed de arriba
            // (ya a distancia ideal, sin tile mejor) dejan la acción disponible.
            context.MarkExecuted(ActionKey);

            var wait = context.VisualService?.WaitForMoveComplete(context.SelfGuid);
            if (wait != null)
            {
                context.PendingWait = wait;
                return AIResult.Running;
            }
            return AIResult.Succeeded;
        }
    }
}
