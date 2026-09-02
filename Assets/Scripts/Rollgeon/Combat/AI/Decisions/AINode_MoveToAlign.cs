using System;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Movimiento "Sniper": busca la casilla alcanzable este turno que quede en la MISMA fila
    /// o columna que el jugador (línea de tiro recta), priorizando la que caiga lo más cerca
    /// posible de <see cref="DesiredRange"/>. Si ya está alineado, no se mueve. Si ninguna
    /// casilla alcanzable alinea, se acerca en general (mismo criterio que Approach) para ir
    /// ganando terreno turno a turno hasta que una alineación quede a tiro.
    /// </summary>
    /// <remarks>
    /// No existía en el proyecto ningún <c>MoveIntent</c> de alineación (solo Approach/Kite en
    /// <see cref="Pathing.IAIPathPlanner"/>) — el resto de los Ranged (Skirmisher, Sniper stock)
    /// dependen de la suerte del pathing normal para caer alineados. Este nodo resuelve la
    /// alineación a mano, mismo patrón BFS que usa <see cref="AINode_KeepDistance"/> cuando no
    /// hay <c>PathPlanner</c> — no pasa por el planner porque no hay intent que le pida esto.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_MoveToAlign : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public AIIntReader MaxSteps;

        [OdinSerialize]
        [Tooltip("Distancia Manhattan preferida una vez alineado — entre las casillas alineadas " +
                 "alcanzables, elige la más cercana a este valor (para no pegarse innecesariamente).")]
        public AIIntReader DesiredRange;

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

            // Ya alineado — nada que hacer.
            if (selfCoord.X == playerCoord.X || selfCoord.Y == playerCoord.Y)
                return AIResult.Succeeded;

            int maxSteps = MaxSteps?.Read(context) ?? 3;
            int desiredRange = DesiredRange?.Read(context) ?? int.MaxValue;

            var reachable = (context.Movement as IPathedMovementService)
                    ?.GetReachableAnchors(context.SelfGuid, maxSteps)
                ?? context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
            if (reachable == null || reachable.Count == 0) return AIResult.Succeeded;

            GridCoord? bestAligned = null;
            int bestAlignedScore = int.MaxValue;
            GridCoord? bestApproach = null;
            int bestApproachDist = selfCoord.Manhattan(playerCoord);

            foreach (var candidate in reachable)
            {
                int dist = candidate.Manhattan(playerCoord);
                bool aligned = candidate.X == playerCoord.X || candidate.Y == playerCoord.Y;
                if (aligned)
                {
                    int score = Mathf.Abs(dist - desiredRange);
                    if (score < bestAlignedScore) { bestAlignedScore = score; bestAligned = candidate; }
                }
                else if (dist < bestApproachDist)
                {
                    bestApproachDist = dist;
                    bestApproach = candidate;
                }
            }

            // Prioridad: una casilla alineada alcanzable gana siempre sobre solo acercarse.
            var target = bestAligned ?? bestApproach;
            if (target == null) return AIResult.Succeeded; // ningún candidato mejora la situación

            if (!context.Movement.Move(context.SelfGuid, target.Value))
                return AIResult.Succeeded; // Move rechazó el destino (ocupado, etc.)

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
