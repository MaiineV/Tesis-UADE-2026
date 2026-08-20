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
    /// <b>Cuidado al colgarlo de un Sequence.</b> Este nodo devuelve <c>Failed</c> en el caso
    /// benigno "no hay nada que kitear" (ya estoy a distancia ideal, no hay tile mejor), como
    /// manda el contrato de <see cref="AIActionNode"/>. Un <see cref="AINode_Sequence"/> aborta
    /// al primer <c>Failed</c>, así que si este nodo va <b>antes</b> del ataque, el enemigo
    /// deja de hacer TODO cuando el player está lejos. Le pasó al Sunken Grand: a 5+ casillas
    /// se quedaba quieto. Si tiene que ir primero, envolvelo en
    /// <c>Selector(KeepDistance, Wait)</c> — el idiom de "intentá esto, no importa si falla".
    /// Los árboles del ranged y del healer lo esquivan poniéndolo último.
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
            if (currentDist >= idealDist) return AIResult.Failed;

            int maxSteps = MaxSteps?.Read(context) ?? 3;

            // Con planner en el contexto, kitea con conciencia de casillas especiales.
            if (context.PathPlanner != null)
            {
                if (!AIPathMoveExecutor.TryPlanAndMove(context, playerCoord, maxSteps, idealDist,
                        Pathing.MoveIntent.Kite))
                {
                    return AIResult.Failed;
                }
            }
            else
            {
                var reachable = context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
                if (reachable == null || reachable.Count == 0) return AIResult.Failed;

                var best = selfCoord;
                int bestScore = currentDist;
                foreach (var candidate in reachable)
                {
                    int dist = Mathf.Min(candidate.Manhattan(playerCoord), idealDist);
                    if (dist <= bestScore) continue;
                    bestScore = dist;
                    best = candidate;
                }

                if (best == selfCoord) return AIResult.Failed;

                if (!context.Movement.Move(context.SelfGuid, best))
                    return AIResult.Failed;
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
