using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    /// <summary>
    /// Editing surface for Odin-serialized polymorphic data: the owning asset, its
    /// <see cref="PropertyTree"/>, and undo. Every tool that authors <c>EffectData</c>,
    /// enchantment triggers, combo passives or AI nodes goes through this.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The tree is rooted on the asset, never on the POCO being edited.</b> Rooting a
    /// <see cref="PropertyTree"/> at a plain object makes Odin resolve it with
    /// <c>SerializationBackend.None</c>, which silently drops every interface-typed field —
    /// exactly the fields these tools exist to author.
    /// </para>
    /// <para>
    /// <b>Undo is whole-object, and that is not a shortcut.</b> The hosts are
    /// <c>SerializedScriptableObject</c>, whose <c>OnAfterDeserialize</c> repopulates every
    /// <c>[OdinSerialize]</c> member from the <c>serializationData</c> blob <i>after</i> Unity's
    /// native pass. A <c>SerializedProperty</c> write never reaches that blob, so it reverts on
    /// the next reload. <c>Undo.RecordObject</c> works because the undo system serializes the
    /// object, which runs <c>OnBeforeSerialize</c> and regenerates the blob from live state.
    /// There is no granular undo available here — do not design for one.
    /// </para>
    /// </remarks>
    public sealed class PolymorphicAuthoringContext : IDisposable
    {
        PropertyTree _tree;

        /// <summary>The asset that owns the Odin blob. Undo and dirty target.</summary>
        public UnityEngine.Object Root { get; private set; }

        /// <summary>Raised after any mutation. Hosts subscribe to repaint and refresh views.</summary>
        public event Action Changed;

        public PolymorphicAuthoringContext(UnityEngine.Object root = null)
        {
            Root = root;
        }

        public PropertyTree Tree
        {
            get
            {
                if (_tree == null && Root != null) _tree = PropertyTree.Create(Root);
                return _tree;
            }
        }

        /// <summary>Point the context at another asset, disposing the previous tree.</summary>
        public void Bind(UnityEngine.Object root)
        {
            DisposeTree();
            Root = root;
        }

        public InspectorProperty At(string absolutePath)
        {
            if (Tree == null || string.IsNullOrEmpty(absolutePath)) return null;
            return Tree.GetPropertyAtPath(absolutePath);
        }

        /// <summary>
        /// Let Odin draw the property at <paramref name="absolutePath"/>.
        /// </summary>
        /// <remarks>
        /// A missing path is silent by default — it renders a placeholder rather than throwing,
        /// which is how a mistyped path can hide for weeks. Define <c>ROLLGEON_TOOLS_STRICT</c>
        /// to turn it into an error while working on the tools.
        /// </remarks>
        public void Draw(string absolutePath)
        {
            var prop = At(absolutePath);
            if (prop != null)
            {
                prop.Draw();
                return;
            }
#if ROLLGEON_TOOLS_STRICT
            Debug.LogError($"[Polymorphic] path not found on {(Root != null ? Root.name : "<null>")}: {absolutePath}");
#endif
            EditorGUILayout.LabelField(absolutePath, "(field not found)");
        }

        /// <summary>Snapshot the whole asset. Must be called <b>before</b> mutating.</summary>
        public void RecordUndo(string label)
        {
            if (Root != null) Undo.RecordObject(Root, label);
        }

        public void MarkDirty()
        {
            if (Root != null) EditorUtility.SetDirty(Root);
        }

        public void Notify()
        {
            Changed?.Invoke();
        }

        /// <summary>Record + mutate + dirty + notify, in the one order that works.</summary>
        public void Mutate(string undoLabel, Action mutation)
        {
            if (Root == null || mutation == null) return;
            RecordUndo(undoLabel);
            mutation();
            MarkDirty();
            Notify();
        }

        /// <summary>
        /// Path whose value reference-equals <paramref name="target"/>, or null if unreachable.
        /// Needed because polymorphic topologies shift paths on every structural edit, so a
        /// cached path cannot be trusted across mutations.
        /// </summary>
        public string FindPathTo(object target)
        {
            if (Tree == null || target == null) return null;
            foreach (var prop in Tree.EnumerateTree(true))
            {
                var value = prop.ValueEntry?.WeakSmartValue;
                if (ReferenceEquals(value, target)) return prop.Path;
            }
            return null;
        }

        public bool PathPointsTo(string absolutePath, object target)
        {
            if (Tree == null || target == null || string.IsNullOrEmpty(absolutePath)) return false;
            var prop = At(absolutePath);
            return prop != null && ReferenceEquals(prop.ValueEntry?.WeakSmartValue, target);
        }

        public void UpdateTree() => Tree?.UpdateTree();

        public void ApplyChanges() => Tree?.ApplyChanges();

        public void Dispose() => DisposeTree();

        void DisposeTree()
        {
            _tree?.Dispose();
            _tree = null;
        }
    }
}
