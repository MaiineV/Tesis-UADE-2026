using System;
using Rollgeon.Attributes.Stats;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Conditions
{
    /// <summary>
    /// True si Self.Health &lt; <see cref="Percent"/>% del max. Percent en 0..1.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AICond_HPBelow : AICondition
    {
        [Range(0f, 1f)]
        public float Percent = 0.5f;

        public override string ConditionName => $"Self HP < {Percent:P0}";

        public override bool Evaluate(AIContext context)
        {
            if (context?.Attributes == null) return false;
            if (context.SelfMaxHp <= 0) return false;
            var hp = context.Attributes.GetAttribute<Health>(context.SelfGuid);
            if (hp == null) return false;
            float ratio = (float)hp.ModifiedValue / context.SelfMaxHp;
            return ratio < Percent;
        }
    }
}
