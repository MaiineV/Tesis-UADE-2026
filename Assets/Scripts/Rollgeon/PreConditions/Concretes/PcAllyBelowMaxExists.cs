using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Entities;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si existe al menos un aliado vivo (HP &gt; 0) del owner cuya vida está por
    /// debajo de su HP máximo — es decir, hay alguien a quien curar. El Healer usa esta
    /// PC para no gastar el turno curando cuando todos los aliados están a full (o no
    /// queda ninguno vivo).
    /// </summary>
    /// <remarks>
    /// El max HP por aliado se lee de <see cref="IEnemyAIRegistry"/> (lo registra el spawn
    /// resolver). Si un aliado no tiene entry de max HP registrada, no podemos saber si
    /// está herido ⇒ no cuenta como target de cura (conservador). Sin
    /// <see cref="IEntityQueryService"/>, fallback al scan de <see cref="AttributesManager"/>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcAllyBelowMaxExists : BasePreCondition
    {
        public override string ConditionName => "Ally below max HP exists";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs)) return false;

            ServiceLocator.TryGetService<IEnemyAIRegistry>(out var aiRegistry);

            if (ServiceLocator.TryGetService<IEntityQueryService>(out var query) && query != null)
            {
                var allies = query.GetAllAlliesOf(context.OwnerGuid);
                if (allies == null) return false;
                foreach (var ally in allies)
                {
                    if (ally == null) continue;
                    if (ally.Guid == context.OwnerGuid) continue;
                    if (IsWounded(attrs, aiRegistry, ally.Guid)) return true;
                }
                return false;
            }

            // Fallback: scan all registered entities except the owner.
            foreach (var kvp in attrs.EnumerateEntries())
            {
                if (kvp.Key == context.OwnerGuid) continue;
                if (IsWounded(attrs, aiRegistry, kvp.Key)) return true;
            }
            return false;
        }

        private static bool IsWounded(AttributesManager attrs, IEnemyAIRegistry aiRegistry, Guid guid)
        {
            var hp = attrs.GetAttribute<Health>(guid);
            if (hp == null || hp.Value <= 0) return false;

            // Sin max HP conocido no podemos afirmar que esté herido.
            if (aiRegistry == null || !aiRegistry.TryGet(guid, out _, out var maxHp) || maxHp <= 0)
                return false;

            return hp.Value < maxHp;
        }
    }
}
