using System;
using System.Collections.Generic;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Reubica al que actúa en el centro de la sala. Sin campos obligatorios: el centro sale de la
    /// propia sala, así que el nodo sirve igual en cualquier arena sin autorar nada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va por <see cref="IPathedMovementService.Teleport"/> y no por un paso de caminata: como no
    /// "entra" a ninguna casilla, no dispara los <c>OnEnter</c> de las especiales del camino y el jefe
    /// no se come su propio fuego por ir a ponerse en el medio.
    /// </para>
    /// <para>
    /// El centro es el del bounding box de las casillas caminables y no el centroide, porque es el
    /// mismo centro con el que razonan las formas de telegrafía (<c>HalfRoom</c>,
    /// <c>GridPartition</c> parten la sala por sus bounds).
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TeleportToRoomCenter : AIActionNode
    {
        [Tooltip("Consume el presupuesto de movimiento del turno (Move y KeepDistance). Con esto en " +
                 "false, un paso de movimiento posterior en el mismo Sequence saca al jefe del centro " +
                 "en el mismo turno en que se plantó ahí.")]
        public bool ConsumeMoveAction = true;

        public override string NodeName => "Teleport To Room Center";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            // Los Failed de acá abajo avisan porque el nodo suele ir DESNUDO adentro de una Sequence:
            // su Failed corta el turno entero y deja sin latchear al AINode_Once que envuelve el setup
            // de fase, y callado se lee como "el jefe perdió el turno".
            var grid = context.Grid;
            if (grid == null || context.Movement == null)
            {
                Debug.LogWarning("[AINode_TeleportToRoomCenter] Sin IGridManager o sin " +
                                 "IMovementService en el contexto — no se reubica nada.");
                return AIResult.Failed;
            }
            if (!grid.TryGetPosition(context.SelfGuid, out var selfCoord))
            {
                Debug.LogWarning($"[AINode_TeleportToRoomCenter] {context.SelfGuid} no está " +
                                 "registrado en la grilla — no hay desde dónde reubicarlo.");
                return AIResult.Failed;
            }

            if (!TryResolveDestination(grid, context.SelfGuid, selfCoord, out var destination))
            {
                Debug.LogWarning("[AINode_TeleportToRoomCenter] La sala no tiene ninguna casilla " +
                                 "caminable libre — ¿grafo sin bounds? No se reubica nada.");
                return AIResult.Failed;
            }

            // Ya está en el destino: Succeeded para no abortar el Sequence del jefe — el estado
            // pedido se cumple igual.
            if (destination == selfCoord)
            {
                ConsumeMove(context);
                return AIResult.Succeeded;
            }

            if (!Relocate(context, destination))
            {
                Debug.LogWarning($"[AINode_TeleportToRoomCenter] No se pudo reubicar a {destination}.");
                return AIResult.Failed;
            }

            ConsumeMove(context);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Centro del bounding box de la sala si está usable, y si no la casilla usable más cercana.
        /// <c>false</c> sólo si la sala no ofrece ninguna.
        /// </summary>
        /// <remarks>
        /// "Usable" = caminable y libre, con la propia casilla del que actúa contando como libre:
        /// descartarla lo mandaría a dar un salto lateral cuando ya era lo más cerca del centro que hay.
        /// </remarks>
        private static bool TryResolveDestination(
            IGridManager grid, Guid selfGuid, GridCoord selfCoord, out GridCoord destination)
        {
            destination = selfCoord;

            // RoomTiles ya filtra caminable y devuelve vacío con el grafo stub "infinito". Materializado
            // porque se recorre dos veces (bounds + pick).
            var tiles = new List<GridCoord>(ThreatAreaShape.RoomTiles(grid));
            if (tiles.Count == 0) return false;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in tiles)
            {
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }

            // División entera: en un lado de largo par el centro cae en la casilla de abajo/izquierda.
            // Arbitrario pero estable — el jefe aterriza siempre en la misma, no alternando entre las
            // dos del medio de turno a turno.
            var center = new GridCoord((minX + maxX) / 2, (minY + maxY) / 2);

            bool found = false;
            int bestToCenter = int.MaxValue;
            int bestFromSelf = int.MaxValue;
            foreach (var c in tiles)
            {
                if (!IsFreeFor(grid, c, selfGuid)) continue;

                int toCenter = c.Manhattan(center);
                int fromSelf = c.Manhattan(selfCoord);
                if (found && !IsBetter(c, toCenter, fromSelf, destination, bestToCenter, bestFromSelf))
                    continue;

                destination = c;
                bestToCenter = toCenter;
                bestFromSelf = fromSelf;
                found = true;
            }

            return found;
        }

        private static bool IsFreeFor(IGridManager grid, GridCoord coord, Guid selfGuid)
        {
            if (!grid.IsOccupied(coord)) return true;
            return grid.TryGetOccupant(coord, out var occupant) && occupant == selfGuid;
        }

        /// <remarks>
        /// Cercanía al centro primero, empates por el salto más corto y después por menor (Y, X), para
        /// que el destino no dependa del orden en que el grafo horneado enumera sus nodos.
        /// </remarks>
        private static bool IsBetter(
            GridCoord candidate, int toCenter, int fromSelf,
            GridCoord best, int bestToCenter, int bestFromSelf)
        {
            if (toCenter != bestToCenter) return toCenter < bestToCenter;
            if (fromSelf != bestFromSelf) return fromSelf < bestFromSelf;
            if (candidate.Y != best.Y) return candidate.Y < best.Y;
            return candidate.X < best.X;
        }

        /// <remarks>
        /// Degradado a <see cref="IMovementService.Move"/> cuando el servicio no expone la interfaz
        /// aditiva: los fakes de los tests EditMode implementan sólo <c>IMovementService</c>. Camina en
        /// vez de teleportar, pero termina en la misma casilla.
        /// </remarks>
        private static bool Relocate(AIContext context, GridCoord destination)
        {
            if (context.Movement is IPathedMovementService pathed)
                return pathed.Teleport(context.SelfGuid, destination);

            return context.Movement.Move(context.SelfGuid, destination);
        }

        /// <remarks>
        /// Las keys de <c>AINode_Move</c> y <c>AINode_KeepDistance</c> porque es el mismo presupuesto:
        /// reubicarse ES el movimiento del turno. Los dos, no uno: son budgets separados, así que
        /// marcar sólo el de Move deja al paso de reacomodo sacando al jefe del centro en el mismo
        /// turno en que se plantó.
        /// </remarks>
        private void ConsumeMove(AIContext context)
        {
            if (!ConsumeMoveAction) return;
            context.MarkExecuted(AINode_Move.ActionKey);
            context.MarkExecuted(AINode_KeepDistance.ActionKey);
        }
    }
}
