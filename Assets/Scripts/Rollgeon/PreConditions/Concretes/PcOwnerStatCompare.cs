using System;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Compara un stat int del owner contra <see cref="Value"/> usando <see cref="Comparison"/>.
    /// PC genérico para construir conditions tipo "Energy > 0", "Health <= 5", "Speed == 3".
    /// Pensado para alimentar loops (<c>AINode_While</c>) y branches (<c>AINode_If</c>).
    /// </summary>
    /// <remarks>
    /// El <see cref="AttributesManager"/> llega via <see cref="PreConditionContext.Attributes"/>
    /// (lo popula el bridge AI). Si el caller no lo provee (ej. flujo héroe / effects sin AI),
    /// la PC retorna <c>true</c> — semántica permisiva igual que <c>PcRoundNumber</c>:
    /// "sin servicio → no vetamos". Mismo patrón <c>Get&lt;TAttr&gt;</c> que <c>AIReadSelfStat</c>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcOwnerStatCompare : BasePreCondition
    {
        public StatType Stat = StatType.Energy;

        public IntComparison Comparison = IntComparison.Greater;

        public int Value = 0;

        [Tooltip("True = incluye buffs/debuffs (ModifiedValue). False = valor base raw.")]
        public bool UseModified = true;

        public override string ConditionName => $"Owner.{Stat} {Symbol(Comparison)} {Value}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null) return true;
            if (context.Attributes == null) return true;
            if (context.OwnerGuid == Guid.Empty) return true;

            int current = ReadStat(context.Attributes, context.OwnerGuid);
            return Apply(current, Comparison, Value);
        }

        private int ReadStat(AttributesManager am, Guid owner) => Stat switch
        {
            StatType.Health       => Get<Health>(am, owner),
            StatType.Attack       => Get<Attack>(am, owner),
            StatType.Speed        => Get<Speed>(am, owner),
            StatType.Energy       => Get<Energy>(am, owner),
            StatType.Shield       => Get<Shield>(am, owner),
            StatType.HealStrength => Get<HealStrength>(am, owner),
            _                     => 0,
        };

        private int Get<TAttr>(AttributesManager am, Guid owner) where TAttr : class, IModifiable<int>
            => UseModified
                ? am.GetAttributeModifiedValue<TAttr, int>(owner)
                : am.GetAttributeValue<TAttr, int>(owner);

        private static bool Apply(int a, IntComparison op, int b) => op switch
        {
            IntComparison.Equal          => a == b,
            IntComparison.NotEqual       => a != b,
            IntComparison.Less           => a <  b,
            IntComparison.LessOrEqual    => a <= b,
            IntComparison.Greater        => a >  b,
            IntComparison.GreaterOrEqual => a >= b,
            _                            => false,
        };

        private static string Symbol(IntComparison op) => op switch
        {
            IntComparison.Equal          => "==",
            IntComparison.NotEqual       => "!=",
            IntComparison.Less           => "<",
            IntComparison.LessOrEqual    => "<=",
            IntComparison.Greater        => ">",
            IntComparison.GreaterOrEqual => ">=",
            _                            => "?",
        };
    }
}
