using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Entities;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// Canvas that renders the <see cref="EnemyDataSO.AIRoot"/> as a graph and writes
    /// changes back through <see cref="AITreeSerializer.Save"/>. Inline parameter edits
    /// happen in the side panel exposed by <see cref="Inspector"/> — keeping IMGUI/Odin
    /// out of the GraphView Node body avoids the polymorphic-picker focus issues that
    /// caused condition lists to reset when edited inline.
    /// </summary>
    public sealed class AIDecisionTreeGraphView : GraphView
    {
        public AIDecisionTreeInspector Inspector { get; }

        EnemyDataSO _enemy;
        GraphSnapshot _snap;
        readonly Dictionary<AIDecisionNode, AIDecisionNodeView> _views = new Dictionary<AIDecisionNode, AIDecisionNodeView>();
        AINodeSearchProvider _searchProvider;
        EditorWindow _hostWindow;
        Label _statusLabel;
        bool _suppressChange;

        public AIDecisionTreeGraphView(EditorWindow hostWindow)
        {
            _hostWindow = hostWindow;
            Inspector = new AIDecisionTreeInspector(OnInspectorChanged);

            style.flexGrow = 1;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new RightClickPanManipulator());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
            nodeCreationRequest = OnNodeCreationRequest;

            _statusLabel = new Label();
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.left = 8;
            _statusLabel.style.top = 8;
            _statusLabel.style.color = new Color(1f, 0.5f, 0.5f);
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(_statusLabel);
        }

        public void Bind(EnemyDataSO enemy)
        {
            // DeleteElements fires graphViewChanged for every removed node/edge. Without
            // suppression, the teardown would mutate the previous enemy's snapshot and
            // overwrite its AIRoot with an empty tree. Same risk for AddElement during
            // BuildViewsFromSnapshot — guard the whole rebuild.
            _suppressChange = true;
            try
            {
                _snap = null;
                _enemy = null;
                ClearSelection();
                DeleteElements(graphElements.ToList());
                _views.Clear();

                _enemy = enemy;
                _statusLabel.text = string.Empty;
                Inspector.Bind(enemy);
                if (_enemy == null) return;

                _snap = AITreeSerializer.Load(_enemy.AIRoot);
                BuildViewsFromSnapshot();
            }
            finally
            {
                _suppressChange = false;
            }
        }

        // ---- snapshot ↔ views --------------------------------------------

        void BuildViewsFromSnapshot()
        {
            _views.Clear();

            var saved = AITreeLayoutSidecar.Load(_enemy, _snap);
            var auto = AITreeAutoLayout.Compute(_snap);

            foreach (var n in _snap.Nodes)
            {
                var view = new AIDecisionNodeView(n);
                Vector2 pos;
                if (saved != null && saved.TryGetValue(n, out var savedPos)) pos = savedPos;
                else if (auto != null && auto.TryGetValue(n, out var autoPos)) pos = autoPos;
                else pos = Vector2.zero;
                view.SetPosition(new Rect(pos, Vector2.zero));
                AddElement(view);
                _views[n] = view;
            }

            foreach (var e in _snap.Edges)
            {
                if (!_views.TryGetValue(e.Parent, out var parentView)) continue;
                if (!_views.TryGetValue(e.Child, out var childView)) continue;

                var outPort = FindFreeOutputForSlot(parentView, e.SlotIndex);
                if (outPort == null) continue;

                var edge = outPort.ConnectTo(childView.InputPort);
                AddElement(edge);
            }

            EnsureFreeDynamicPorts();
            RefreshRootIndicators();
        }

        void EnsureFreeDynamicPorts()
        {
            foreach (var view in _views.Values)
            {
                var slots = AITreeTopology.SlotsOf(view.Data);
                for (int i = 0; i < slots.Count; i++)
                {
                    if (!slots[i].IsDynamic) continue;
                    bool hasFree = false;
                    foreach (var p in view.OutputPorts)
                    {
                        if ((int)p.userData != i) continue;
                        if (!p.connected) { hasFree = true; break; }
                    }
                    if (!hasFree) view.AddOutputPortForSlot(slots[i], i);
                }
            }
        }

        static Port FindFreeOutputForSlot(AIDecisionNodeView view, int slotIndex)
        {
            foreach (var p in view.OutputPorts)
            {
                if ((int)p.userData != slotIndex) continue;
                if (!p.connected) return p;
            }
            var slots = AITreeTopology.SlotsOf(view.Data);
            for (int i = 0; i < slots.Count; i++)
            {
                if (i != slotIndex) continue;
                if (!slots[i].IsDynamic) return null;
                return view.AddOutputPortForSlot(slots[i], slotIndex);
            }
            return null;
        }

        // ---- port compatibility ------------------------------------------

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            foreach (var p in ports.ToList())
            {
                if (p == startPort) continue;
                if (p.direction == startPort.direction) continue;
                if (p.node == startPort.node) continue;
                compatible.Add(p);
            }
            return compatible;
        }

        // ---- selection → inspector --------------------------------------

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            UpdateInspectorFromSelection();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            UpdateInspectorFromSelection();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            UpdateInspectorFromSelection();
        }

        void UpdateInspectorFromSelection()
        {
            AIDecisionNodeView only = null;
            int count = 0;
            foreach (var s in selection)
            {
                if (s is AIDecisionNodeView nv) { only = nv; count++; }
            }
            Inspector.SetSelection(count == 1 ? only.Data : null);
        }

        void OnInspectorChanged()
        {
            // Inspector edited a node's inline field. Refresh the visual summary on canvas.
            foreach (var view in _views.Values) view.RefreshSummary();
        }

        // ---- root selection ---------------------------------------------

        /// <summary>
        /// Right-click context menu — adds "Set as Root" for the targeted node, on top of
        /// the default Cut/Copy/Paste/Delete entries provided by the base GraphView.
        /// </summary>
        /// <remarks>
        /// Three layers of target resolution because <c>evt.target</c> can be the node itself
        /// (right-click on the frame), a child element (click on title/port/label) — and Unity's
        /// <c>GetFirstAncestorOfType&lt;T&gt;()</c> starts at <c>parent</c>, not <c>self</c>.
        /// Selection fallback covers the rare case where the click bubble misses entirely.
        /// </remarks>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var nv = ResolveTargetNodeView(evt.target as VisualElement);
            if (nv != null && _snap != null)
            {
                bool alreadyRoot = (nv.Data == _snap.Root);
                evt.menu.AppendAction(
                    "Set as Root",
                    _ => SetRoot(nv.Data),
                    alreadyRoot ? DropdownMenuAction.AlwaysDisabled : DropdownMenuAction.AlwaysEnabled);
                evt.menu.AppendSeparator();
            }
            base.BuildContextualMenu(evt);
        }

        AIDecisionNodeView ResolveTargetNodeView(VisualElement target)
        {
            if (target is AIDecisionNodeView direct) return direct;
            var ancestor = target?.GetFirstAncestorOfType<AIDecisionNodeView>();
            if (ancestor != null) return ancestor;
            // Fallback: Unity GraphView typically auto-selects the right-clicked node.
            foreach (var s in selection)
                if (s is AIDecisionNodeView sel) return sel;
            return null;
        }

        void SetRoot(AIDecisionNode node)
        {
            if (_snap == null || node == null || _snap.Root == node) return;
            _snap.Root = node;
            RefreshRootIndicators();
            MarkDirty();
        }

        void RefreshRootIndicators()
        {
            if (_snap == null) return;
            foreach (var kv in _views) kv.Value.SetIsRoot(kv.Key == _snap.Root);
        }

        // ---- changes -----------------------------------------------------

        GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_suppressChange || _snap == null) return change;
            bool topologyChanged = false;

            if (change.elementsToRemove != null)
            {
                foreach (var el in change.elementsToRemove)
                {
                    if (el is AIDecisionNodeView nv)
                    {
                        _snap.Nodes.Remove(nv.Data);
                        _snap.Edges.RemoveAll(e => e.Parent == nv.Data || e.Child == nv.Data);
                        bool wasRoot = (_snap.Root == nv.Data);
                        if (wasRoot) _snap.Root = _snap.Nodes.Count > 0 ? _snap.Nodes[0] : null;
                        _views.Remove(nv.Data);
                        if (wasRoot) RefreshRootIndicators();
                        topologyChanged = true;
                    }
                    else if (el is Edge edge)
                    {
                        var parentView = edge.output.node as AIDecisionNodeView;
                        var childView = edge.input.node as AIDecisionNodeView;
                        if (parentView != null && childView != null)
                        {
                            int slot = (int)edge.output.userData;
                            _snap.Edges.RemoveAll(e =>
                                e.Parent == parentView.Data &&
                                e.Child == childView.Data &&
                                e.SlotIndex == slot);
                            topologyChanged = true;
                        }
                    }
                }
            }

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    var parentView = edge.output.node as AIDecisionNodeView;
                    var childView = edge.input.node as AIDecisionNodeView;
                    if (parentView == null || childView == null) continue;

                    int slot = (int)edge.output.userData;
                    _snap.Edges.Add(new GraphSnapshot.Edge(parentView.Data, slot, childView.Data));
                    topologyChanged = true;
                }
            }

            if (topologyChanged)
            {
                EnsureFreeDynamicPorts();
                MarkDirty();
            }
            else if (change.movedElements != null && change.movedElements.Count > 0)
            {
                SaveLayoutOnly();
            }
            return change;
        }

        void SaveLayoutOnly()
        {
            if (_enemy == null || _snap == null) return;
            var positions = new Dictionary<AIDecisionNode, Vector2>();
            foreach (var kv in _views)
                positions[kv.Key] = kv.Value.GetPosition().position;
            AITreeLayoutSidecar.Save(_enemy, _snap, positions);
        }

        // ---- node creation via SearchWindow ------------------------------

        void OnNodeCreationRequest(NodeCreationContext ctx)
        {
            if (_searchProvider == null)
            {
                _searchProvider = ScriptableObject.CreateInstance<AINodeSearchProvider>();
                _searchProvider.GraphView = this;
                _searchProvider.OnSelect = (type, screenPos) =>
                {
                    var graphPos = ScreenToGraphPosition(screenPos);
                    SpawnNode(type, graphPos);
                };
            }
            SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), _searchProvider);
        }

        /// <summary>
        /// Standard Unity GraphView pattern for screen → graph local conversion. SearchWindow
        /// hands us screen coordinates; we need them in the canvas's pan/zoom-transformed
        /// local space. Three steps:
        ///   1. screen → window-local (subtract the EditorWindow's screen position).
        ///   2. window-local → root-local via ChangeCoordinatesTo (handles any toolbar offset).
        ///   3. root-local → graph-local via the GraphView's content transform.
        /// </summary>
        Vector2 ScreenToGraphPosition(Vector2 screenPos)
        {
            if (_hostWindow == null || _hostWindow.rootVisualElement == null)
                return contentViewContainer.WorldToLocal(screenPos);

            var root = _hostWindow.rootVisualElement;
            var parent = root.parent ?? root;
            Vector2 windowMouse = root.ChangeCoordinatesTo(
                parent,
                screenPos - _hostWindow.position.position);
            return contentViewContainer.WorldToLocal(windowMouse);
        }

        void SpawnNode(Type subtype, Vector2 position)
        {
            if (_enemy == null) return;
            if (!typeof(AIDecisionNode).IsAssignableFrom(subtype) || subtype.IsAbstract) return;

            var node = (AIDecisionNode)Activator.CreateInstance(subtype);
            _snap.Nodes.Add(node);
            if (_snap.Root == null) _snap.Root = node;

            var view = new AIDecisionNodeView(node);
            view.SetPosition(new Rect(position, Vector2.zero));
            AddElement(view);
            _views[node] = view;

            RefreshRootIndicators();
            MarkDirty();
        }

        // ---- save back to SO ---------------------------------------------

        void MarkDirty()
        {
            if (_enemy == null || _snap == null) return;

            var positions = new Dictionary<AIDecisionNode, Vector2>();
            foreach (var kv in _views)
                positions[kv.Key] = kv.Value.GetPosition().position;

            var newRoot = AITreeSerializer.Save(_snap, out var errors);
            if (errors.Count > 0)
            {
                _statusLabel.text = "Errors: " + string.Join(" · ", errors.ConvertAll(e => e.Message));
                return;
            }
            _statusLabel.text = string.Empty;

            Undo.RecordObject(_enemy, "Edit AI Tree");
            _enemy.AIRoot = newRoot;
            EditorUtility.SetDirty(_enemy);
            AITreeLayoutSidecar.Save(_enemy, _snap, positions);
        }

        public void DisposeViews()
        {
            _views.Clear();
            Inspector?.Dispose();
        }
    }
}
