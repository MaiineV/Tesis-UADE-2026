using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.Threat;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Flourish visual del aura defensiva del Guardian (Support del GDD): si al menos un aliado
    /// vivo cae dentro del radio este turno, dispara <see cref="CoverageAnimTrigger"/> en el
    /// Animator del portador. Puramente cosmético, corre TODOS los turnos del portador (se cuelga
    /// como primer hijo de un <c>Sequence</c>, antes de la decisión de ataque/movimiento) y nunca
    /// aborta el árbol.
    /// </summary>
    /// <remarks>
    /// La zona pintada en el piso (capa 1 del pedido original) se descartó por feedback del
    /// usuario ("queda descatualizada, es raro") — este nodo ya no toca
    /// <c>IThreatOverlayService</c> en absoluto, solo el Animator. <see cref="Radius"/> sigue
    /// siendo un parámetro propio del nodo (no lee <c>EnemyDataSO.AuraRadius</c>): mismo criterio
    /// que el resto del árbol, donde cada nodo trae sus números de diseño en vez de derivarlos de
    /// otro campo del SO.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_ShowGuardAura : AIActionNode
    {
        [OdinSerialize]
        [Tooltip("Radio Chebyshev a chequear — usar el mismo valor que AuraRadius en la ficha " +
                 "del enemigo (Support), mismo criterio que el gate de ataque melee (8 casillas " +
                 "alrededor con radio 1). No se lee de ahí: es un parámetro propio, igual que el " +
                 "resto de los nodos del árbol.")]
        public AIIntReader Radius;

        [Tooltip("Trigger del Animator a disparar cuando cubre a >=1 aliado vivo este turno. " +
                 "Vacío = sin flourish.")]
        public string CoverageAnimTrigger = "BoostDef";

        public override string NodeName => "Show Guard Aura";

        public override AIResult Tick(AIContext context)
        {
            // Puramente cosmético: cualquier falta de servicio es un no-op tolerante, nunca
            // Failed — no tiene sentido abortar la decisión de ataque/movimiento por esto.
            if (context == null || context.SelfGuid == Guid.Empty || context.Grid == null)
                return AIResult.Succeeded;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return AIResult.Succeeded;

            int radius = Radius != null ? Radius.Read(context) : 0;
            if (radius <= 0) return AIResult.Succeeded;

            var tiles = ThreatAreaShape.Compute(
                context.Grid, selfCoord, ThreatShape.SquareAroundPlayer, radius, HalfRoomAxis.Vertical);

            if (!string.IsNullOrEmpty(CoverageAnimTrigger)
                && CoversLivingAlly(context, tiles, selfCoord)
                && context.VisualService != null
                && context.VisualService.TryGetPawn(context.SelfGuid, out var pawn)
                && pawn != null)
            {
                pawn.TrySetTrigger(CoverageAnimTrigger);
            }

            return AIResult.Succeeded;
        }

        private static bool CoversLivingAlly(AIContext context, HashSet<GridCoord> tiles, GridCoord selfCoord)
        {
            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var query) || query == null)
                return false;

            foreach (var coord in tiles)
            {
                if (coord.Equals(selfCoord)) continue;
                if (!context.Grid.TryGetOccupant(coord, out var occupant) || occupant == Guid.Empty) continue;
                if ((query.GetRelationship(context.SelfGuid, occupant) & EntityFilterMask.Allies) != 0)
                    return true;
            }
            return false;
        }
    }
}
