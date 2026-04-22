using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Combat.AI.Conditions
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class AICond_Not : AICondition
    {
        [OdinSerialize]
        public AICondition Inner;

        public override string ConditionName => "NOT";

        public override bool Evaluate(AIContext context)
        {
            if (Inner == null) return true;
            return !Inner.Evaluate(context);
        }
    }
}
