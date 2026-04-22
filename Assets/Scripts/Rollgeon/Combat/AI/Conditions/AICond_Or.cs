using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Combat.AI.Conditions
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class AICond_Or : AICondition
    {
        [OdinSerialize]
        public List<AICondition> Conditions = new List<AICondition>();

        public override string ConditionName => "OR";

        public override bool Evaluate(AIContext context)
        {
            if (Conditions == null || Conditions.Count == 0) return false;
            foreach (var c in Conditions)
            {
                if (c == null) continue;
                if (c.Evaluate(context)) return true;
            }
            return false;
        }
    }
}
