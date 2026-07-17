using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    /// <summary>
    /// Reflection-driven dropdown for assigning concrete subtypes to polymorphic fields.
    /// <para>
    /// Odin hides its own picker whenever the <b>declared</b> type carries
    /// <c>[HideReferenceObjectPicker]</c> (project rule §13.6.1) — so <c>BasePreCondition</c>,
    /// <c>BaseEnemyTargetSelector</c> and <c>EffectIntReader</c> slots are unauthorable without
    /// this. Interface-typed slots (<c>IEffect</c>, <c>IEnchantmentTrigger</c>, <c>IFaceFilter</c>)
    /// do get Odin's picker while null, but lose it once assigned — every concrete declares the
    /// attribute — so re-typing in place is impossible there too.
    /// </para>
    /// <para>
    /// Tools therefore own the whole list UI (add / remove / assign) and let Odin draw only each
    /// element's inner fields. That keeps one consistent picker everywhere instead of one that
    /// appears and vanishes depending on the declared type.
    /// </para>
    /// </summary>
    public static class PolymorphicPicker
    {
        public static List<Type> ConcreteSubtypesOf(Type baseType)
        {
            var types = new List<Type>();
            // Include the base type itself when it's a concrete class (e.g. EnemyActionBehavior
            // has no derived classes — without this the picker would show an empty menu).
            if (IsValidPickerType(baseType)) types.Add(baseType);
            foreach (var t in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (!IsValidPickerType(t)) continue;
                types.Add(t);
            }
            types.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            return types;
        }

        static bool IsValidPickerType(Type t)
        {
            if (t.IsAbstract) return false;
            if (t.IsGenericType) return false;
            if (t.IsInterface) return false;
            // Hide types living in test assemblies (Eff_ReturnsConfigured, etc.).
            var asmName = t.Assembly.GetName().Name;
            if (asmName != null && asmName.EndsWith(".Tests", StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>
        /// Single-field assignment. Shows label + current type (or "(none)") + a button that
        /// opens a GenericMenu of concrete subtypes. <paramref name="onAssign"/> receives the
        /// new instance (or null when the user picks "Clear").
        /// </summary>
        public static void DrawSingle(
            string label, Type baseType, object current, Action<object> onAssign)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                string currentName = current != null ? current.GetType().Name : "(none)";
                if (GUILayout.Button(currentName + " ▾", EditorStyles.popup))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("(none)"), current == null, () => onAssign(null));
                    menu.AddSeparator(string.Empty);
                    foreach (var t in ConcreteSubtypesOf(baseType))
                    {
                        var capt = t;
                        bool selected = current != null && current.GetType() == t;
                        menu.AddItem(new GUIContent(t.Name), selected, () =>
                        {
                            onAssign(Activator.CreateInstance(capt));
                        });
                    }
                    menu.ShowAsContext();
                }
            }
        }

        /// <summary>
        /// "+ Add" button below an existing list. Mutates the list in place when the user picks
        /// a concrete type. The caller is responsible for invoking <paramref name="onAdded"/>
        /// to mark the host SO dirty and trigger a repaint.
        /// <para>
        /// <paramref name="onBeforeAdd"/> runs immediately before the mutation — it exists so the
        /// caller can record undo, which has to happen while the list is still in its old state.
        /// </para>
        /// </summary>
        public static void DrawAddButton(
            string label, Type baseType, IList list, Action onAdded, Action onBeforeAdd = null)
        {
            if (list == null) return;
            if (GUILayout.Button("+ Add " + label, GUILayout.Height(22f)))
            {
                var menu = new GenericMenu();
                foreach (var t in ConcreteSubtypesOf(baseType))
                {
                    var capt = t;
                    menu.AddItem(new GUIContent(t.Name), false, () =>
                    {
                        onBeforeAdd?.Invoke();
                        list.Add(Activator.CreateInstance(capt));
                        onAdded?.Invoke();
                    });
                }
                if (menu.GetItemCount() == 0)
                {
                    menu.AddDisabledItem(new GUIContent("(no concrete subtypes found)"));
                }
                menu.ShowAsContext();
            }
        }

        /// <summary>Compact ✕ button that nulls a field. Returns true if clicked.</summary>
        public static bool DrawClearButton()
        {
            return GUILayout.Button("✕", GUILayout.Width(22f));
        }
    }
}
