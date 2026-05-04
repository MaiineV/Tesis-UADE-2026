using System;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
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
    /// Hermano de <see cref="AINode_Move"/>: aquel persigue, este "kitea". Pensado para
    /// arqueros / casters en el FP. Usa pathfinding (A*) para asegurar que el destino
    /// elegido sea alcanzable; ignora tiles sin ruta válida desde la posición actual.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_KeepDistance : AIActionNode
    {
        [MinValue(1)]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public int MaxSteps = 3;

        [MinValue(1)]
        [Tooltip("Distancia Manhattan al player que el enemigo intenta mantener. Si la actual " +
                 "ya es >= ideal, no se mueve.")]
        public int IdealDistance = 4;

        public override string NodeName => "Keep Distance From Player";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            if (context.Grid == null || context.Movement == null) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
                return AIResult.Failed;

            int currentDist = selfCoord.Manhattan(playerCoord);
            if (currentDist >= IdealDistance) return AIResult.Failed; // already at distance — no-op

            // Tiles alcanzables en MaxSteps. Elegimos el que más se acerca a IdealDistance
            // del player (capeado, no premiamos overshoot). Empate: el más cerca de Self
            // (menos pasos gastados).
            var reachable = context.Movement.GetReachableTiles(selfCoord, MaxSteps, includeOrigin: false);
            if (reachable == null || reachable.Count == 0) return AIResult.Failed;

            var best = selfCoord;
            int bestScore = currentDist;
            foreach (var candidate in reachable)
            {
                int dist = Mathf.Min(candidate.Manhattan(playerCoord), IdealDistance);
                if (dist <= bestScore) continue;

                // GetReachableTiles ya considera walkable + ocupados via BFS, así que
                // los candidatos son alcanzables por construcción — no hace falta un
                // FindPath extra acá. Confiamos en el subsistema.
                bestScore = dist;
                best = candidate;
            }

            if (best == selfCoord) return AIResult.Failed;

            // Move dispara OnEntityMoved con el path completo (A* via FindPath internamente);
            // la capa visual lo anima casilla-a-casilla.
            return context.Movement.Move(context.SelfGuid, best)
                ? AIResult.Succeeded
                : AIResult.Failed;
        }
    }
}
