using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Selection.Readers
{
    /// <summary>
    /// Cantidad de targets = valor de un stat del ejecutor, clampeado a [Min, Max].
    /// Ej: "apuntá a tantos enemigos como tu Attack". Mismo switch de stats que
    /// <see cref="Rollgeon.Effects.Readers.ReadEntityStat"/>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class StatCountReader : ISelectionCountReader
    {
        public StatType Stat = StatType.Attack;

        [Tooltip("True = include modifiers (buffs/debuffs). False = raw base value.")]
        public bool UseModified = true;

        [MinValue(1)]
        [Tooltip("Piso del count. También es el fallback defensivo sin owner/servicios.")]
        public int Min = 1;

        [MinValue(1)]
        [Tooltip("Techo del count (SelectionCount máximo autoreable es 16).")]
        public int Max = 16;

        public int Read(ReadInfo info)
        {
            if (info.ownerGuid == Guid.Empty) return Min;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
                return Min;

            var value = Stat switch
            {
                StatType.Health       => Get<Health>(attrs, info.ownerGuid),
                StatType.Attack       => Get<Attack>(attrs, info.ownerGuid),
                StatType.Speed        => Get<Speed>(attrs, info.ownerGuid),
                StatType.Energy       => Get<Energy>(attrs, info.ownerGuid),
                StatType.Shield       => Get<Shield>(attrs, info.ownerGuid),
                StatType.HealStrength => Get<HealStrength>(attrs, info.ownerGuid),
                StatType.AttackRange  => Get<AttackRange>(attrs, info.ownerGuid),
                _ => Min,
            };

            return Mathf.Clamp(value, Min, Math.Max(Min, Max));
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
