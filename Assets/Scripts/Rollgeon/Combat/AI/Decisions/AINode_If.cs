using System;
using Rollgeon.Combat.AI.Conditions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Ramifica según una <see cref="AICondition"/>. Ejecuta <see cref="Then"/> si
    /// la condición es true; <see cref="Else"/> si false. TECHNICAL.md §7.5.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_If : AIQuestionNode
    {
        [OdinSerialize] public AICondition Condition;
        [OdinSerialize] public AIDecisionNode Then;
        [OdinSerialize] public AIDecisionNode Else;

        public override string NodeName => "If";

        public override AIResult Tick(AIContext context)
        {
            bool cond = Condition != null && Condition.Evaluate(context);
            var branch = cond ? Then : Else;
            return branch?.Tick(context) ?? AIResult.Failed;
        }
    }
}
