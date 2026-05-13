using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadEntityStat : EffectIntReader
    {
        public ReaderEntitySource Entity = ReaderEntitySource.Source;
        public StatType Stat = StatType.Attack;

        [Tooltip("True = include modifiers (buffs/debuffs). False = raw base value.")]
        public bool UseModified = true;

        public override int Read(EffectContext context)
        {
            if (context == null) return 0;

            var guid = Entity switch
            {
                ReaderEntitySource.Source => context.SourceGuid,
                ReaderEntitySource.Target => context.TargetGuid,
                _ => Guid.Empty,
            };
            if (guid == Guid.Empty) return 0;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
                return 0;

            return Stat switch
            {
                StatType.Health       => Get<Health>(attrs, guid),
                StatType.Attack       => Get<Attack>(attrs, guid),
                StatType.Speed        => Get<Speed>(attrs, guid),
                StatType.Energy       => Get<Energy>(attrs, guid),
                StatType.Shield       => Get<Shield>(attrs, guid),
                StatType.HealStrength => Get<HealStrength>(attrs, guid),
                _ => 0,
            };
        }

        private int Get<TAttr>(AttributesManager attrs, Guid entityId)
            where TAttr : class, IModifiable<int>
        {
            return UseModified
                ? attrs.GetAttributeModifiedValue<TAttr, int>(entityId)
                : attrs.GetAttributeValue<TAttr, int>(entityId);
        }
    }
}
