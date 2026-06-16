using System;
using System.Collections.Generic;
using System.Linq;
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
        [ValueDropdown(nameof(GetAttributeTypeChoices))]
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

        // El dropdown lista solo subtipos concretos de BaseAttribute<int> declarados
        // fuera de assemblies de test (los TestEnergy/TestHealth contaminan el picker
        // y son fuente clásica de errores al armar behaviors).
        private static IEnumerable<ValueDropdownItem<Type>> GetAttributeTypeChoices()
        {
#if UNITY_EDITOR
            if (_attributeTypeChoicesCache != null) return _attributeTypeChoicesCache;

            var baseType = typeof(BaseAttribute<int>);
            var items = new List<ValueDropdownItem<Type>>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (IsTestAssembly(asm)) continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || t.IsGenericTypeDefinition) continue;
                    if (!baseType.IsAssignableFrom(t)) continue;
                    if (t.Namespace != null && t.Namespace.Contains(".Tests")) continue;

                    items.Add(new ValueDropdownItem<Type>(t.Name, t));
                }
            }

            items.Sort((a, b) => string.CompareOrdinal(a.Text, b.Text));
            _attributeTypeChoicesCache = items;
            return _attributeTypeChoicesCache;
#else
            return System.Array.Empty<ValueDropdownItem<Type>>();
#endif
        }

#if UNITY_EDITOR
        private static List<ValueDropdownItem<Type>> _attributeTypeChoicesCache;

        private static bool IsTestAssembly(System.Reflection.Assembly asm)
        {
            var name = asm.GetName().Name;
            return name != null && (name.EndsWith(".Tests") || name.Contains(".Tests."));
        }
#endif
    }
}
