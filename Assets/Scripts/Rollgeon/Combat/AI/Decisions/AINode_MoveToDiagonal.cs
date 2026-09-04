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
    /// Movimiento "Ranged diagonal" (GDD: ataque a distancia solo en diagonales exactas): busca
    /// la casilla alcanzable este turno que quede en diagonal EXACTA con el jugador
    /// (|dx| == |dy|, dx != 0), priorizando la que caiga lo más cerca posible de
    /// <see cref="DesiredRange"/>. Si ya está en diagonal <b>y dentro de esa distancia</b>, no se mueve. Si ninguna casilla
    /// alcanzable alinea, se acerca en general al jugador por distancia de CAMINO real hasta que
    /// una diagonal quede a tiro.
    /// </summary>
    /// <remarks>
    /// Tercer hermano de <see cref="AINode_MoveToAlign"/> (fila/columna) y
    /// <see cref="AINode_MoveToLineOfSight"/> (cualquier ángulo + LoS) — mismo esqueleto
    /// (candidato "bueno" con preferencia hazard-free, más fallback de <see cref="GridPathDistance"/>
    /// para rodear obstáculos de forma convergente), pero con la condición de alineación de
    /// <see cref="PcTargetInRange"/>'s <c>TargetAlignment.DiagonalOnly</c>. Nodo aparte y no un
    /// parámetro nuevo en <see cref="AINode_MoveToAlign"/> a propósito: un campo nuevo en un nodo
    /// ya serializado en árboles existentes (Sniper) nace en el valor CLR default al deserializar
    /// data vieja (Odin no corre field initializers), y el default de <c>TargetAlignment</c> es
    /// <c>Any</c> — agregarlo ahí habría re-interpretado retroactivamente al Sniper como "cualquier
    /// ángulo sirve", rompiendo su restricción de línea recta en silencio.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_MoveToDiagonal : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Cantidad máxima de tiles a recorrer en un turno.")]
        public AIIntReader MaxSteps;

        [OdinSerialize]
        [Tooltip("Distancia Chebyshev preferida una vez en diagonal — entre las casillas diagonales " +
                 "alcanzables, elige la más cercana a este valor. También es el tope para " +
                 "considerarse 'ya en posición': en diagonal pero MÁS LEJOS que esto sigue " +
                 "acercándose. Vacío = sin tope (comportamiento viejo).")]
        public AIIntReader DesiredRange;

        public override string NodeName => "Move To Diagonal";

        /// <summary>Key propia: alinearse en diagonal es una acción distinta de acercarse o kitear.</summary>
        public const string ActionKey = "__move_to_diagonal";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            if (context.HasExecuted(ActionKey)) return AIResult.Succeeded;
            if (context.Grid == null || context.Movement == null) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
                return AIResult.Failed;

            // "Ya llegué" = en diagonal exacta + DENTRO de DesiredRange (Chebyshev). El chequeo de
            // distancia no estaba (mismo BUG de playtest que AINode_MoveToAlign, ver su comentario):
            // un enemigo en diagonal exacta se consideraba en posición a CUALQUIER distancia, no se
            // movía más, y sus gates de ataque le fallaban por estar lejos — turno tras turno hasta
            // que el jugador se movía y rompía la diagonal.
            // El tope sólo rige si DesiredRange está autorado (null = sin tope, comportamiento
            // viejo); el default del SCORING de abajo sigue siendo 2, para no re-interpretar
            // árboles viejos que dejaron el campo en blanco.
            int arrivedWithin = DesiredRange?.Read(context) ?? int.MaxValue;
            if (IsDiagonal(selfCoord, playerCoord) && selfCoord.Chebyshev(playerCoord) <= arrivedWithin)
                return AIResult.Succeeded;

            int maxSteps = MaxSteps?.Read(context) ?? 3;
            int desiredRange = DesiredRange?.Read(context) ?? 2;

            var reachable = (context.Movement as IPathedMovementService)
                    ?.GetReachableAnchors(context.SelfGuid, maxSteps)
                ?? context.Movement.GetReachableTiles(selfCoord, maxSteps, includeOrigin: false);
            if (reachable == null || reachable.Count == 0) return AIResult.Succeeded;

            ServiceLocator.TryGetService<ISpecialTileAIQuery>(out var hazardTiles);
            GridCoord? bestDiagonal = null;
            int bestDiagonalScore = int.MaxValue;
            GridCoord? bestDiagonalSafe = null;
            int bestDiagonalSafeScore = int.MaxValue;

            // Semilla con la casilla ACTUAL si ya está en diagonal pero fuera de banda: sólo vale
            // moverse a una estrictamente mejor. Sin esto, ahora que "en diagonal" ya no corta
            // arriba, empataría con otra diagonal igual de buena y oscilaría turno a turno.
            if (IsDiagonal(selfCoord, playerCoord))
            {
                bestDiagonalScore = Mathf.Abs(selfCoord.Chebyshev(playerCoord) - desiredRange);
                if (!AIMovementHazard.IsDamaging(hazardTiles, context.SelfGuid, selfCoord))
                    bestDiagonalSafeScore = bestDiagonalScore;
            }

            foreach (var candidate in reachable)
            {
                if (!IsDiagonal(candidate, playerCoord)) continue;

                int score = Mathf.Abs(candidate.Chebyshev(playerCoord) - desiredRange);
                if (score < bestDiagonalScore) { bestDiagonalScore = score; bestDiagonal = candidate; }

                if (!AIMovementHazard.IsDamaging(hazardTiles, context.SelfGuid, candidate)
                    && score < bestDiagonalSafeScore)
                {
                    bestDiagonalSafeScore = score;
                    bestDiagonalSafe = candidate;
                }
            }

            GridCoord? target = bestDiagonalSafe ?? bestDiagonal;
            bool moved;
            if (target != null)
            {
                moved = context.Movement.Move(context.SelfGuid, target.Value);
            }
            else
            {
                // Sin candidato diagonal a mano este turno: acercarse por distancia de CAMINO
                // real (no Manhattan) para rodear obstáculos de forma convergente — mismo criterio
                // que los otros dos hermanos.
                moved = TryApproachByPathDistance(context, reachable, selfCoord, playerCoord);
            }

            if (!moved) return AIResult.Succeeded;

            context.MarkExecuted(ActionKey);

            var wait = context.VisualService?.WaitForMoveComplete(context.SelfGuid);
            if (wait != null)
            {
                context.PendingWait = wait;
                return AIResult.Running;
            }
            return AIResult.Succeeded;
        }

        /// <summary>Diagonal EXACTA: |dx| == |dy| y dx != 0 — mismo criterio que
        /// <c>PcTargetInRange</c>'s <c>diagAligned</c> (la propia celda no cuenta).</summary>
        private static bool IsDiagonal(GridCoord a, GridCoord b)
        {
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;
            return dx != 0 && Mathf.Abs(dx) == Mathf.Abs(dy);
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
                if (!pathDist.TryGetValue(candidate, out var dist)) continue;
                if (dist < bestDist) { bestDist = dist; best = candidate; }
            }

            return best != null && context.Movement.Move(context.SelfGuid, best.Value);
        }
    }
}
