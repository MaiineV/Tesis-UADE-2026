using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Targeting
{
    /// <summary>
    /// Selector polimorfico que elige una entidad por valor extremo (min / max) de un
    /// <see cref="StatType"/>, filtrando candidatos por <see cref="EntityFilterMask"/>
    /// relativo al owner (Allies, Enemies, Player...). Generaliza el algoritmo manual de
    /// <c>SupportHealBehavior.PickLowestHpWoundedAlly</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Fuentes.</b> Candidatos = entidades registradas en <see cref="AttributesManager"/>
    /// cuya relacion con el owner (via <see cref="IEntityQueryService.GetRelationship"/>)
    /// intersecte <see cref="Relation"/>. El owner se excluye siempre (usa
    /// <c>TargetSelector_Self</c> si querias auto-target).
    /// </para>
    /// <para>
    /// <b>Score.</b> Lee el stat indicado (<see cref="UseModifiedValue"/> decide si toma
    /// <c>Value</c> base o <c>ModifiedValue</c> con buffs). Entidades sin el stat o sin
    /// HP (<see cref="SkipDead"/>) se descartan silenciosamente.
    /// </para>
    /// <para>
    /// <b>Tiebreak.</b> Si <see cref="UseTiebreaker"/>, en empate del stat primario
    /// resuelve por <see cref="TiebreakStat"/> en la direccion <see cref="TiebreakMode"/>.
    /// Empate final → <see cref="Guid.CompareTo"/> (determinismo).
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class TargetSelector_ByAttribute : BaseEnemyTargetSelector
    {
        [Title("Filter")]
        [Tooltip("Relacion(es) que debe matchear el candidato respecto al owner. " +
                 "Combinable: ej. Allies | Player para apuntar a cualquier no-enemigo.")]
        public EntityFilterMask Relation = EntityFilterMask.Allies;

        [Title("Primary Score")]
        [Tooltip("Atributo usado como score principal.")]
        public StatType Stat = StatType.Health;

        [Tooltip("Direccion del extremo: Lowest = menor valor gana; Highest = mayor valor gana.")]
        public ExtremumMode Mode = ExtremumMode.Lowest;

        [Tooltip("Si true, score = ModifiedValue (incluye modificadores). " +
                 "Si false, score = Value base.")]
        public bool UseModifiedValue;

        [Tooltip("Excluye candidatos con Health <= 0. Default true.")]
        public bool SkipDead = true;

        [Title("Tiebreaker")]
        [Tooltip("Si dos candidatos empatan en el stat primario, desempata por una segunda Stat.")]
        public bool UseTiebreaker;

        [ShowIf(nameof(UseTiebreaker))]
        public StatType TiebreakStat = StatType.Speed;

        [ShowIf(nameof(UseTiebreaker))]
        public ExtremumMode TiebreakMode = ExtremumMode.Lowest;

        public override string SelectorName =>
            $"By Attribute — {Mode} {Stat} ({Relation})";

        public override Guid PickTarget(AIContext ctx, Guid ownerGuid)
        {
            if (ctx?.Attributes == null)
            {
                Debug.LogWarning("[TargetSelector_ByAttribute] AIContext.Attributes is null.");
                return Guid.Empty;
            }
            if (Relation == EntityFilterMask.None)
            {
                return Guid.Empty;
            }
            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var query) || query == null)
            {
                Debug.LogWarning("[TargetSelector_ByAttribute] IEntityQueryService not registered.");
                return Guid.Empty;
            }

            Guid best = Guid.Empty;
            int bestScore = 0;
            int bestTiebreak = 0;
            bool hasTiebreakValue = false;

            foreach (var kvp in ctx.Attributes.EnumerateEntries())
            {
                var candidate = kvp.Key;
                if (candidate == Guid.Empty) continue;
                if (candidate == ownerGuid) continue;

                var rel = query.GetRelationship(ownerGuid, candidate);
                if ((Relation & rel) == 0) continue;

                if (SkipDead && IsDead(ctx.Attributes, candidate)) continue;
                if (!TryGetIntStat(ctx.Attributes, candidate, Stat, UseModifiedValue, out var score)) continue;

                int tiebreak = 0;
                bool gotTiebreak = false;
                if (UseTiebreaker)
                {
                    gotTiebreak = TryGetIntStat(ctx.Attributes, candidate, TiebreakStat, UseModifiedValue, out tiebreak);
                }

                if (best == Guid.Empty)
                {
                    best = candidate;
                    bestScore = score;
                    bestTiebreak = tiebreak;
                    hasTiebreakValue = gotTiebreak;
                    continue;
                }

                int cmp = CompareScore(score, bestScore, Mode);
                if (cmp < 0) continue;                         // candidate worse
                if (cmp > 0)                                   // candidate better
                {
                    best = candidate;
                    bestScore = score;
                    bestTiebreak = tiebreak;
                    hasTiebreakValue = gotTiebreak;
                    continue;
                }

                // tie on primary
                if (UseTiebreaker && hasTiebreakValue && gotTiebreak)
                {
                    int tcmp = CompareScore(tiebreak, bestTiebreak, TiebreakMode);
                    if (tcmp < 0) continue;
                    if (tcmp > 0)
                    {
                        best = candidate;
                        bestTiebreak = tiebreak;
                        continue;
                    }
                }

                // final tiebreak: lower Guid wins (deterministic)
                if (candidate.CompareTo(best) < 0)
                {
                    best = candidate;
                    bestTiebreak = tiebreak;
                    hasTiebreakValue = gotTiebreak;
                }
            }

            return best;
        }

        /// <summary>
        /// Returns 1 if <paramref name="candidate"/> beats <paramref name="current"/> under
        /// <paramref name="mode"/>; -1 if worse; 0 if tied.
        /// </summary>
        private static int CompareScore(int candidate, int current, ExtremumMode mode)
        {
            if (candidate == current) return 0;
            bool better = mode == ExtremumMode.Lowest
                ? candidate < current
                : candidate > current;
            return better ? 1 : -1;
        }

        private static bool IsDead(AttributesManager attrs, Guid guid)
        {
            var health = attrs.GetAttribute<Health>(guid);
            return health != null && health.Value <= 0;
        }

        private static bool TryGetIntStat(
            AttributesManager attrs, Guid guid, StatType stat, bool modified, out int value)
        {
            switch (stat)
            {
                case StatType.Health:       return TryRead<Health>(attrs, guid, modified, out value);
                case StatType.Attack:       return TryRead<Attack>(attrs, guid, modified, out value);
                case StatType.Speed:        return TryRead<Speed>(attrs, guid, modified, out value);
                case StatType.Energy:       return TryRead<Energy>(attrs, guid, modified, out value);
                case StatType.Shield:       return TryRead<Shield>(attrs, guid, modified, out value);
                case StatType.HealStrength: return TryRead<HealStrength>(attrs, guid, modified, out value);
                default:
                    value = 0;
                    return false;
            }
        }

        private static bool TryRead<TAttr>(AttributesManager attrs, Guid guid, bool modified, out int value)
            where TAttr : class, IModifiable<int>
        {
            var attr = attrs.GetAttribute<TAttr>(guid);
            if (attr == null)
            {
                value = 0;
                return false;
            }
            value = modified ? attr.ModifiedValue : attr.Value;
            return true;
        }
    }
}
