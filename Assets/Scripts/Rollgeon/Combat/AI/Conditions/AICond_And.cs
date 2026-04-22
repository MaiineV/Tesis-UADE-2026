using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Combat.AI.Conditions
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class AICond_And : AICondition
    {
        [OdinSerialize]
        public List<AICondition> Conditions = new List<AICondition>();

        public override string ConditionName => "AND";

        public override bool Evaluate(AIContext context)
        {
            if (Conditions == null || Conditions.Count == 0) return true;
            foreach (var c in Conditions)
            {
                if (c == null) continue;
                if (!c.Evaluate(context)) return false;
            }
            return true;
        }
    }
}
