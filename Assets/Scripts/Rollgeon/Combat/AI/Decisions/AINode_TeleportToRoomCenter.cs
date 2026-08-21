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
    /// <b>Para qué.</b> Un ataque anclado en el jefe que cubre "toda la sala menos su cuadrado"
    /// sólo se lee si ese cuadrado está donde el jugador lo espera. Con el jefe pegado a una pared,
    /// la única zona segura queda partida por el borde y el ataque pasa a leerse como "media sala al
    /// azar". Plantarlo en el centro primero es lo que convierte el área en una figura reconocible.
    /// </para>
    /// <para>
    /// <b>Reubicación, no caminata.</b> Va por <see cref="IPathedMovementService.Teleport"/> — la
    /// primitiva de reubicación instantánea del proyecto, la misma que usan los portales: mueve la
    /// ocupación de la grilla y dispara <c>OnEntityTeleported</c>, así que la capa visual reposiciona
    /// el pawn sin que este nodo toque un transform. Como no "entra" a ninguna casilla, tampoco
    /// dispara los <c>OnEnter</c> de las especiales del camino, que es exactamente lo que se quiere:
    /// el jefe no se come su propio fuego por ir a ponerse en el medio.
    /// </para>
    /// <para>
    /// <b>El centro es el del bounding box de las casillas caminables</b>, no el centroide. Es el
    /// mismo centro con el que razonan las formas de telegrafía (<c>HalfRoom</c>,
    /// <c>GridPartition</c> parten la sala por sus bounds): si el nodo usara otro, el jefe quedaría
    /// en un "centro" que sus propias áreas no consideran el centro.
    /// </para>
    /// <para>
    /// <b>Sí puede devolver <c>Failed</c></b>, y quien lo monte tiene que contar con eso — un
    /// <c>AINode_Once</c> alrededor del setup de fase no latchea con Failed, así que el umbral se
    /// reintenta el turno siguiente en vez de perderse. Falla cuando: no hay contexto; falta el
    /// <c>IGridManager</c> o el <c>IMovementService</c> (tests EditMode sin servicio de
    /// movimiento); el que actúa no está registrado en la grilla; la sala no ofrece <b>ninguna</b>
    /// casilla caminable y libre (grafo stub sin bounds, o todo ocupado por otros); o el servicio de
    /// movimiento rechaza la reubicación. Cada uno de esos casos avisa por consola: un Failed mudo
    /// en este paso es indistinguible de un jefe que perdió el turno. En cambio <b>no</b> falla
    /// cuando ya está parado en el destino — ahí devuelve <c>Succeeded</c>, porque el estado pedido
    /// se cumple igual.
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

            // Los dos Failed de acá abajo avisan porque este nodo suele ir DESNUDO adentro de una
            // Sequence, justo antes del paso que ancla un área en la casilla del jefe: su Failed
            // corta el turno entero y deja sin latchear al AINode_Once que envuelve el setup de
            // fase. Un fallo así, callado, se lee como "el jefe perdió el turno" y no como
            // "faltaba un servicio".
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

            // Ya está en el destino: el centro estaba libre y es su propia casilla. Succeeded para no
            // abortar el Sequence del jefe — el estado pedido se cumple igual.
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
        /// "Usable" = caminable y libre, con la propia casilla del que actúa contando como libre: está
        /// ocupada por él mismo, y descartarla lo mandaría a dar un salto lateral cuando ya era lo más
        /// cerca del centro que hay.
        /// </remarks>
        private static bool TryResolveDestination(
            IGridManager grid, Guid selfGuid, GridCoord selfCoord, out GridCoord destination)
        {
            destination = selfCoord;

            // RoomTiles ya filtra caminable y devuelve vacío con el grafo stub "infinito", donde no
            // hay extensión que medir. Materializado porque se recorre dos veces (bounds + pick).
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
        /// Cercanía al centro primero. Los empates los rompe el salto más corto (se lee como un
        /// ajuste de posición y no como un teleport a la otra punta) y, si eso también empata, la
        /// casilla de menor (Y, X) — para que el destino no dependa del orden en que el grafo
        /// horneado enumera sus nodos.
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
        /// aditiva: la impl de runtime siempre la expone, pero los fakes de los tests EditMode
        /// implementan sólo <c>IMovementService</c> (ver el degradado gemelo en
        /// <c>AIPathMoveExecutor</c>). Camina en vez de teleportar, pero termina en la misma casilla.
        /// </remarks>
        private static bool Relocate(AIContext context, GridCoord destination)
        {
            if (context.Movement is IPathedMovementService pathed)
                return pathed.Teleport(context.SelfGuid, destination);

            return context.Movement.Move(context.SelfGuid, destination);
        }

        /// <remarks>
        /// <para>
        /// Las keys de <c>AINode_Move</c> y <c>AINode_KeepDistance</c> porque es el mismo presupuesto:
        /// reubicarse ES el movimiento del turno. Los dos, no uno: son budgets separados, así que
        /// marcar sólo el de Move deja al paso de "reacomodo" (KeepDistance, el último del Sequence en
        /// los jefes que huyen) sacando al jefe del centro en el mismo turno en que se plantó.
        /// </para>
        /// <para>
        /// Y eso no es sólo un movimiento de más: un área anclada en el jefe deja su hueco seguro
        /// donde el jefe estaba <b>al marcar</b>, y la ignición consume la marca sin recalcularla. Si
        /// el jefe se va después, el cuadrado a salvo queda vacío en el medio de la sala y deja de
        /// leerse como "el lugar donde está el jefe".
        /// </para>
        /// </remarks>
        private void ConsumeMove(AIContext context)
        {
            if (!ConsumeMoveAction) return;
            context.MarkExecuted(AINode_Move.ActionKey);
            context.MarkExecuted(AINode_KeepDistance.ActionKey);
        }
    }
}
