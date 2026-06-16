using System;
using System.Collections.Generic;
using Sirenix.Serialization;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Composite OR (#164): se cumple cuando <b>alguna</b> sub-condición se cumple.
    /// Solo se invalida cuando todos los hijos quedaron invalidados.
    /// </summary>
    [Serializable]
    public sealed class OrCondition : IUnlockCondition
    {
        [OdinSerialize]
        public List<IUnlockCondition> Children = new List<IUnlockCondition>();

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (Children == null || Children.Count == 0) return false;
            foreach (var child in Children)
            {
                if (child != null && child.Evaluate(ctx)) return true;
            }
            return false;
        }

        public bool IsInvalidated(UnlockEvaluationContext ctx)
        {
            if (Children == null || Children.Count == 0) return false;
            foreach (var child in Children)
            {
                if (child == null) continue;
                if (!child.IsInvalidated(ctx)) return false;
            }
            return true;
        }
    }
}
