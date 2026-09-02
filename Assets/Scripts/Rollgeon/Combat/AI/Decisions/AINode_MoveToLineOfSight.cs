using System;
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
    /// Movimiento "Ranged omnidireccional": busca la casilla alcanzable este turno que quede a
    /// ≤ <see cref="Range"/> del jugador CON línea de visión libre en cualquier ángulo (Bresenham
    /// vía <see cref="GridLineOfSight"/>), priorizando la que caiga lo más cerca posible de
    /// <see cref="DesiredRange"/>. Si ya cumple ambas, no se mueve. Si ninguna casilla alcanzable
    /// cumple, se acerca en general al jugador por distancia de CAMINO real (rodea obstáculos)
    /// hasta que un ángulo libre quede a tiro.
    /// </summary>
    /// <remarks>
    /// Hermano de <see cref="AINode_MoveToAlign"/> (Sniper): mismo esqueleto — candidato "bueno"
    /// vs. fallback de acercamiento por <see cref="GridPathDistance"/> — pero sin la restricción
    /// de fila/columna, porque el atacante que usa este nodo dispara en cualquier dirección (ej.
    /// GDD Ranged Kiter, AoE en rango) y solo le importa la línea de visión puntual, no el ángulo.
    /// Comparte los helpers de grid con MoveToAlign en vez de duplicarlos.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_MoveToLineOfSight : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public AIIntReader MaxSteps;

        [OdinSerialize]
        [Tooltip("Rango máximo (Manhattan) al que este nodo considera una casilla 'a tiro'. " +
                 "Normalmente el mismo Range que usa el PcTargetInRange de ataque del árbol.")]
        public AIIntReader Range;

        [OdinSerialize]
        [Tooltip("Distancia Manhattan preferida entre las casillas candidatas (a rango y con LoS " +
                 "libre) — elige la más cercana a este valor, para no pegarse innecesariamente.")]
        public AIIntReader DesiredRange;

        public override string NodeName => "Move To Line Of Sight (any angle)";

        /// <summary>Key propia: reposicionarse por LoS es una acción distinta de acercarse o kitear.</summary>
        public const string ActionKey = "__move_to_los";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            // Ya se movió para buscar LoS este turno → no-op transparente (Succeeded), mismo
            // criterio que AINode_MoveToAlign/KeepDistance: un Failed acá abortaría el While padre.
            if (context.HasExecuted(ActionKey)) return AIResult.Succeeded;
            if (context.Grid == null || context.Movement == null) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
                return AIResult.Failed;

            int range = Range?.Read(context) ?? 5;

            // Ya a tiro y con LoS libre — nada que hacer.
            if (selfCoord.Manhattan(playerCoord) <= range
                && GridLineOfSight.HasClearLine(context.Grid, selfCoord, playerCoord, context.SelfGuid, context.PlayerGuid))
                return AIResult.Succeeded;

            int maxSteps = MaxSteps?.Read(context) ?? 3;
            int desiredRange = DesiredRange?.Read(context) ?? range;

            var reachable = (context.Movement as IPathedMovementService)
                    ?.GetReachableAnchors(context.SelfGuid, maxSteps)
                ?? context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
            if (reachable == null || reachable.Count == 0) return AIResult.Succeeded;

            // Candidato a rango Y con LoS libre en cualquier ángulo — el único objetivo "de
            // verdad" de este nodo. Dos niveles, mismo criterio que AINode_MoveToAlign: preferí
            // uno que no le haga daño pisarlo (BUG de playtest, el Sniper se paraba en su propio
            // fuego) antes que uno que sí, y ese antes que congelarse.
            ServiceLocator.TryGetService<ISpecialTileAIQuery>(out var hazardTiles);
            GridCoord? bestGood = null;
            int bestGoodScore = int.MaxValue;
            GridCoord? bestGoodSafe = null;
            int bestGoodSafeScore = int.MaxValue;
            foreach (var candidate in reachable)
            {
                int dist = candidate.Manhattan(playerCoord);
                if (dist > range) continue;
                if (!GridLineOfSight.HasClearLine(context.Grid, candidate, playerCoord, context.SelfGuid, context.PlayerGuid))
                    continue;

                int score = Mathf.Abs(dist - desiredRange);
                if (score < bestGoodScore) { bestGoodScore = score; bestGood = candidate; }

                if (!AIMovementHazard.IsDamaging(hazardTiles, context.SelfGuid, candidate)
                    && score < bestGoodSafeScore)
                {
                    bestGoodSafeScore = score;
                    bestGoodSafe = candidate;
                }
            }

            GridCoord? target = bestGoodSafe ?? bestGood;
            bool moved;
            if (target != null)
            {
                moved = context.Movement.Move(context.SelfGuid, target.Value);
            }
            else
            {
                // Sin candidato a rango+LoS a mano este turno: acercarse por distancia de CAMINO
                // real (no Manhattan) para rodear obstáculos de forma convergente — mismo criterio
                // que AINode_MoveToAlign, ver su comentario para el porqué (Manhattan en línea
                // recta no distingue "seguir derecho contra la pared" de "empezar a bordearla").
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

        private static bool TryApproachByPathDistance(AIContext context, System.Collections.Generic.IReadOnlyCollection<GridCoord> reachable,
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
    }
}
