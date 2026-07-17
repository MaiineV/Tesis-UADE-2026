using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    /// <summary>One authorable polymorphic slot on a type — a field whose concrete type the designer picks.</summary>
    public readonly struct PolymorphicMember
    {
        /// <summary>Backing field. Used to read/write the value and to build the Odin path.</summary>
        public readonly FieldInfo Field;

        /// <summary>Odin path segment — the raw field name (Odin paths use field names verbatim).</summary>
        public readonly string Name;

        /// <summary>Declared type to pick from. For lists this is the element type.</summary>
        public readonly Type BaseType;

        /// <summary>True when the field is a <see cref="IList"/> of <see cref="BaseType"/>.</summary>
        public readonly bool IsList;

        /// <summary>Human label — from <c>[Title]</c> when present, else the prettified field name.</summary>
        public readonly string Title;

        public PolymorphicMember(FieldInfo field, Type baseType, bool isList, string title)
        {
            Field = field;
            Name = field.Name;
            BaseType = baseType;
            IsList = isList;
            Title = title;
        }
    }

    /// <summary>
    /// Finds the members of a type whose concrete type a designer must pick — i.e. every field
    /// (or list element) declared as an abstract class or interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> Verified against Odin on real assets: Odin hides its own type picker
    /// exactly when the <i>declared</i> type carries <c>[HideReferenceObjectPicker]</c> (project rule
    /// §13.6.1 puts it on every polymorphic base). So <c>BasePreCondition</c>,
    /// <c>BaseEnemyTargetSelector</c> and <c>EffectIntReader</c> slots cannot be authored at all in a
    /// stock inspector. Interface-typed slots (<c>IEffect</c>, <c>IEnchantmentTrigger</c>,
    /// <c>IFaceFilter</c>) do get a picker while null — but every concrete declares the attribute too,
    /// so the picker vanishes once assigned and the type can never be changed in place.
    /// </para>
    /// <para>
    /// The scan deliberately keys off "abstract or interface", <b>not</b> off the attribute: the
    /// attribute explains <i>why</i> a tool must own the picker, but a tool that owned the picker only
    /// where Odin dropped it would show one that appears and disappears depending on the declared type.
    /// Owning every polymorphic slot is what makes the UI uniform.
    /// </para>
    /// </remarks>
    public static class PolymorphicMemberScanner
    {
        static readonly Dictionary<Type, IReadOnlyList<PolymorphicMember>> _pickerCache =
            new Dictionary<Type, IReadOnlyList<PolymorphicMember>>();
        static readonly Dictionary<Type, IReadOnlyList<PolymorphicMember>> _blockCache =
            new Dictionary<Type, IReadOnlyList<PolymorphicMember>>();
        static readonly Dictionary<Type, bool> _hiddenDeepCache = new Dictionary<Type, bool>();

        /// <summary>Slots whose concrete type the designer picks — the fields a tool must own.</summary>
        public static IReadOnlyList<PolymorphicMember> Scan(Type type)
        {
            if (type == null) return Array.Empty<PolymorphicMember>();
            if (_pickerCache.TryGetValue(type, out var cached)) return cached;

            var found = new List<PolymorphicMember>();
            foreach (var field in SerializedFieldsOf(type))
            {
                var elementType = ElementTypeOf(field.FieldType, out bool isList);
                if (elementType == null) continue;
                if (!IsPickerSlot(elementType)) continue;

                found.Add(new PolymorphicMember(field, elementType, isList, TitleOf(field)));
            }

            _pickerCache[type] = found;
            return found;
        }

        /// <summary>
        /// Members that are <b>concrete</b> containers a tool has to walk into, because Odin cannot
        /// author what's underneath: <c>EffectData</c>, <c>ChainPhase</c>, <c>PassiveItemHook</c>…
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="Scan"/> finds where a <i>picker</i> goes; this finds where to <i>walk</i>.
        /// Both are needed: <c>ItemSO</c> has zero picker slots of its own, yet reaches three of them
        /// through <c>OnActivate</c>, and <c>EffChain</c> reaches more through
        /// <c>Phases → ChainPhase → Effects</c>.
        /// </para>
        /// <para>
        /// <b>The test is "contains a hidden picker below", not "contains a polymorphic slot".</b>
        /// That distinction is load-bearing: <c>SelectionSettings</c> holds an
        /// <c>ISelectionCountReader</c>, which Odin draws perfectly well — treating it as a container
        /// would make every effect render field-by-field and change how the working enemy tool looks,
        /// for nothing. A tool should take over traversal only where Odin actually fails.
        /// </para>
        /// </remarks>
        public static IReadOnlyList<PolymorphicMember> BlockMembersOf(Type type)
        {
            if (type == null) return Array.Empty<PolymorphicMember>();
            if (_blockCache.TryGetValue(type, out var cached)) return cached;

            var found = new List<PolymorphicMember>();
            foreach (var field in SerializedFieldsOf(type))
            {
                var elementType = ElementTypeOf(field.FieldType, out bool isList);
                if (elementType == null) continue;
                if (IsPickerSlot(elementType)) continue;              // that's a picker, not a container
                if (!IsInlineSerializableClass(elementType)) continue;
                if (!HasHiddenPickerDeep(elementType)) continue;

                found.Add(new PolymorphicMember(field, elementType, isList, TitleOf(field)));
            }

            _blockCache[type] = found;
            return found;
        }

        /// <summary>True when a designer must choose a concrete type for this slot.</summary>
        static bool IsPickerSlot(Type elementType)
        {
            if (!elementType.IsAbstract && !elementType.IsInterface) return false;
            // UnityEngine.Object references are asset links, not inline polymorphism —
            // Odin's object field already handles them.
            return !typeof(UnityEngine.Object).IsAssignableFrom(elementType);
        }

        /// <summary>
        /// Whether <paramref name="type"/> transitively holds a slot Odin refuses to pick for.
        /// Cycle-guarded: <c>EffChain → ChainPhase → EffectData → IEffect → EffChain</c> is real.
        /// </summary>
        public static bool HasHiddenPickerDeep(Type type)
        {
            if (type == null) return false;
            if (_hiddenDeepCache.TryGetValue(type, out bool cached)) return cached;

            // Only the entry point caches. A nested call can return false purely because the
            // cycle guard cut it short, and caching that would poison the type for good.
            bool result = Walk(type, new HashSet<Type>());
            _hiddenDeepCache[type] = result;
            return result;
        }

        static bool Walk(Type type, HashSet<Type> visited)
        {
            if (type == null || !visited.Add(type)) return false;

            foreach (var field in SerializedFieldsOf(type))
            {
                var elementType = ElementTypeOf(field.FieldType, out _);
                if (elementType == null) continue;

                if (IsPickerSlot(elementType))
                {
                    // Odin hides its picker exactly when the *declared* type carries the attribute.
                    if (elementType.IsDefined(typeof(HideReferenceObjectPickerAttribute), true)) return true;
                    continue;
                }

                if (IsInlineSerializableClass(elementType) && Walk(elementType, visited)) return true;
            }
            return false;
        }

        static bool IsInlineSerializableClass(Type t)
        {
            if (t.IsPrimitive || t.IsEnum || t == typeof(string)) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return false;
            if (!t.IsClass && !t.IsValueType) return false;
            if (t.Namespace != null && t.Namespace.StartsWith("System", StringComparison.Ordinal)) return false;
            if (t.Namespace != null && t.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal)) return false;
            return t.IsDefined(typeof(SerializableAttribute), false);
        }

        /// <summary>Clears the reflection caches. Call after a domain reload in long-lived tools.</summary>
        public static void ClearCache()
        {
            _pickerCache.Clear();
            _blockCache.Clear();
            _hiddenDeepCache.Clear();
        }

        /// <summary>
        /// Fields Odin will serialize: public instance fields, plus non-public ones opted in with
        /// <c>[OdinSerialize]</c>, <c>[SerializeField]</c> or <c>[SerializeReference]</c>.
        /// </summary>
        static IEnumerable<FieldInfo> SerializedFieldsOf(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var f in type.GetFields(flags))
            {
                if (f.IsDefined(typeof(NonSerializedAttribute), false)
                    && !f.IsDefined(typeof(OdinSerializeAttribute), false)) continue;

                bool optedIn = f.IsDefined(typeof(OdinSerializeAttribute), false)
                               || f.IsDefined(typeof(SerializeField), false)
                               || f.IsDefined(typeof(SerializeReference), false);

                if (f.IsPublic || optedIn) yield return f;
            }
        }

        /// <summary>
        /// The type a designer would pick for this field: the element type for lists, the field
        /// type otherwise. Returns null for types that are never inline-polymorphic.
        /// </summary>
        static Type ElementTypeOf(Type fieldType, out bool isList)
        {
            isList = false;
            if (fieldType == null) return null;
            if (fieldType.IsPrimitive || fieldType.IsEnum || fieldType == typeof(string)) return null;

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                isList = true;
                return fieldType.GetGenericArguments()[0];
            }
            if (fieldType.IsArray)
            {
                isList = true;
                return fieldType.GetElementType();
            }
            return fieldType;
        }

        static string TitleOf(FieldInfo field)
        {
            var title = field.GetCustomAttribute<TitleAttribute>(false);
            if (title != null && !string.IsNullOrEmpty(title.Title)) return title.Title;
            return Prettify(field.Name);
        }

        /// <summary>`_faceFilter` → "Face Filter". Mirrors how Odin labels backing fields.</summary>
        static string Prettify(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int start = 0;
            while (start < name.Length && (name[start] == '_' || name[start] == 'm' && start + 1 < name.Length && name[start + 1] == '_'))
                start += name[start] == '_' ? 1 : 2;
            if (start >= name.Length) return name;

            var sb = new System.Text.StringBuilder();
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
