using System.Collections.Generic;
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
    /// </remarks>
    public abstract class BlockEditorWindow<T> : EditorWindow where T : ScriptableObject
    {
        const float LEFT_WIDTH = 230f;

        readonly List<T> _assets = new List<T>();
        readonly PolymorphicAuthoringContext _ctx = new PolymorphicAuthoringContext();

        T _selected;
        BlockGraphNode _selectedNode;
        string _search = string.Empty;
        int _tabIndex; // 0 = graph, 1 = raw data
        Vector2 _leftScroll, _panelScroll, _dataScroll;

        BlockGraphView _graph;
        IMGUIContainer _leftPanel;
        IMGUIContainer _sidePanel;
        IMGUIContainer _dataPanel;
        VisualElement _rightHost;
        VisualElement _graphTab;
        Button _tabGraph, _tabData;

        // ---- subclass contract --------------------------------------------

        /// <summary>Folder new assets are created in, e.g. <c>Assets/Rollgeon/Items</c>.</summary>
        protected abstract string DefaultFolder { get; }

        /// <summary>File name stem for new assets, e.g. <c>Item_New</c>.</summary>
        protected abstract string NewAssetName { get; }

        /// <summary>Label for the list row. Falls back to the asset's file name.</summary>
        protected virtual string LabelOf(T asset) => asset != null ? asset.name : "(null)";

        /// <summary>Extra searchable text (ids, display names) beyond the file name.</summary>
        protected virtual string SearchTextOf(T asset) => LabelOf(asset);

        /// <summary>Optional per-asset warnings drawn above the graph.</summary>
        protected virtual void DrawIssues(T asset) { }

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

            var tabBar = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 28, marginBottom = 4 } };
            _tabGraph = MakeTab("Graph", () => SwitchTab(0));
            _tabData = MakeTab("Raw Data", () => SwitchTab(1));
            tabBar.Add(_tabGraph);
            tabBar.Add(_tabData);
            rightCol.Add(tabBar);

            _rightHost = new VisualElement { style = { flexGrow = 1 } };
            rightCol.Add(_rightHost);

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

            // The context repaints both panels itself so GenericMenu picks show up immediately —
            // those callbacks fire outside the IMGUI cycle.
            _ctx.Changed += () =>
            {
                _sidePanel?.MarkDirtyRepaint();
                _dataPanel?.MarkDirtyRepaint();
                _graph?.Rebuild();
            };

            SwitchTab(0);
        }

        Button MakeTab(string label, System.Action onClick)
        {
            var b = new Button(onClick) { text = label };
            b.style.flexGrow = 1;
            b.style.height = 26;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            return b;
        }

        void SwitchTab(int index)
        {
            _tabIndex = index;
            _rightHost.Clear();
            _rightHost.Add(index == 0 ? _graphTab : (VisualElement)_dataPanel);
            _tabGraph.style.backgroundColor = index == 0 ? new Color(0.30f, 0.40f, 0.55f) : new Color(0.20f, 0.20f, 0.20f);
            _tabData.style.backgroundColor = index == 1 ? new Color(0.30f, 0.40f, 0.55f) : new Color(0.20f, 0.20f, 0.20f);
        }

        // ---- left list -----------------------------------------------------

        void DrawLeft()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string next = GUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (next != _search) _search = next;
            }

            EditorGUILayout.Space(2);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            int shown = 0;
            foreach (var asset in _assets)
            {
                if (!Matches(asset)) continue;
                shown++;
                bool isSel = asset == _selected;
                var prev = GUI.backgroundColor;
                if (isSel) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                if (GUILayout.Button(LabelOf(asset), GUILayout.Height(24f))) Select(asset);
                GUI.backgroundColor = prev;
            }
            if (shown == 0)
                EditorGUILayout.HelpBox(_assets.Count == 0 ? "No assets found." : "Nothing matches the filter.", MessageType.Info);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(6);

            if (GUILayout.Button("+ Create", GUILayout.Height(24f))) CreateAsset();

            using (new EditorGUI.DisabledScope(_selected == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Duplicate")) DuplicateSelected();
                    if (GUILayout.Button("Delete")) DeleteSelected();
                }
                if (GUILayout.Button("Ping in Project")) EditorGUIUtility.PingObject(_selected);
            }
        }

        bool Matches(T asset)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            var text = SearchTextOf(asset);
            return text != null && text.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void Select(T asset)
        {
            if (_selected == asset) return;
            _selected = asset;
            _selectedNode = null;
            _ctx.Bind(asset);
            _graph.Bind(asset, _ctx);
            _graph.FrameGraph();
            _sidePanel?.MarkDirtyRepaint();
            _dataPanel?.MarkDirtyRepaint();
        }

        // ---- panels ---------------------------------------------------------

        void DrawSidePanel()
        {
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select an asset on the left.", MessageType.Info);
                return;
            }

            _ctx.UpdateTree();
            _panelScroll = EditorGUILayout.BeginScrollView(_panelScroll);

            DrawIssues(_selected);

            if (_selectedNode == null)
            {
                EditorGUILayout.HelpBox("Select a block in the graph to edit it.", MessageType.Info);
            }
            else if (_selectedNode.Kind == BlockNodeKind.Root)
            {
                // The asset's own fields — id, display name, icon, cooldown… Everything except the
                // blocks, which are nodes to the right. Saves a trip to the Raw Data tab for the
                // fields an author touches most.
                BlockPanelStyles.DrawNodeHeader(_selectedNode);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    PolymorphicBlockDrawer.DrawNode(
                        _ctx, _selected, string.Empty, PolymorphicBlockDrawer.Options.Default);
                }

                if (_selectedNode.Children.Count > 0)
                {
                    EditorGUILayout.Space(6);
                    BlockPanelStyles.DrawChildrenSummary(_selectedNode);
                }
            }
            else
            {
                BlockPanelStyles.DrawNodeHeader(_selectedNode);

                // The cached path goes stale whenever a list shifts — re-resolve against the live
                // object before drawing, or the panel would edit the wrong element.
                if (!_ctx.PathPointsTo(_selectedNode.Path, _selectedNode.Value))
                {
                    var repaired = _ctx.FindPathTo(_selectedNode.Value);
                    if (string.IsNullOrEmpty(repaired))
                    {
                        EditorGUILayout.HelpBox(
                            "This block is no longer reachable — it was probably removed. " +
                            "Select another one.", MessageType.Warning);
                        EditorGUILayout.EndScrollView();
                        return;
                    }
                    _selectedNode.Path = repaired;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    PolymorphicBlockDrawer.DrawNode(
                        _ctx, _selectedNode.Value, _selectedNode.Path, PolymorphicBlockDrawer.Options.Default);
                }

                if (_selectedNode.Children.Count > 0)
                {
                    EditorGUILayout.Space(6);
                    BlockPanelStyles.DrawChildrenSummary(_selectedNode);
                }
            }

            EditorGUILayout.EndScrollView();
            _ctx.ApplyChanges();
        }

        void DrawRawData()
        {
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select an asset on the left.", MessageType.Info);
                return;
            }
            _ctx.UpdateTree();
            _dataScroll = EditorGUILayout.BeginScrollView(_dataScroll);
            _ctx.Tree?.Draw(false);
            EditorGUILayout.EndScrollView();
            _ctx.ApplyChanges();
        }

        // ---- CRUD -----------------------------------------------------------

        void RefreshList()
        {
            _assets.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) _assets.Add(asset);
            }
            _assets.Sort((a, b) => string.CompareOrdinal(LabelOf(a), LabelOf(b)));

            if (_selected != null && !_assets.Contains(_selected)) Select(null);
        }

        void CreateAsset()
        {
            if (!AssetDatabase.IsValidFolder(DefaultFolder))
            {
                System.IO.Directory.CreateDirectory(DefaultFolder);
                AssetDatabase.Refresh();
            }
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolder}/{NewAssetName}.asset");
            var asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            RefreshList();
            Select(asset);
        }

        void DuplicateSelected()
        {
            if (_selected == null) return;
            string src = AssetDatabase.GetAssetPath(_selected);
            string dst = AssetDatabase.GenerateUniqueAssetPath(src);
            if (!AssetDatabase.CopyAsset(src, dst)) return;
            AssetDatabase.SaveAssets();
            RefreshList();
            Select(AssetDatabase.LoadAssetAtPath<T>(dst));
        }

        void DeleteSelected()
        {
            if (_selected == null) return;
            if (!EditorUtility.DisplayDialog(
                    "Delete asset",
                    $"Delete '{LabelOf(_selected)}'? This cannot be undone.\n\nAnything referencing it " +
                    "(pools, catalogs) will be left with a missing reference.",
                    "Delete", "Cancel")) return;

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_selected));
            AssetDatabase.SaveAssets();
            Select(null);
            RefreshList();
        }
    }
}
