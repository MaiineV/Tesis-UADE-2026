using System;
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

            if (!RoomCenterResolver.TryResolve(grid, context.SelfGuid, selfCoord, out var destination))
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
