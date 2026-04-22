using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Conditions
{
    /// <summary>
    /// Base polimórfica de las condiciones de árbol. TECHNICAL.md §7.5.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public abstract class AICondition
    {
        public virtual string ConditionName => GetType().Name;
        public abstract bool Evaluate(AIContext context);
    }
}
