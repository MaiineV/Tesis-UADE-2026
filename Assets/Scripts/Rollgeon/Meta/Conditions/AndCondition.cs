using System;
using System.Collections.Generic;
using Sirenix.Serialization;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Composite AND (#164): se cumple cuando <b>todas</b> las sub-condiciones se
    /// cumplen. Se invalida apenas cualquier hijo se invalida (ya no puede cumplirse).
    /// </summary>
    [Serializable]
    public sealed class AndCondition : IUnlockCondition
    {
        [OdinSerialize]
        public List<IUnlockCondition> Children = new List<IUnlockCondition>();

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (Children == null || Children.Count == 0) return false;
            foreach (var child in Children)
            {
                if (child == null || !child.Evaluate(ctx)) return false;
            }
            return true;
        }

        public bool IsInvalidated(UnlockEvaluationContext ctx)
        {
            if (Children == null) return false;
            foreach (var child in Children)
            {
                if (child != null && child.IsInvalidated(ctx)) return true;
            }
            return false;
        }
    }
}
