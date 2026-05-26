using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si existe al menos un aliado vivo (HP &gt; 0) del owner. Reemplaza al viejo
    /// <c>AICond_AllyAlive</c> usando <see cref="IEntityQueryService"/> en vez del scan
    /// directo de <see cref="AttributesManager"/>.
    /// </summary>
    /// <remarks>
    /// Si el query service no está registrado, fallback al scan permisivo del manager
    /// (todo entity que no sea el owner) — mantiene paridad con el comportamiento previo
    /// para no romper tests/escenas legacy.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcAllyAliveExists : BasePreCondition
    {
        public override string ConditionName => "Ally alive exists";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs)) return false;

            if (ServiceLocator.TryGetService<IEntityQueryService>(out var query) && query != null)
            {
                var allies = query.GetAllAlliesOf(context.OwnerGuid);
                if (allies == null) return false;
                foreach (var ally in allies)
                {
                    if (ally == null) continue;
                    if (ally.Guid == context.OwnerGuid) continue;
                    var hp = attrs.GetAttribute<Health>(ally.Guid);
                    if (hp != null && hp.ModifiedValue > 0) return true;
                }
                return false;
            }

            // Fallback: scan all registered entities except the owner.
            foreach (var kvp in attrs.EnumerateEntries())
            {
                if (kvp.Key == context.OwnerGuid) continue;
                var hp = attrs.GetAttribute<Health>(kvp.Key);
                if (hp != null && hp.ModifiedValue > 0) return true;
            }
            return false;
        }
    }
}
