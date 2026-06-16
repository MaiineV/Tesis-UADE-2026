using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Chequea presencia de un <see cref="Modifier{T}"/> activo sobre un stat
    /// concreto del owner. TECHNICAL.md §8.2.
    /// <para>
    /// Los filtros se aplican en AND — un modifier debe matchear todos los
    /// que estén configurados. Si no se configura ningún filtro extra, basta
    /// con que exista al menos un modifier en el stack del atributo.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class PCHasModifier : BasePreCondition
    {
        [OdinSerialize]
        [Tooltip("Stat sobre el que se chequea presencia de modifier (Energy, Health, …).")]
        public Type AttributeType;

        [Tooltip("Si != Guid.Empty, sólo cuenta modifiers cuyo SourceId matchee. " +
                 "Util para chequear 'me afecta el buff de la skill X'.")]
        public string SourceIdString;

        [Tooltip("Filtra por dirección. Cualquier dirección si no se marca.")]
        public bool FilterByDirection;

        [ShowIf(nameof(FilterByDirection))]
        public ModifierDirection Direction = ModifierDirection.Intrinsic;

        [Tooltip("Mínimo de modifiers que deben matchear para evaluar true.")]
        [MinValue(1)]
        public int MinCount = 1;

        public override string ConditionName =>
            $"HasModifier({AttributeType?.Name ?? "<null>"}{(FilterByDirection ? $", {Direction}" : "")}, x{MinCount})";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || AttributeType == null) return false;
            if (context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var manager)) return false;

            var attrs = manager.GetAttributes(context.OwnerGuid);
            if (attrs == null) return false;

            Guid sourceId = ParseGuidOrEmpty(SourceIdString);

            foreach (var entry in attrs.EnumerateEntries())
            {
                if (entry.Key != AttributeType) continue;
                int matches = CountMatching(entry.Value, sourceId, FilterByDirection, Direction);
                return matches >= MinCount;
            }

            return false;
        }

        private static int CountMatching(IModifiable attr, Guid sourceId, bool filterDir, ModifierDirection dir)
        {
            Type t = attr.GetValueType();
            if (t == typeof(int))   return CountTyped<int>(attr, sourceId, filterDir, dir);
            if (t == typeof(float)) return CountTyped<float>(attr, sourceId, filterDir, dir);
            if (t == typeof(bool))  return CountTyped<bool>(attr, sourceId, filterDir, dir);
            return 0;
        }

        private static int CountTyped<TVal>(IModifiable attr, Guid sourceId, bool filterDir, ModifierDirection dir)
        {
            if (attr is not BaseAttribute<TVal> baseAttr) return 0;
            int count = 0;
            foreach (var m in baseAttr.GetRawModifiers())
            {
                if (sourceId != Guid.Empty && m.SourceId != sourceId) continue;
                if (filterDir && m.Direction != dir) continue;
                count++;
            }
            return count;
        }

        private static Guid ParseGuidOrEmpty(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Guid.Empty;
            return Guid.TryParse(s, out var g) ? g : Guid.Empty;
        }
    }
}
