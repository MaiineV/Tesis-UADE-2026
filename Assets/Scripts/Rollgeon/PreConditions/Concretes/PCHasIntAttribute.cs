using System;
using Patterns;
using Rollgeon.Attributes;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Compara un stat <c>int</c> del owner contra un literal. TECHNICAL.md §8.2.
    /// <para>
    /// El <see cref="AttributeType"/> debe ser un concreto que implemente
    /// <see cref="IModifiable"/> con valor <c>int</c> (Energy, Health, …).
    /// Si la entidad no está registrada en <see cref="AttributesManager"/>
    /// o no tiene el stat, evalúa <c>false</c> — la responsabilidad de garantizar
    /// el stat corresponde al sistema que arma el behavior.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class PCHasIntAttribute : BasePreCondition
    {
        [OdinSerialize]
        [Tooltip("Tipo concreto del stat (debe extender BaseAttribute<int>: Energy, Health, …).")]
        public Type AttributeType;

        [Tooltip("Operador de comparación.")]
        public IntComparison Comparison = IntComparison.GreaterOrEqual;

        [Tooltip("Valor contra el que se compara.")]
        public int Value;

        [Tooltip("Si true, compara contra ModifiedValue (stack de modifiers Intrinsic). " +
                 "Si false, compara contra el raw Value.")]
        public bool UseModifiedValue = true;

        public override string ConditionName =>
            $"{AttributeType?.Name ?? "<null>"} {ComparisonSymbol(Comparison)} {Value}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || AttributeType == null) return false;
            if (context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var manager)) return false;

            var attrs = manager.GetAttributes(context.OwnerGuid);
            if (attrs == null) return false;

            foreach (var entry in attrs.EnumerateEntries())
            {
                if (entry.Key != AttributeType) continue;
                if (entry.Value.GetValueType() != typeof(int)) return false;

                int current = UseModifiedValue
                    ? entry.Value.GetModifiedValue<int>()
                    : entry.Value.GetValue<int>();
                return Compare(current, Value, Comparison);
            }

            return false;
        }

        private static bool Compare(int a, int b, IntComparison op) => op switch
        {
            IntComparison.Equal           => a == b,
            IntComparison.NotEqual        => a != b,
            IntComparison.Less            => a < b,
            IntComparison.LessOrEqual     => a <= b,
            IntComparison.Greater         => a > b,
            IntComparison.GreaterOrEqual  => a >= b,
            _                             => false,
        };

        private static string ComparisonSymbol(IntComparison op) => op switch
        {
            IntComparison.Equal           => "==",
            IntComparison.NotEqual        => "!=",
            IntComparison.Less            => "<",
            IntComparison.LessOrEqual     => "<=",
            IntComparison.Greater         => ">",
            IntComparison.GreaterOrEqual  => ">=",
            _                             => "?",
        };
    }
}
