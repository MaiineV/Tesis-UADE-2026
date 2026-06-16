using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Targeting
{
    /// <summary>
    /// Selector que elige la entidad más cercana (distancia Manhattan en la grilla),
    /// filtrando candidatos por <see cref="EntityFilterMask"/> relativo al owner
    /// (Enemies, Allies, Player...). Pensado para nodos de movimiento (ej.
    /// <c>AINode_Move</c>) que necesitan "ir hacia el más cercano".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Fuentes.</b> Candidatos = entidades registradas en <see cref="AttributesManager"/>
    /// (<c>ctx.Attributes</c>) cuya relación con el owner (via
    /// <see cref="IEntityQueryService.GetRelationship"/>) intersecte <see cref="Relation"/>
    /// y que tengan posición en el <see cref="IGridManager"/>. El owner se excluye siempre.
    /// </para>
    /// <para>
    /// <b>Score.</b> Menor distancia Manhattan al owner gana. Entidades sin HP
    /// (<see cref="SkipDead"/>) o sin posición en grilla se descartan. Empate de distancia
    /// → menor <see cref="Guid.CompareTo"/> (determinismo).
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class TargetSelector_Nearest : BaseEnemyTargetSelector
    {
        [Title("Filter")]
        [Tooltip("Relación(es) que debe matchear el candidato respecto al owner. " +
                 "Combinable: ej. Enemies | Player.")]
        public EntityFilterMask Relation = EntityFilterMask.Player;

        [Tooltip("Excluye candidatos con Health <= 0. Default true.")]
        public bool SkipDead = true;

        public override string SelectorName => $"Nearest ({Relation})";

        public override Guid PickTarget(AIContext ctx, Guid ownerGuid)
        {
            if (ctx?.Attributes == null)
            {
                Debug.LogWarning("[TargetSelector_Nearest] AIContext.Attributes is null.");
                return Guid.Empty;
            }
            if (ctx.Grid == null)
            {
                Debug.LogWarning("[TargetSelector_Nearest] AIContext.Grid is null.");
                return Guid.Empty;
            }
            if (Relation == EntityFilterMask.None)
            {
                return Guid.Empty;
            }
            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var query) || query == null)
            {
                Debug.LogWarning("[TargetSelector_Nearest] IEntityQueryService not registered.");
                return Guid.Empty;
            }
            if (!ctx.Grid.TryGetPosition(ownerGuid, out var ownerCoord))
            {
                return Guid.Empty;
            }

            Guid best = Guid.Empty;
            int bestDist = int.MaxValue;

            foreach (var kvp in ctx.Attributes.EnumerateEntries())
            {
                var candidate = kvp.Key;
                if (candidate == Guid.Empty) continue;
                if (candidate == ownerGuid) continue;

                var rel = query.GetRelationship(ownerGuid, candidate);
                if ((Relation & rel) == 0) continue;

                if (SkipDead && IsDead(ctx.Attributes, candidate)) continue;
                if (!ctx.Grid.TryGetPosition(candidate, out var coord)) continue;

                int dist = ownerCoord.Manhattan(coord);
                if (dist < bestDist || (dist == bestDist && candidate.CompareTo(best) < 0))
                {
                    bestDist = dist;
                    best = candidate;
                }
            }

            return best;
        }

        private static bool IsDead(AttributesManager attrs, Guid guid)
        {
            var health = attrs.GetAttribute<Health>(guid);
            return health != null && health.Value <= 0;
        }
    }
}
