using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

        public void ApplyChanges()
        {
            FlushNestedCollectionQueues();
            Tree?.ApplyChanges();
        }

        // ---- colas de colección anidadas -----------------------------------

        static readonly Dictionary<Type, FieldInfo> _changeQueueFields = new Dictionary<Type, FieldInfo>();

        /// <summary>
        /// Aplica los cambios de colección que el "+"/"✕" de Odin dejó ENCOLADOS en listas
        /// anidadas (ComboIds de un hook, PersistentModifiers, …).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Odin no aplica el click de una lista al instante: lo encola en el
        /// <see cref="ICollectionResolver"/> de ESA propiedad. <c>PropertyTree.ApplyChanges</c>
        /// solo vacía los resolvers del nivel raíz, y el flush de los anidados ocurre dentro
        /// de la draw-session completa de Odin (<c>BeginDraw</c>/<c>EndDraw</c>) — que estos
        /// paneles, al dibujar <c>prop.Draw()</c> a mano, nunca abren. Sin esto, el + de una
        /// lista anidada queda encolado para siempre y el click "no hace nada".
        /// </para>
        /// <para>
        /// Gate de eventos: mutar la lista durante <c>Layout</c> desalinea el control-count
        /// de IMGUI con el <c>Repaint</c> del mismo frame; al final de cualquier otro evento
        /// (el click ya consumido, o el Repaint) es seguro — el próximo par Layout/Repaint ve
        /// la lista nueva completa.
        /// </para>
        /// </remarks>
        void FlushNestedCollectionQueues()
        {
            if (Tree == null) return;
            var evt = Event.current;
            if (evt != null && evt.type == EventType.Layout) return;

            List<IApplyableResolver> pending = null;
            foreach (var prop in Tree.EnumerateTree(true))
            {
                if (prop.ChildResolver is ICollectionResolver collection && HasQueuedChanges(collection))
                    (pending ?? (pending = new List<IApplyableResolver>())).Add(collection);
            }
            if (pending == null) return;

            // Mismo modelo de undo whole-object que toda mutación de estas tools.
            RecordUndo("Edit Collection");
            foreach (var resolver in pending) resolver.ApplyChanges();
            MarkDirty();
            Notify();
        }

        /// <summary>
        /// Mira la <c>changeQueue</c> privada del resolver — <see cref="IApplyableResolver"/>
        /// no expone un <c>HasChanges</c>, y llamar <c>ApplyChanges</c> a ciegas impediría
        /// registrar el undo ANTES de la mutación. Si Odin renombra el campo en un upgrade,
        /// el fallback aplica igual (sin undo de ese paso, pero el + vuelve a funcionar).
        /// </summary>
        static bool HasQueuedChanges(ICollectionResolver resolver)
        {
            var type = resolver.GetType();
            if (!_changeQueueFields.TryGetValue(type, out var field))
            {
                for (var t = type; t != null && field == null; t = t.BaseType)
                    field = t.GetField("changeQueue", BindingFlags.Instance | BindingFlags.NonPublic);
                _changeQueueFields[type] = field;
            }

            if (field == null) return resolver.ApplyChanges();
            return field.GetValue(resolver) is ICollection queue && queue.Count > 0;
        }

        public void Dispose() => DisposeTree();

        void DisposeTree()
        {
            _tree?.Dispose();
            _tree = null;
        }
    }
}
