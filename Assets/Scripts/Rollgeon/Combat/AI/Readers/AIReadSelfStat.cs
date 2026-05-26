using System;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Readers
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class AIReadSelfStat : AIIntReader
    {
        public StatType Stat;

        [Tooltip("True = include intrinsic modifiers (buffs/debuffs). False = raw base value.")]
        public bool UseModified = true;

        public override int Read(AIContext context)
        {
            if (context?.Attributes == null) return 0;

            return Stat switch
            {
                StatType.Health => Get<Health>(context, context.SelfGuid),
                StatType.Attack => Get<Attack>(context, context.SelfGuid),
                StatType.Speed => Get<Speed>(context, context.SelfGuid),
                StatType.Energy => Get<Energy>(context, context.SelfGuid),
                StatType.Shield => Get<Shield>(context, context.SelfGuid),
                StatType.HealStrength => Get<HealStrength>(context, context.SelfGuid),
                _ => 0,
            };
        }

        private int Get<TAttr>(AIContext context, System.Guid entityId)
            where TAttr : class, IModifiable<int>
        {
            return UseModified
                ? context.Attributes.GetAttributeModifiedValue<TAttr, int>(entityId)
                : context.Attributes.GetAttributeValue<TAttr, int>(entityId);
        }
    }
}
