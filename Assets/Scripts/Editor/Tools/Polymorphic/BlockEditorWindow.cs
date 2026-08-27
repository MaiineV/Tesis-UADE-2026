using Rollgeon.Editor.Tools.Polymorphic.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    /// <summary>
    /// Shell for authoring one family of Odin assets: searchable list on the left, and on the right
    /// a left-to-right graph of the asset's blocks next to the panel that edits the selected one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Layout follows <c>EnemyEditorWindow</c> (list + tabs + resize grip); the CRUD/search toolbar
    /// follows <c>HeroClassEditorWindow</c>. Repository caching and bulk operations are left out on
    /// purpose — <c>docs/tools/hero-class-editor-review.md</c> already ruled them YAGNI at this
    /// content volume, and re-scanning on <c>OnProjectChange</c> costs nothing for tens of assets.
    /// </para>
    /// <para>
    /// Subclasses supply the asset type and the folder; everything else is shared.
    /// </para>
    /// <para>
    /// <b>Split into partials by responsibility</b> (same convention as <c>RoomEditorWindow.*.cs</c>)
    /// so that concurrent work on the list, the CRUD flow, the inspector, the raw-data view and the
    /// host-declared tabs never lands in the same file:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>BlockEditorWindow.cs</c> — shell, lifecycle, root layout, shared state.</description></item>
    /// <item><description><c>BlockEditorWindow.List.cs</c> — the left list, search, filters, row rendering, selection.</description></item>
    /// <item><description><c>BlockEditorWindow.Crud.cs</c> — create / duplicate / delete / rename / catalog registration.</description></item>
    /// <item><description><c>BlockEditorWindow.Inspector.cs</c> — the side panel that edits the selected node.</description></item>
    /// <item><description><c>BlockEditorWindow.RawData.cs</c> — the raw Odin tree tab.</description></item>
    /// <item><description><c>BlockEditorWindow.Tabs.cs</c> — the tab host and <see cref="BlockEditorTabAttribute"/> discovery.</description></item>
    /// </list>
    /// <para>
    /// Each partial declares the host hooks it consumes, so the extension contract lives next to the
    /// code that calls it rather than in one shared block everybody has to edit.
    /// </para>
    /// </remarks>
    public abstract partial class BlockEditorWindow<T> : EditorWindow where T : ScriptableObject
    {
        const float LEFT_WIDTH = 230f;

        readonly PolymorphicAuthoringContext _ctx = new PolymorphicAuthoringContext();

        T _selected;
        BlockGraphNode _selectedNode;

        BlockGraphView _graph;
        IMGUIContainer _leftPanel;
        IMGUIContainer _sidePanel;
        IMGUIContainer _dataPanel;
        VisualElement _rightHost;
        VisualElement _graphTab;

        // ---- shared state exposed to hosts -----------------------------------

        /// <summary>The asset the list has selected, or null.</summary>
        protected T SelectedAsset => _selected;

        /// <summary>
        /// The authoring context bound to <see cref="SelectedAsset"/>. Exposed because a
        /// host-declared tab that edits data has no other legal way to mutate an Odin asset:
        /// every write must go through <see cref="PolymorphicAuthoringContext.Mutate"/> so the
        /// serialization blob is regenerated (see that type's remarks).
        /// </summary>
        protected PolymorphicAuthoringContext Context => _ctx;

        // ---- lifecycle -----------------------------------------------------

        protected virtual void OnEnable()
        {
            BuildUI();
            RefreshList();
            Undo.undoRedoPerformed += OnUndo;
        }

        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            _ctx.Dispose();
        }

        void OnProjectChange()
        {
            RefreshList();
            _leftPanel?.MarkDirtyRepaint();
        }

        void OnUndo()
        {
            // Undo re-deserializes the whole Odin blob, so every live reference the graph held is
            // orphaned — rebuild rather than trying to patch.
            _ctx.Bind(_selected);
            _selectedNode = null;
            _graph?.Bind(_selected, _ctx);
            _leftPanel?.MarkDirtyRepaint();
            _sidePanel?.MarkDirtyRepaint();
            MarkDeclaredTabsDirty();
        }

        // ---- UI ------------------------------------------------------------

        void BuildUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;

            _leftPanel = new IMGUIContainer(DrawLeft) { style = { width = LEFT_WIDTH, marginRight = 4 } };
            root.Add(_leftPanel);

            var rightCol = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };
            root.Add(rightCol);

            _rightHost = new VisualElement { style = { flexGrow = 1 } };

            _graph = new BlockGraphView { style = { flexGrow = 1 } };
            _graph.OnNodeSelected += node =>
            {
                _selectedNode = node;
                _sidePanel?.MarkDirtyRepaint();
            };
            _graph.OnStructureChanged += () =>
            {
                _sidePanel?.MarkDirtyRepaint();
                _dataPanel?.MarkDirtyRepaint();
                MarkDeclaredTabsDirty();
            };

            _sidePanel = new IMGUIContainer(DrawSidePanel)
            {
                style =
                {
                    width = 340, minWidth = 280, flexShrink = 0,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f),
                    paddingLeft = 8, paddingRight = 8, paddingTop = 8, paddingBottom = 8,
                },
            };

            _graphTab = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            _graphTab.Add(_graph);
            _graphTab.Add(_sidePanel);

            _dataPanel = new IMGUIContainer(DrawRawData) { style = { flexGrow = 1 } };

            // Tab bar is built after its contents exist, and added above the host so the bar sits on
            // top; the built-in tabs hand it elements that are already wired.
            BuildTabBar(rightCol);
            rightCol.Add(_rightHost);

            // The context repaints both panels itself so GenericMenu picks show up immediately —
            // those callbacks fire outside the IMGUI cycle.
            _ctx.Changed += () =>
            {
                _sidePanel?.MarkDirtyRepaint();
                _dataPanel?.MarkDirtyRepaint();
                MarkDeclaredTabsDirty();
                _graph?.Rebuild();
            };

            SwitchTab(0);
        }
    }
}
