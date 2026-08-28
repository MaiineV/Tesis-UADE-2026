using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic.Graph
{
    /// <summary>
    /// One human line describing what a block <b>does</b>, so the canvas can say
    /// "+30 daño al Full House" instead of the class name. Reflection-driven, like
    /// <see cref="BlockGraphModel"/>'s own title probing — new effect/condition/reader/hook
    /// families describe themselves without a case added here.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="BlockGraphModel"/> on purpose: the model answers "where does
    /// this node live" (path, parent, ownership — load-bearing for mutation), this answers "what
    /// would a designer want to read" (display only, never touched by <c>Detach</c>/<c>Add</c>).
    /// </remarks>
    public static class BlockNodeDescription
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const int MaxFields = 4;

        /// <summary>Body text for a node's detail area. Empty when there's nothing to add beyond
        /// the title/subtitle already on the header.</summary>
        public static string Describe(BlockGraphNode node)
        {
            if (node?.Value == null) return string.Empty;

            switch (node.Kind)
            {
                case BlockNodeKind.Root:   return DescribeRoot(node.Value);
                case BlockNodeKind.Group:  return DescribeGroup(node.Value);
                case BlockNodeKind.Effect: return DescribeEffect(node.Value);
                default:                   return DescribeFields(node.Value);
            }
        }

        /// <summary>Asset identity: a headline field (Type/Rarity — whichever exists) plus the
        /// authored description, by convention rather than by asset type so ItemSO and
        /// EnchantmentSO both describe themselves without this file knowing either exists.</summary>
        static string DescribeRoot(object asset)
        {
            var type = asset.GetType();
            string description = StringMember(type, asset, "Description", "Desc");
            string headline = EnumMember(type, asset, "Type", "Rarity");

            if (string.IsNullOrEmpty(description)) return headline ?? string.Empty;
            if (string.IsNullOrEmpty(headline)) return description;
            return $"{headline} — {description}";
        }

        /// <summary>Reflects the asset's Icon field/property, if any. Convention-based like the
        /// rest of this file — works for ItemSO.Icon and EnchantmentSO/UpgradeSO.Icon alike.</summary>
        public static Sprite TryGetIcon(BlockGraphNode node)
        {
            if (node?.Value == null) return null;
            var type = node.Value.GetType();

            var prop = type.GetProperty("Icon", Flags);
            if (prop != null && prop.PropertyType == typeof(Sprite) && prop.CanRead)
                return prop.GetValue(node.Value) as Sprite;

            var field = type.GetField("Icon", Flags);
            if (field != null && field.FieldType == typeof(Sprite))
                return field.GetValue(node.Value) as Sprite;

            return null;
        }

        /// <summary>EffectData already titles itself with its Label; the body says how many parts
        /// feed it, since that's the one thing the title can't show.</summary>
        static string DescribeGroup(object value)
        {
            int preConditions = CountOf(value, "PreConditions");
            int effects = CountOf(value, "Effects");

            var sb = new StringBuilder();
            if (preConditions > 0)
                sb.Append(preConditions).Append(preConditions == 1 ? " condition" : " conditions").Append("  ·  ");
            sb.Append(effects).Append(effects == 1 ? " effect" : " effects");
            return sb.ToString();
        }

        static int CountOf(object owner, string fieldName)
        {
            var field = owner.GetType().GetField(fieldName, Flags);
            return field?.GetValue(owner) is ICollection list ? list.Count : 0;
        }

        /// <summary>
        /// Effects prefer their own runtime tooltip — <see cref="Rollgeon.UI.Tooltips.IHasTooltipInfo.BuildTooltip()"/>
        /// is already the "+30 damage" text players see in combat, built and localized once.
        /// Safe to call outside play mode: every implementation checked (EffDealDamage, EffHeal,
        /// EffAddShield…) falls back to a static/default value when its runtime services aren't
        /// registered rather than throwing — <c>ServiceLocator.TryGetService</c> just returns
        /// false with an empty registry. Wrapped in try/catch anyway: this runs from an Editor
        /// window, and a badly-behaved future implementation must not be able to break the graph.
        /// </summary>
        static string DescribeEffect(object value)
        {
            if (value is Rollgeon.UI.Tooltips.IHasTooltipInfo tooltip)
            {
                string built = null;
                try { built = tooltip.BuildTooltip(); }
                catch { /* degrade to the generic field dump below */ }

                if (!string.IsNullOrEmpty(built)) return Flatten(built);
            }
            return DescribeFields(value);
        }

        /// <summary>
        /// Generic fallback: the first few primitive-ish fields as "Name: value", in declaration
        /// order. Used by conditions, readers, triggers, hooks and any effect without its own
        /// tooltip — anything the author didn't title. Reads private <c>[SerializeField]</c>
        /// backing fields too (that's most of the interesting data on these blocks), but only
        /// primitives/enums/strings — nested objects and lists are already nodes of their own.
        /// </summary>
        static string DescribeFields(object value)
        {
            var sb = new StringBuilder();
            int shown = 0;

            foreach (var field in value.GetType().GetFields(Flags))
            {
                if (shown >= MaxFields) break;
                if (field.Name.IndexOf("k__BackingField", StringComparison.Ordinal) >= 0) continue;
                if (!IsSimple(field.FieldType)) continue;

                object raw;
                try { raw = field.GetValue(value); }
                catch { continue; }

                string text = FormatValue(raw);
                if (string.IsNullOrEmpty(text)) continue;

                if (shown > 0) sb.Append("  ·  ");
                sb.Append(Prettify(field.Name)).Append(": ").Append(text);
                shown++;
            }
            return sb.ToString();
        }

        static bool IsSimple(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string);

        static string FormatValue(object raw)
        {
            switch (raw)
            {
                case null: return null;
                case string s: return string.IsNullOrEmpty(s) ? null : s;
                case float f: return f.ToString("0.##");
                case bool b: return b ? "true" : "false";
                default: return raw.ToString();
            }
        }

        static string StringMember(Type type, object instance, params string[] names)
        {
            foreach (var name in names)
            {
                var prop = type.GetProperty(name, Flags);
                if (prop != null && prop.PropertyType == typeof(string) && prop.CanRead)
                {
                    var s = prop.GetValue(instance) as string;
                    if (!string.IsNullOrEmpty(s)) return s;
                }

                var field = type.GetField(name, Flags);
                if (field != null && field.FieldType == typeof(string))
                {
                    var s = field.GetValue(instance) as string;
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            return null;
        }

        static string EnumMember(Type type, object instance, params string[] names)
        {
            foreach (var name in names)
            {
                var prop = type.GetProperty(name, Flags);
                if (prop != null && prop.PropertyType.IsEnum && prop.CanRead)
                    return prop.GetValue(instance)?.ToString();

                var field = type.GetField(name, Flags);
                if (field != null && field.FieldType.IsEnum)
                    return field.GetValue(instance)?.ToString();
            }
            return null;
        }

        /// <summary>Tooltip text can carry its own line breaks (combo/no-combo blurbs); the node
        /// body is one line, so collapse them into a single readable strip.</summary>
        static string Flatten(string text) => text.Replace("\r\n", " ").Replace('\n', ' ').Trim();

        /// <summary>`_baseAmount` → "Base Amount". Mirrors how Odin labels backing fields.</summary>
        static string Prettify(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int start = 0;
            while (start < name.Length && name[start] == '_') start++;
            if (start >= name.Length) return name;

            var sb = new StringBuilder();
            sb.Append(char.ToUpperInvariant(name[start]));
            for (int i = start + 1; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
                sb.Append(name[i]);
            }
            return sb.ToString();
        }
    }
}
