using System;
using Rollgeon.Attributes.Stats;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Conditions
{
    /// <summary>
    /// True si existe al menos un aliado con <c>Health &gt; 0</c> además del Self.
    /// </summary>
    /// <remarks>
    /// "Aliado" en el FP = todo entity registrado en <see cref="AIContext.Attributes"/> que
    /// no sea Self ni Player. Cuando aparezca un registry de facciones se puede afinar.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AICond_AllyAlive : AICondition
    {
        public override string ConditionName => "Ally alive";

        public override bool Evaluate(AIContext context)
        {
            if (context?.Attributes == null) return false;

            foreach (var kvp in context.Attributes.EnumerateEntries())
            {
                var id = kvp.Key;
                if (id == context.SelfGuid || id == context.PlayerGuid) continue;
                var hp = context.Attributes.GetAttribute<Health>(id);
                if (hp == null) continue;
                if (hp.ModifiedValue > 0) return true;
            }
            return false;
        }
    }
}
