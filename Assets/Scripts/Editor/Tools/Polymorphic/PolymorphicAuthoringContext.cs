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

        // ---- agrupación de undo ---------------------------------------------

        /// <summary>
        /// Collapses every undo step recorded inside the scope into a single named one.
        /// </summary>
        /// <example>
        /// <code>
        /// using (PolymorphicAuthoringContext.UndoGroup("Create Item"))
        /// {
        ///     // create the asset, register it in the catalog, write the localization keys,
        ///     // write the shop price…
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Creating one item touches four assets (spec §7.2). Without this, Unity files each write in
        /// its own group and a single Ctrl+Z undoes only the last one — leaving the author with a
        /// half-created item and no way to tell how many more times to press it.
        /// </para>
        /// <para>
        /// Purely additive and opt-in: it neither reads nor writes any context state, so nothing that
        /// already calls <see cref="Mutate"/> changes behaviour. Static for the same reason — a
        /// grouped operation usually spans several assets and therefore several contexts.
        /// </para>
        /// </remarks>
        public static UndoGroupScope UndoGroup(string name) => new UndoGroupScope(name);

        /// <summary>Scope returned by <see cref="UndoGroup"/>. Collapses on dispose.</summary>
        /// <remarks>
        /// A struct so the <c>using</c> costs no allocation, and the group index is captured on
        /// construction because <c>Undo.GetCurrentGroup</c> has already advanced by the time the
        /// scope ends.
        /// </remarks>
        public readonly struct UndoGroupScope : IDisposable
        {
            readonly int _group;

            internal UndoGroupScope(string name)
            {
                _group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(name);
            }

            public void Dispose() => Undo.CollapseUndoOperations(_group);
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

        /// <summary>
        /// Vuelca al asset lo que el panel editó este frame, y avisa si algo cambió de verdad.
        /// </summary>
        /// <remarks>
        /// El <c>Notify</c> es la parte que faltaba. <c>PropertyTree.ApplyChanges</c> devuelve
        /// <c>true</c> cuando efectivamente aplicó una edición, y ese retorno se estaba descartando:
        /// como editar un valor por el drawer de Odin no pasa por <see cref="Mutate"/>, nadie
        /// levantaba <see cref="Changed"/> y el grafo seguía mostrando el valor viejo hasta que uno
        /// cambiaba de asset y volvía — momento en que se reconstruía por otro camino.
        /// <para>
        /// Solo notifica cuando hubo cambio real, no en cada repaint: si notificara siempre, el grafo
        /// se reconstruiría en cada frame.
        /// </para>
        /// </remarks>
        public void ApplyChanges()
        {
            FlushNestedCollectionQueues();

            if (Tree == null) return;
            if (!Tree.ApplyChanges()) return;

            MarkDirty();
            Notify();
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
